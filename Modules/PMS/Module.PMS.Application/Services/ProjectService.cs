using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 專案、成員、卡控節點與交付物用例（M1）。所有查詢與異動皆套用 <see cref="ProjectAccessPolicy"/> 資料列授權：
/// 非成員一律回 404（防 IDOR，SEC-05）。功能權限由 Controller 的 [RequiresPermission] 負責。
/// </summary>
public sealed class ProjectService(PMSDbContext db, TimeProvider clock)
{
    private const int MaxPageSize = 200;

    // ---------- 專案代號 ----------

    public async Task<ProjectCodeCheckDto> CheckCodeAsync(string? code, CancellationToken ct)
    {
        var normalized = ProjectCodePolicy.Normalize(code);
        var check = ProjectCodePolicy.Check(normalized, await UsageOfAsync(normalized, ct).ConfigureAwait(false));
        return new ProjectCodeCheckDto(normalized, check == ProjectCodeCheck.Available, check == ProjectCodeCheck.Available ? null : check.ToString());
    }

    public async Task<ProjectCodeSuggestionDto> SuggestCodeAsync(string? name, CancellationToken ct)
    {
        // 候選以一次查詢載入已佔用代號，避免逐一查詢
        var taken = await db.ProjectCodeReservations.AsNoTracking().Select(r => r.Code).ToListAsync(ct).ConfigureAwait(false);
        var set = new HashSet<string>(taken, StringComparer.Ordinal);
        return new ProjectCodeSuggestionDto(ProjectCodePolicy.Suggest(name, set.Contains));
    }

    private async Task<ProjectCodeUsage?> UsageOfAsync(string code, CancellationToken ct)
    {
        if (!ProjectCodePolicy.IsValidFormat(code)) return null;
        var status = await db.ProjectCodeReservations.AsNoTracking()
            .Where(r => r.Code == code).Select(r => (ProjectCodeStatus?)r.Status).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return status switch
        {
            null => null,
            ProjectCodeStatus.Retired => ProjectCodeUsage.Retired,
            _ => ProjectCodeUsage.InUse
        };
    }

    // ---------- 專案 ----------

    /// <param name="viewAll">具 PMS.PROJECT.ADMIN（檢視並管理所有專案）者為 true：清單不受成員限制（由控制器依授予的權限判定）。</param>
    public async Task<PagedResult<ProjectListDto>> SearchAsync(Guid userId, ProjectSearchRequest request, CancellationToken ct, bool viewAll = false)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var filter = request.Filter ?? new ProjectSearchFilter();

        // 資料列授權：僅「我是成員」的專案；具 PMS.PROJECT.ADMIN 者可見全部專案，勾選 MineOnly 時仍只看我參與的
        var query = db.Projects.AsNoTracking().AsQueryable();
        if (!viewAll || filter.MineOnly) query = query.Where(p => p.Members.Any(m => m.UserId == userId));

