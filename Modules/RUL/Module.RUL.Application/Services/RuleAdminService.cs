using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.RUL.Application.Contracts;
using SGS.Modules.RUL.Application.Rules;
using SGS.Modules.RUL.Infrastructure.Dbcontexts;
using SGS.Modules.RUL.Infrastructure.Entities;
using SGSFramework.Core.Rules;

namespace SGS.Modules.RUL.Application.Services;

/// <summary>規則維護與 Maker-Checker 審批：規則內容只能經「變更申請 → 他人核准」生效。</summary>
public sealed class RuleAdminService(
    RULDbContext db,
    IRuleExpressionValidator validator,
    IRuleTargetCatalog catalog,
    IRuleTester tester,
    RuleSnapshotProvider snapshots,
    TimeProvider clock)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public IReadOnlyList<RuleTargetDto> GetTargets() =>
        catalog.GetTargets().Select(t => new RuleTargetDto(t.Name, t.Description, t.Fields)).ToList();

    // ── 查詢 ─────────────────────────────────────────────

    public async Task<PagedList<RuleDto>> SearchAsync(RuleSearchRequest req, CancellationToken ct)
    {
        var q = db.Rules.AsNoTracking().AsQueryable();
        if (req.Domain is { } d) q = q.Where(r => r.Domain == d);
        if (req.Status is { } s) q = q.Where(r => r.Status == s);
        if (!string.IsNullOrWhiteSpace(req.Keyword))
        {
            var k = req.Keyword.Trim();
            q = q.Where(r => r.Name.Contains(k) || r.TargetEntity.Contains(k) || (r.FieldName != null && r.FieldName.Contains(k)));
        }
        var total = await q.CountAsync(ct).ConfigureAwait(false);
        var (page, size) = Clamp(req.Page, req.PageSize);
        var rows = await q.OrderBy(r => r.Domain).ThenBy(r => r.TargetEntity).ThenByDescending(r => r.Priority).ThenBy(r => r.Id)
            .Skip((page - 1) * size).Take(size).ToListAsync(ct).ConfigureAwait(false);
        return new PagedList<RuleDto>(rows.Select(ToDto).ToList(), total);
    }

    public async Task<RulResult<RuleDto>> GetAsync(long id, CancellationToken ct)
    {
        var r = await db.Rules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        return r is null ? RulResult<RuleDto>.Failure(RulError.NotFound("規則不存在。")) : RulResult<RuleDto>.Success(ToDto(r));
    }

    public async Task<PagedList<ChangeDto>> SearchChangesAsync(ChangeSearchRequest req, CancellationToken ct)
    {
        var q = db.ChangeRequests.AsNoTracking().AsQueryable();
        if (req.Status is { } s) q = q.Where(c => c.Status == s);
        if (req.RuleId is { } rid) q = q.Where(c => c.RuleId == rid);
        var total = await q.CountAsync(ct).ConfigureAwait(false);
        var (page, size) = Clamp(req.Page, req.PageSize);
        var rows = await q.OrderByDescending(c => c.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct).ConfigureAwait(false);
        return new PagedList<ChangeDto>(rows.Select(ToDto).ToList(), total);
    }

    public async Task<IReadOnlyList<RuleVersionDto>> GetVersionsAsync(long ruleId, CancellationToken ct)
    {
        var rows = await db.Versions.AsNoTracking().Where(v => v.RuleId == ruleId).OrderByDescending(v => v.Version).ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(v => new RuleVersionDto(v.Id, v.RuleId, v.Version, Deserialize(v.SnapshotJson)!, v.ApprovedBy, v.CreatedAtUtc)).ToList();
    }

    // ── 驗證與試跑 ─────────────────────────────────────

    public ValidateExpressionResult ValidateExpression(ValidateExpressionRequest req)
    {
        var c = validator.Validate(req.TargetEntity, req.Kind, req.ConditionExpr);
        if (!c.IsValid) return new(false, c.Error);
        if (!string.IsNullOrWhiteSpace(req.ActionExpr) && req.Kind == RuleKind.Pricing)
        {
            // 動作運算式回傳 decimal；以同一白名單檢查（語法/安全）
            var a = validator.Validate(req.TargetEntity, RuleKind.Column, $"({req.ActionExpr}) >= 0m");
            if (!a.IsValid) return new(false, $"動作運算式：{a.Error}");
        }
        return new(true, null);
    }

    public RulResult<RuleTestOutcome> DryRun(DryRunRequest req)
    {
        var err = CheckInput(req.Rule);
        if (err is not null) return RulResult<RuleTestOutcome>.Failure(RulError.Validation(err));
        var user = ParseUser(req.UserJson);
        return RulResult<RuleTestOutcome>.Success(tester.Test(ToSnapshot(0, 0, req.Rule), user, req.TargetJson));
    }

    // ── 變更申請（Maker） ─────────────────────────────────

    public async Task<RulResult<ChangeDto>> SubmitChangeAsync(string actor, SubmitChangeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length > 500)
            return Fail<ChangeDto>("申請理由必填且不可超過 500 字。");

        RuleInput? proposed = req.Proposed;
        if (req.ChangeType is RuleChangeType.Create or RuleChangeType.Update)
        {
            if (proposed is null) return Fail<ChangeDto>("新增／修改必須提供規則內容。");
            var err = CheckInput(proposed);
            if (err is not null) return Fail<ChangeDto>(err);
        }
        else proposed = null;

        if (req.ChangeType != RuleChangeType.Create)
        {
            if (req.RuleId is null) return Fail<ChangeDto>("缺少規則 Id。");
            var rule = await db.Rules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == req.RuleId, ct).ConfigureAwait(false);
            if (rule is null) return RulResult<ChangeDto>.Failure(RulError.NotFound("規則不存在。"));
            if (req.ChangeType == RuleChangeType.Disable && rule.Status == RuleStatus.Disabled) return Fail<ChangeDto>("規則已停用。");
            if (req.ChangeType == RuleChangeType.Enable && rule.Status == RuleStatus.Active) return Fail<ChangeDto>("規則已啟用。");
            if (await db.ChangeRequests.AnyAsync(c => c.RuleId == req.RuleId && c.Status == RuleChangeStatus.Pending, ct).ConfigureAwait(false))
                return RulResult<ChangeDto>.Failure(RulError.Conflict("RUL.PENDING_EXISTS", "此規則已有待審批的變更申請。"));
        }

        var entity = new RuleChangeRequest
        {
            RuleId = req.ChangeType == RuleChangeType.Create ? null : req.RuleId,
            ChangeType = req.ChangeType,
            ProposedJson = proposed is null ? null : JsonSerializer.Serialize(proposed, Json),
            Reason = req.Reason.Trim(),
            RequestedBy = actor,
            RequestedAtUtc = clock.GetUtcNow(),
            CreatedBy = actor,
            CreatedAtUtc = clock.GetUtcNow()
        };
        db.ChangeRequests.Add(entity);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return RulResult<ChangeDto>.Success(ToDto(entity));
    }

    public async Task<RulResult<ChangeDto>> RollbackAsync(string actor, RollbackRequest req, CancellationToken ct)
    {
        var v = await db.Versions.AsNoTracking().FirstOrDefaultAsync(x => x.RuleId == req.RuleId && x.Version == req.Version, ct).ConfigureAwait(false);
        if (v is null) return RulResult<ChangeDto>.Failure(RulError.NotFound("指定版本不存在。"));
        return await SubmitChangeAsync(actor, new SubmitChangeRequest
        {
            RuleId = req.RuleId,
            ChangeType = RuleChangeType.Update,
            Proposed = Deserialize(v.SnapshotJson),
            Reason = $"回滾至 v{req.Version}：{req.Reason}"
        }, ct).ConfigureAwait(false);
    }

    public async Task<RulResult<bool>> CancelAsync(string actor, long id, CancellationToken ct)
    {
        var c = await db.ChangeRequests.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (c is null) return RulResult<bool>.Failure(RulError.NotFound("變更申請不存在。"));
        if (c.Status != RuleChangeStatus.Pending) return RulResult<bool>.Failure(RulError.Conflict("RUL.NOT_PENDING", "只有待審批的申請可以撤回。"));
        if (!string.Equals(c.RequestedBy, actor, StringComparison.OrdinalIgnoreCase))
            return RulResult<bool>.Failure(RulError.Forbidden("只有申請者可以撤回。"));
        c.Status = RuleChangeStatus.Cancelled;
        c.UpdatedBy = actor;
        c.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return RulResult<bool>.Success(true);
    }

    // ── 審核（Checker） ─────────────────────────────────

    public async Task<RulResult<ChangeDto>> ApproveAsync(string actor, long id, ReviewRequest req, CancellationToken ct)
    {
        var c = await db.ChangeRequests.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (c is null) return RulResult<ChangeDto>.Failure(RulError.NotFound("變更申請不存在。"));
        if (c.Status != RuleChangeStatus.Pending) return RulResult<ChangeDto>.Failure(RulError.Conflict("RUL.NOT_PENDING", "此申請已處理。"));
        if (RuleApprovalRules.IsSelfApproval(c.RequestedBy, actor))
            return RulResult<ChangeDto>.Failure(RulError.Forbidden("申請者不可審核自己的變更（職責分離）。", "RUL.SELF_APPROVAL"));

        var now = clock.GetUtcNow();
        RuleDefinition rule;
        if (c.ChangeType == RuleChangeType.Create)
        {
            var input = Deserialize(c.ProposedJson)!;
            var err = CheckInput(input);
            if (err is not null) return Fail<ChangeDto>($"提議內容已不合法：{err}");
            rule = new RuleDefinition { CreatedBy = actor, CreatedAtUtc = now, Version = 1, Status = RuleStatus.Active };
            Apply(rule, input);
            db.Rules.Add(rule);
        }
        else
        {
            rule = (await db.Rules.FirstOrDefaultAsync(r => r.Id == c.RuleId, ct).ConfigureAwait(false))!;
            if (rule is null) return RulResult<ChangeDto>.Failure(RulError.NotFound("規則不存在。"));
            switch (c.ChangeType)
            {
                case RuleChangeType.Update:
                    var input = Deserialize(c.ProposedJson)!;
                    var err = CheckInput(input);
                    if (err is not null) return Fail<ChangeDto>($"提議內容已不合法：{err}");
                    Apply(rule, input);
                    break;
                case RuleChangeType.Disable: rule.Status = RuleStatus.Disabled; break;
                case RuleChangeType.Enable: rule.Status = RuleStatus.Active; break;
            }
            rule.Version++;
            rule.UpdatedBy = actor;
            rule.UpdatedAtUtc = now;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false); // 取得 Create 的 Id
        db.Versions.Add(new RuleVersion
        {
            RuleId = rule.Id,
            Version = rule.Version,
            SnapshotJson = JsonSerializer.Serialize(ToInput(rule), Json),
            ChangeRequestId = c.Id,
            ApprovedBy = actor,
            CreatedBy = actor,
            CreatedAtUtc = now
        });
        c.RuleId = rule.Id;
        c.Status = RuleChangeStatus.Approved;
        c.ReviewedBy = actor;
        c.ReviewedAtUtc = now;
        c.ReviewComment = Trim(req?.Comment);
        c.UpdatedBy = actor;
        c.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        snapshots.Invalidate();
        return RulResult<ChangeDto>.Success(ToDto(c));
    }

    public async Task<RulResult<ChangeDto>> RejectAsync(string actor, long id, ReviewRequest req, CancellationToken ct)
    {
        var c = await db.ChangeRequests.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        if (c is null) return RulResult<ChangeDto>.Failure(RulError.NotFound("變更申請不存在。"));
        if (c.Status != RuleChangeStatus.Pending) return RulResult<ChangeDto>.Failure(RulError.Conflict("RUL.NOT_PENDING", "此申請已處理。"));
        if (RuleApprovalRules.IsSelfApproval(c.RequestedBy, actor))
            return RulResult<ChangeDto>.Failure(RulError.Forbidden("申請者不可審核自己的變更（職責分離）；如需取消請撤回。", "RUL.SELF_APPROVAL"));
        if (string.IsNullOrWhiteSpace(req?.Comment)) return Fail<ChangeDto>("駁回必須填寫理由。");

        c.Status = RuleChangeStatus.Rejected;
        c.ReviewedBy = actor;
        c.ReviewedAtUtc = clock.GetUtcNow();
        c.ReviewComment = Trim(req.Comment);
        c.UpdatedBy = actor;
        c.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return RulResult<ChangeDto>.Success(ToDto(c));
    }

    // ── 內部 ─────────────────────────────────────────────

    private string? CheckInput(RuleInput r)
    {
        var err = RuleInputRules.Validate(r);
        if (err is not null) return err;
        var v = ValidateExpression(new ValidateExpressionRequest(r.TargetEntity, r.Kind, r.ConditionExpr, r.ActionExpr));
        return v.IsValid ? null : v.Error;
    }

    private static RulResult<T> Fail<T>(string detail) => RulResult<T>.Failure(RulError.Validation(detail));

    private static (int Page, int Size) Clamp(int page, int size) => (Math.Max(1, page), Math.Clamp(size, 1, 200));

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim()[..Math.Min(s.Trim().Length, 500)];

    internal static RuleUserContext ParseUser(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new RuleUserContext();
        try { return JsonSerializer.Deserialize<RuleUserContext>(json, Json) ?? new RuleUserContext(); }
        catch (JsonException) { return new RuleUserContext(); }
    }

    private static RuleInput? Deserialize(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<RuleInput>(json, Json);

    private static void Apply(RuleDefinition r, RuleInput i)
    {
        r.Name = i.Name.Trim();
        r.Description = i.Description;
        r.Domain = i.Domain;
        r.Kind = i.Kind;
        r.TargetEntity = i.TargetEntity.Trim();
        r.FieldName = string.IsNullOrWhiteSpace(i.FieldName) ? null : i.FieldName.Trim();
        r.RoleName = i.RoleName.Trim();
        r.ConditionExpr = i.ConditionExpr.Trim();
        r.ActionExpr = string.IsNullOrWhiteSpace(i.ActionExpr) ? null : i.ActionExpr.Trim();
        r.OutputPayload = string.IsNullOrWhiteSpace(i.OutputPayload) ? null : i.OutputPayload;
        r.ReadAccess = i.ReadAccess;
        r.WriteAccess = i.WriteAccess;
        r.Risk = i.Risk;
        r.Priority = i.Priority;
        r.EffectiveFromUtc = i.EffectiveFromUtc;
        r.EffectiveToUtc = i.EffectiveToUtc;
    }

    internal static RuleInput ToInput(RuleDefinition r) => new()
    {
        Name = r.Name, Description = r.Description, Domain = r.Domain, Kind = r.Kind, TargetEntity = r.TargetEntity,
        FieldName = r.FieldName, RoleName = r.RoleName, ConditionExpr = r.ConditionExpr, ActionExpr = r.ActionExpr,
        OutputPayload = r.OutputPayload, ReadAccess = r.ReadAccess, WriteAccess = r.WriteAccess, Risk = r.Risk,
        Priority = r.Priority, EffectiveFromUtc = r.EffectiveFromUtc, EffectiveToUtc = r.EffectiveToUtc
    };

    internal static RuleSnapshot ToSnapshot(long id, int version, RuleInput i) => new()
    {
        Id = id, Version = version, Domain = i.Domain, Kind = i.Kind, TargetEntity = i.TargetEntity, FieldName = i.FieldName,
        RoleName = i.RoleName, ConditionExpr = i.ConditionExpr, ActionExpr = i.ActionExpr, OutputPayload = i.OutputPayload,
        ReadAccess = i.ReadAccess, WriteAccess = i.WriteAccess, Risk = i.Risk, Priority = i.Priority
    };

    private static RuleDto ToDto(RuleDefinition r) =>
        new(r.Id, ToInput(r), r.Status, r.Version, r.CreatedBy, r.CreatedAtUtc, r.UpdatedBy, r.UpdatedAtUtc);

    private static ChangeDto ToDto(RuleChangeRequest c) =>
        new(c.Id, c.RuleId, c.ChangeType, Deserialize(c.ProposedJson), c.Reason, c.Status, c.RequestedBy, c.RequestedAtUtc, c.ReviewedBy, c.ReviewedAtUtc, c.ReviewComment);
}
