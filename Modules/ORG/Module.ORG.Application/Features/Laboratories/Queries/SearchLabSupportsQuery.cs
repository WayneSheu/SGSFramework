using MediatR;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Paginations;
using SGSFramework.Core.Results;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Queries
{
    /// <summary>
    /// 實驗室支援狀態（由後端依現在時間與有效期間計算，前端直接顯示）
    /// </summary>
    public static class LabSupportStatus
    {
        public const string Active = "Active";
        public const string Upcoming = "Upcoming";
        public const string Expired = "Expired";
        public const string Revoked = "Revoked";
    }

    /// <summary>
    /// 實驗室支援查詢條件（POST search，Request Body 傳遞）
    /// </summary>
    public sealed record LabSupportSearchRequest
    {
        public string? EmployeeNo { get; init; }
        public string? UserName { get; init; }
        /// <summary>實驗室關鍵字（人員主要實驗室或目標實驗室之代碼/名稱）</summary>
        public string? Laboratory { get; init; }
        /// <summary>人員主要實驗室所屬區域（上層組織名稱關鍵字）</summary>
        public string? Region { get; init; }
        /// <summary>區域實驗室代碼（完全比對，不分大小寫）；比對人員主要實驗室的代碼。null/空白＝全部</summary>
        public string? RegionalLabCode { get; init; }
        /// <summary>實驗室別（區域實驗室的上層組織 Id，即 Level 1 類別）；null＝全部。比對人員主要實驗室所屬的實驗室別</summary>
        public int? LabCategoryId { get; init; }
        /// <summary>歸屬類別；null＝全部</summary>
        public LabAssignmentType? AssignmentType { get; init; }
        /// <summary>狀態（LabSupportStatus）；null/空白＝全部</summary>
        public string? Status { get; init; }
        /// <summary>期間起（含）：與歸屬有效期間有交集者</summary>
        public DateTime? DateFrom { get; init; }
        /// <summary>期間迄（含）</summary>
        public DateTime? DateTo { get; init; }
        /// <summary>綜合關鍵字：員工編號、姓名、職稱、實驗室代碼/名稱、區域</summary>
        public string? Keyword { get; init; }
        public int PageIndex { get; init; } = 1;
        public int PageSize { get; init; } = 10;
    }

    /// <summary>
    /// 實驗室支援列表項目
    /// </summary>
    public sealed record LabSupportDto
    {
        public Guid UserId { get; init; }
        public string EmployeeNo { get; init; } = string.Empty;
        public string UserName { get; init; } = string.Empty;
        /// <summary>人員主要實驗室</summary>
        public string HomeLabName { get; init; } = string.Empty;
        /// <summary>人員主要實驗室所屬區域</summary>
        public string HomeRegionName { get; init; } = string.Empty;
        /// <summary>人員主要實驗室代碼</summary>
        public string HomeLabCode { get; init; } = string.Empty;
        /// <summary>人員主要實驗室所屬實驗室別（上層組織）Id</summary>
        public int? HomeCategoryId { get; init; }
        /// <summary>目標實驗室（支援類別即支援實驗室）</summary>
        public int SupportLabId { get; init; }
        public Guid SupportTenantLabId { get; init; }
        public string SupportLabCode { get; init; } = string.Empty;
        public string SupportLabName { get; init; } = string.Empty;
        public string? JobTitle { get; init; }
        public DateTimeOffset EffectiveDate { get; init; }
        public DateTimeOffset? ExpiryDate { get; init; }
        public bool IsActive { get; init; }
        public LabAssignmentType AssignmentType { get; init; }
        /// <summary>LabSupportStatus：Active / Upcoming / Expired / Revoked</summary>
        public string Status { get; init; } = LabSupportStatus.Active;
    }

    public sealed record SearchLabSupportsQuery(LabSupportSearchRequest Request)
        : IRequest<Result<PagedResult<LabSupportDto>>>;

    public sealed class SearchLabSupportsQueryHandler
        : IRequestHandler<SearchLabSupportsQuery, Result<PagedResult<LabSupportDto>>>
    {
        // 與命令層一致的台灣時區（+08:00，無日光節約）
        private static readonly TimeSpan TaiwanOffset = TimeSpan.FromHours(8);

        private readonly ICoreDbContext _coreContext;
        private readonly ORGDbContext _orgContext;
        private readonly TimeProvider _clock;

        public SearchLabSupportsQueryHandler(ICoreDbContext coreContext, ORGDbContext orgContext, TimeProvider clock)
        {
            _coreContext = coreContext ?? throw new ArgumentNullException(nameof(coreContext));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public async Task<Result<PagedResult<LabSupportDto>>> Handle(
            SearchLabSupportsQuery query, CancellationToken cancellationToken)
        {
            var req = query.Request;
            var pageIndex = Math.Max(1, req.PageIndex);
            var pageSize = Math.Clamp(req.PageSize, 1, 200);
            var now = _clock.GetUtcNow();

            var mappings = _coreContext.UserLabMappings.AsNoTracking().AsQueryable();

            if (req.AssignmentType.HasValue)
                mappings = mappings.Where(m => m.AssignmentType == req.AssignmentType.Value);

            if (!string.IsNullOrWhiteSpace(req.EmployeeNo))
            {
                var no = req.EmployeeNo.Trim();
                mappings = mappings.Where(m => m.User.EmployeeNo != null && m.User.EmployeeNo.Contains(no));
            }
            if (!string.IsNullOrWhiteSpace(req.UserName))
            {
                var name = req.UserName.Trim();
                mappings = mappings.Where(m => m.User.FullName.Contains(name));
            }

            // 期間：與 [DateFrom 00:00, DateTo 23:59:59.9999999] 有交集
            if (req.DateFrom.HasValue)
            {
                var from = new DateTimeOffset(req.DateFrom.Value.Date, TaiwanOffset);
                mappings = mappings.Where(m => m.ExpiryDate == null || m.ExpiryDate >= from);
            }
            if (req.DateTo.HasValue)
            {
                var to = new DateTimeOffset(req.DateTo.Value.Date, TaiwanOffset).AddDays(1).AddTicks(-1);
                mappings = mappings.Where(m => m.EffectiveDate <= to);
            }

            // 狀態（與 UserLabMapping.IsValidAt 同一判斷：生效日起、失效時間止）
            switch (req.Status?.Trim())
            {
                case LabSupportStatus.Revoked:
                    mappings = mappings.Where(m => !m.IsActive);
                    break;
                case LabSupportStatus.Upcoming:
                    mappings = mappings.Where(m => m.IsActive && m.EffectiveDate > now);
                    break;
                case LabSupportStatus.Expired:
                    mappings = mappings.Where(m => m.IsActive && m.ExpiryDate != null && m.ExpiryDate < now);
                    break;
                case LabSupportStatus.Active:
                    mappings = mappings.Where(m => m.IsActive && m.EffectiveDate <= now
                        && (m.ExpiryDate == null || m.ExpiryDate >= now));
                    break;
            }

            var rows = await mappings
                .OrderBy(m => m.User.EmployeeNo).ThenBy(m => m.EffectiveDate)
                .Select(m => new
                {
                    m.UserId,
                    EmployeeNo = m.User.EmployeeNo ?? string.Empty,
                    UserName = m.User.FullName,
                    m.LabId,
                    m.TenantLabId,
                    m.AssignmentType,
                    m.JobTitle,
                    m.EffectiveDate,
                    m.ExpiryDate,
                    m.IsActive
                })
                .ToListAsync(cancellationToken);

            // 人員主要實驗室與組織名稱位於不同 DbContext，於記憶體組合
            var userIds = rows.Select(r => r.UserId).Distinct().ToList();
            var primaries = await _coreContext.UserLabMappings.AsNoTracking()
                .Where(m => userIds.Contains(m.UserId) && m.AssignmentType == LabAssignmentType.Primary && m.IsActive)
                .Select(m => new { m.UserId, m.LabId })
                .ToListAsync(cancellationToken);
            var primaryByUser = primaries.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.First().LabId);

            var labIds = rows.Select(r => r.LabId).Concat(primaries.Select(p => p.LabId)).Distinct().ToList();
            var labs = await _orgContext.Organizations.AsNoTracking()
                .Include(o => o.Parent)
                .Where(o => labIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, cancellationToken);

            IEnumerable<LabSupportDto> items = rows.Select(r =>
            {
                labs.TryGetValue(r.LabId, out var targetLab);
                var homeLab = primaryByUser.TryGetValue(r.UserId, out var homeId) && labs.TryGetValue(homeId, out var h) ? h : null;
                return new LabSupportDto
                {
                    UserId = r.UserId,
                    EmployeeNo = r.EmployeeNo,
                    UserName = r.UserName,
                    HomeLabName = homeLab?.Name ?? string.Empty,
                    HomeRegionName = homeLab?.Parent?.Name ?? string.Empty,
                    HomeLabCode = homeLab?.Code ?? string.Empty,
                    HomeCategoryId = homeLab?.ParentId,
                    SupportLabId = r.LabId,
                    SupportTenantLabId = r.TenantLabId,
                    SupportLabCode = targetLab?.Code ?? string.Empty,
                    SupportLabName = targetLab?.Name ?? string.Empty,
                    JobTitle = r.JobTitle,
                    EffectiveDate = r.EffectiveDate,
                    ExpiryDate = r.ExpiryDate,
                    IsActive = r.IsActive,
                    AssignmentType = r.AssignmentType,
                    Status = ResolveStatus(r.IsActive, r.EffectiveDate, r.ExpiryDate, now)
                };
            });

            if (!string.IsNullOrWhiteSpace(req.Laboratory))
            {
                var kw = req.Laboratory.Trim();
                items = items.Where(i => Has(i.HomeLabName, kw) || Has(i.SupportLabName, kw) || Has(i.SupportLabCode, kw));
            }
            if (!string.IsNullOrWhiteSpace(req.RegionalLabCode))
            {
                var code = req.RegionalLabCode.Trim();
                items = items.Where(i => string.Equals(i.HomeLabCode, code, StringComparison.OrdinalIgnoreCase));
            }
            if (req.LabCategoryId.HasValue)
            {
                var categoryId = req.LabCategoryId.Value;
                items = items.Where(i => i.HomeCategoryId == categoryId);
            }
            if (!string.IsNullOrWhiteSpace(req.Region))
            {
                var kw = req.Region.Trim();
                items = items.Where(i => Has(i.HomeRegionName, kw));
            }
            if (!string.IsNullOrWhiteSpace(req.Keyword))
            {
                var kw = req.Keyword.Trim();
                items = items.Where(i => Has(i.EmployeeNo, kw) || Has(i.UserName, kw) || Has(i.JobTitle, kw)
                    || Has(i.HomeLabName, kw) || Has(i.HomeRegionName, kw)
                    || Has(i.SupportLabName, kw) || Has(i.SupportLabCode, kw));
            }

            var all = items.ToList();
            var page = all.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
            return Result.Success(new PagedResult<LabSupportDto>(page, all.Count, pageIndex, pageSize));
        }

        private static bool Has(string? source, string keyword) =>
            source != null && source.Contains(keyword, StringComparison.OrdinalIgnoreCase);

        private static string ResolveStatus(bool isActive, DateTimeOffset effective, DateTimeOffset? expiry, DateTimeOffset now)
        {
            if (!isActive) return LabSupportStatus.Revoked;
            if (effective > now) return LabSupportStatus.Upcoming;
            if (expiry.HasValue && expiry.Value < now) return LabSupportStatus.Expired;
            return LabSupportStatus.Active;
        }
    }
}