        if (filter.Status is { } status) query = query.Where(p => p.Status == status);
        if (filter.Stage is { } stage) query = query.Where(p => p.Stage == stage);
        if (filter.Phase is { } phase) query = query.Where(p => p.Phase == phase);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(p => p.Code.Contains(term) || p.Name.Contains(term));
        }

        var sort = request.Sorts.FirstOrDefault();
        var desc = sort?.Direction == SortDirection.Descending;
        query = (sort?.SortBy?.ToLowerInvariant()) switch
        {
            "name" => desc ? query.OrderByDescending(p => p.Name) : query.OrderBy(p => p.Name),
            "phase" => desc ? query.OrderByDescending(p => p.Phase) : query.OrderBy(p => p.Phase),
            "createdat" => desc ? query.OrderByDescending(p => p.CreatedAtUtc) : query.OrderBy(p => p.CreatedAtUtc),
            _ => desc ? query.OrderByDescending(p => p.Code) : query.OrderBy(p => p.Code)
        };

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query.Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(p => new ProjectListDto(p.Id, p.Code, p.Name, p.Phase, p.Stage, p.Status, p.PmUserId, p.TechLeadUserId, p.FreezeLineDate))
            .ToListAsync(ct).ConfigureAwait(false);

        return new PagedResult<ProjectListDto>(items, total, pageIndex, pageSize);
    }

    public async Task<PmsResult<ProjectDetailDto>> CreateAsync(string actor, CreateProjectRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var code = ProjectCodePolicy.Normalize(request.Code);
        if (!ProjectCodePolicy.IsValidFormat(code))
            return PmsError.Validation("專案代號須為 2~10 碼大寫英數且首碼為字母。", "PMS.PROJECT_CODE_INVALID");
        if (ProjectCodePolicy.IsReservedWord(code))
            return PmsError.Validation($"「{code}」為保留字，不可作為專案代號。", "PMS.PROJECT_CODE_INVALID");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
            return PmsError.Validation("專案名稱為必填且不得超過 200 字。");
        if (request.PmUserId == Guid.Empty || request.TechLeadUserId == Guid.Empty)
            return PmsError.Validation("須指定 PM 與 Tech Lead。");
        if (request.PmUserId == request.TechLeadUserId)
            return PmsError.Validation("PM 與 Tech Lead 須為不同人（技術類 A 為 Tech Lead、範圍時程類 A 為 PM）。");

        if (await UsageOfAsync(code, ct).ConfigureAwait(false) is not null)
            return PmsError.Conflict("PMS.PROJECT_CODE_TAKEN", $"專案代號 {code} 已使用或曾作廢，不可重用。");

        var now = clock.GetUtcNow();
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Stage = request.Stage,
            Phase = ProjectPhase.Initiation,
            Status = ProjectStatus.Active,
            PmUserId = request.PmUserId,
            TechLeadUserId = request.TechLeadUserId,
            FreezeLineDate = request.FreezeLineDate,
            ContractSlaNote = request.ContractSlaNote?.Trim(),
            CreatedBy = actor,
            CreatedAtUtc = now
        };

        project.Members.Add(NewMember(project.Id, request.PmUserId, RaciRole.Pm, actor, now));
        project.Members.Add(NewMember(project.Id, request.TechLeadUserId, RaciRole.TechLead, actor, now));
        foreach (var gate in GateRule.Order)
            project.Gates.Add(new ProjectGate { Id = Guid.NewGuid(), ProjectId = project.Id, Gate = gate, Status = GateStatus.Pending, CreatedBy = actor, CreatedAtUtc = now });

        db.Projects.Add(project);
        db.ProjectCodeReservations.Add(new ProjectCodeReservation
        {
            Code = code, Status = ProjectCodeStatus.Active, ProjectId = project.Id, CreatedBy = actor, CreatedAtUtc = now
        });

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return PmsError.Conflict("PMS.PROJECT_CODE_TAKEN", $"專案代號 {code} 已使用或曾作廢，不可重用。");
        }

        return await GetCoreAsync(project.Id, ct).ConfigureAwait(false) is { } dto ? PmsResult<ProjectDetailDto>.Success(dto) : PmsError.NotFound();
    }

    /// <param name="adminAccess">具 PMS.PROJECT.ADMIN 者為 true：非成員也可檢視專案詳情（以內部成員視角，但不含合約資訊；放行與核准仍須是專案成員）。</param>
    public async Task<PmsResult<ProjectDetailDto>> GetAsync(Guid userId, Guid id, CancellationToken ct, bool adminAccess = false)
    {
        var memberAccess = await AccessAsync(userId, id, ct).ConfigureAwait(false);
        var access = memberAccess == ProjectAccessLevel.None && adminAccess ? ProjectAccessLevel.Internal : memberAccess;
        if (access == ProjectAccessLevel.None) return PmsError.NotFound();

        var dto = await GetCoreAsync(id, ct).ConfigureAwait(false);
        if (dto is null) return PmsError.NotFound();

        // 所屬合約編號僅對內部成員顯示（客戶 Stakeholder 與只具檢視全部專案權限者看不到合約資訊）
        if (memberAccess == ProjectAccessLevel.Internal)
        {
            var link = await db.ContractProjects.AsNoTracking().Where(l => l.ProjectId == id)
                .Select(l => new { l.ContractId, l.Contract!.ContractNo }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (link is not null) dto = dto with { ContractId = link.ContractId, ContractNo = link.ContractNo };
        }

        return PmsResult<ProjectDetailDto>.Success(dto);
    }

    public async Task<PmsResult<ProjectDetailDto>> UpdateAsync(Guid userId, string actor, Guid id, UpdateProjectRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var project = await LoadForWriteAsync(userId, id, ct).ConfigureAwait(false);
        if (project.Error is { } error) return error;
        var p = project.Value!;

        if (request.Code is not null && !string.Equals(ProjectCodePolicy.Normalize(request.Code), p.Code, StringComparison.Ordinal))
            return PmsError.Unprocessable("PMS.PROJECT_CODE_IMMUTABLE", "專案代號核發後不可修改；如需更換請作廢專案並重新核發。");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
            return PmsError.Validation("專案名稱為必填且不得超過 200 字。");
        if (!TryParseRowVersion(request.RowVersion, out var rowVersion))
            return PmsError.Validation("rowVersion 格式不正確。");

        db.Entry(p).Property(x => x.RowVersion).OriginalValue = rowVersion;
        p.Name = request.Name.Trim();
        p.Stage = request.Stage;
        p.FreezeLineDate = request.FreezeLineDate;
        p.ContractSlaNote = request.ContractSlaNote?.Trim();
        p.UpdatedBy = actor;
        p.UpdatedAtUtc = clock.GetUtcNow();

        return await SaveAndReloadAsync(p.Id, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<ProjectDetailDto>> AdvancePhaseAsync(Guid userId, string actor, Guid id, AdvancePhaseRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var project = await LoadForWriteAsync(userId, id, ct).ConfigureAwait(false);
        if (project.Error is { } error) return error;
        var p = project.Value!;

        if (p.Status != ProjectStatus.Active)
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "僅進行中的專案可推進階段。");
        if (!TryParseRowVersion(request.RowVersion, out var rowVersion))
            return PmsError.Validation("rowVersion 格式不正確。");

        var gates = p.Gates.ToDictionary(g => g.Gate, g => g.Status);
        var verdict = GateRule.CanAdvance(p.Phase, request.TargetPhase, gates);
        if (!verdict.IsOk) return ToError(verdict);

        db.Entry(p).Property(x => x.RowVersion).OriginalValue = rowVersion;
        p.Phase = request.TargetPhase;
        p.UpdatedBy = actor;
        p.UpdatedAtUtc = clock.GetUtcNow();

        return await SaveAndReloadAsync(p.Id, ct).ConfigureAwait(false);
    }

    // ---------- 成員 ----------

    public async Task<PmsResult<ProjectMemberDto>> AddMemberAsync(Guid userId, string actor, Guid id, AddMemberRequest request, CancellationToken ct, bool adminAccess = false)
    {
        ArgumentNullException.ThrowIfNull(request);

        var project = await LoadForWriteAsync(userId, id, ct, adminAccess).ConfigureAwait(false);
        if (project.Error is { } error) return error;
        var p = project.Value!;

        if (request.UserId == Guid.Empty) return PmsError.Validation("userId 為必填。");
        if (request.RaciRole == RaciRole.Stakeholder)
            return PmsError.Validation("Stakeholder 不由此端點新增，請使用客戶簽核帳號邀請流程。");
        if (!Enum.IsDefined(request.RaciRole)) return PmsError.Validation("raciRole 不正確。");

        if (p.Members.Any(m => m.UserId == request.UserId && m.RaciRole == request.RaciRole))
            return PmsError.Conflict("PMS.MEMBER_EXISTS", "該成員已具有此角色。");

        // PM／Tech Lead 各僅一人（A 嚴格限制 1 人）
        if (request.RaciRole is RaciRole.Pm or RaciRole.TechLead && p.Members.Any(m => m.RaciRole == request.RaciRole))
            return PmsError.Conflict("PMS.INVALID_TRANSITION", $"{request.RaciRole} 僅能有一人；請先調整專案主檔後再變更。");

        var member = NewMember(p.Id, request.UserId, request.RaciRole, actor, clock.GetUtcNow());
        db.ProjectMembers.Add(member);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return PmsResult<ProjectMemberDto>.Success(new ProjectMemberDto(member.Id, member.UserId, member.RaciRole));
    }

    public async Task<PmsResult<bool>> RemoveMemberAsync(Guid userId, Guid id, Guid memberId, CancellationToken ct, bool adminAccess = false)
    {
        var project = await LoadForWriteAsync(userId, id, ct, adminAccess).ConfigureAwait(false);
        if (project.Error is { } error) return error;
        var p = project.Value!;

        var member = p.Members.FirstOrDefault(m => m.Id == memberId);
        if (member is null) return PmsError.NotFound("成員不存在。");

        if (member.RaciRole is RaciRole.Pm or RaciRole.TechLead)
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "PM 與 Tech Lead 不可移除。");

        db.ProjectMembers.Remove(member);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return PmsResult<bool>.Success(true);
    }

    // ---------- 模組目錄 ----------

    public async Task<PmsResult<PmsModuleDto>> AddModuleAsync(Guid userId, string actor, Guid id, AddModuleRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var project = await LoadForWriteAsync(userId, id, ct).ConfigureAwait(false);
        if (project.Error is { } error) return error;
        var p = project.Value!;

        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^M\d{2}$"))
            return PmsError.Validation("模組代號格式須為 Mxx（例：M01）。");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100)
            return PmsError.Validation("模組名稱為必填且不得超過 100 字。");
        if (await db.Modules.AnyAsync(m => m.ProjectId == p.Id && m.Code == code, ct).ConfigureAwait(false))
            return PmsError.Conflict("PMS.MODULE_EXISTS", $"模組代號 {code} 已存在。");

        var module = new PmsModule { Id = Guid.NewGuid(), ProjectId = p.Id, Code = code, Name = request.Name.Trim(), CreatedBy = actor, CreatedAtUtc = clock.GetUtcNow() };
        db.Modules.Add(module);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return PmsResult<PmsModuleDto>.Success(new PmsModuleDto(module.Id, module.Code, module.Name));
    }

    // ---------- Gate ----------

    public async Task<PmsResult<ProjectGateDto>> DecideGateAsync(Guid userId, string actor, Guid id, GateKind gate, DecideGateRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var project = await LoadForWriteAsync(userId, id, ct).ConfigureAwait(false);
        if (project.Error is { } error)
        {
            // 專案存在但呼叫者不是內部成員：明確告知原因，避免被誤認為資料不存在（專案不存在仍回 404）
            if (error.Status == 404 && await db.Projects.AsNoTracking().AnyAsync(x => x.Id == id, ct).ConfigureAwait(false))
                return PmsError.Forbidden("您不是此專案成員，無法決議。");

            return error;
        }
        var p = project.Value!;

        if (!Enum.IsDefined(gate)) return PmsError.Validation("gate 不正確。");
        if (request.EvidenceNote is { Length: > 1000 }) return PmsError.Validation("證據說明不得超過 1000 字。");

        var members = p.Members.Select(m => (m.UserId, m.RaciRole));
        var isTechLead = ProjectAccessPolicy.IsTechLead(userId, members);
        var current = p.Gates.ToDictionary(g => g.Gate, g => g.Status);

        var verdict = GateRule.CanDecide(gate, request.Result, current, isTechLead);
        if (!verdict.IsOk) return ToError(verdict);

        var entity = p.Gates.FirstOrDefault(g => g.Gate == gate);
        if (entity is null)
        {
            entity = new ProjectGate { Id = Guid.NewGuid(), ProjectId = p.Id, Gate = gate, CreatedBy = actor, CreatedAtUtc = clock.GetUtcNow() };
            db.ProjectGates.Add(entity);
        }

        entity.Status = request.Result;
        entity.ApprovedBy = userId;
        entity.ApprovedAt = clock.GetUtcNow();
        entity.EvidenceNote = request.EvidenceNote?.Trim();
        entity.UpdatedBy = actor;
        entity.UpdatedAtUtc = entity.ApprovedAt;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return PmsResult<ProjectGateDto>.Success(new ProjectGateDto(entity.Gate, entity.Status, entity.ApprovedBy, entity.ApprovedAt, entity.EvidenceNote));
    }

    // ---------- 命名檢查（交付物上傳見 DeliverableFileService） ----------

    public static NamingCheckDto CheckNaming(NamingCheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = DeliverableNaming.Check(request.Kind, request.FileName);
        return new NamingCheckDto(result.Valid, result.Violations);
    }

    // ---------- 內部輔助 ----------

    private async Task<ProjectAccessLevel> AccessAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var members = await db.ProjectMembers.AsNoTracking()
            .Where(m => m.ProjectId == projectId).Select(m => new { m.UserId, m.RaciRole }).ToListAsync(ct).ConfigureAwait(false);
        return ProjectAccessPolicy.Evaluate(userId, members.Select(m => (m.UserId, m.RaciRole)));
    }

    /// <summary>載入含成員／Gate／模組的專案供寫入；非內部成員一律 404（Stakeholder 不可管理專案）。</summary>
    /// <param name="adminAccess">具 PMS.PROJECT.ADMIN 者可管理專案成員（建立後補齊或調整成員）；其他寫入（放行卡控節點、核准）仍須是專案成員。</param>
    private async Task<PmsResult<Project>> LoadForWriteAsync(Guid userId, Guid id, CancellationToken ct, bool adminAccess = false)
    {
        var project = await db.Projects.Include(p => p.Members).Include(p => p.Gates).Include(p => p.Modules)
            .FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound();

        var access = ProjectAccessPolicy.Evaluate(userId, project.Members.Select(m => (m.UserId, m.RaciRole)));
        return access == ProjectAccessLevel.Internal || adminAccess ? PmsResult<Project>.Success(project) : PmsError.NotFound();
    }

    private async Task<ProjectDetailDto?> GetCoreAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Projects.AsNoTracking().Include(x => x.Members).Include(x => x.Gates).Include(x => x.Modules)
            .FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (p is null) return null;

        return new ProjectDetailDto(
            p.Id, p.Code, p.Name, p.Phase, p.Stage, p.Status, p.PmUserId, p.TechLeadUserId, p.FreezeLineDate, p.ContractSlaNote,
            p.Members.OrderBy(m => m.RaciRole).Select(m => new ProjectMemberDto(m.Id, m.UserId, m.RaciRole)).ToList(),
            p.Modules.OrderBy(m => m.Code).Select(m => new PmsModuleDto(m.Id, m.Code, m.Name)).ToList(),
            p.Gates.OrderBy(g => g.Gate).Select(g => new ProjectGateDto(g.Gate, g.Status, g.ApprovedBy, g.ApprovedAt, g.EvidenceNote)).ToList(),
            Convert.ToBase64String(p.RowVersion));
    }

    private async Task<PmsResult<ProjectDetailDto>> SaveAndReloadAsync(Guid projectId, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "資料已被他人更新，請重新載入後再試。");
        }

        db.ChangeTracker.Clear();
        return await GetCoreAsync(projectId, ct).ConfigureAwait(false) is { } dto ? PmsResult<ProjectDetailDto>.Success(dto) : PmsError.NotFound();
    }

    private static ProjectMember NewMember(Guid projectId, Guid userId, RaciRole role, string actor, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), ProjectId = projectId, UserId = userId, RaciRole = role, CreatedBy = actor, CreatedAtUtc = now
    };

    private static PmsError ToError(RuleResult result) => result.Outcome switch
    {
        RuleOutcome.GatePrerequisiteNotMet => PmsError.Unprocessable("PMS.GATE_PREREQUISITE_NOT_MET", result.Detail ?? "前道卡控節點尚未通過。"),
        RuleOutcome.RoleNotAllowed => PmsError.Forbidden(result.Detail ?? "角色不符。"),
        _ => PmsError.Conflict("PMS.INVALID_TRANSITION", result.Detail ?? "狀態不允許該轉移。")
    };

    private static bool TryParseRowVersion(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { bytes = Convert.FromBase64String(value); return bytes.Length > 0; }
        catch (FormatException) { return false; }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
