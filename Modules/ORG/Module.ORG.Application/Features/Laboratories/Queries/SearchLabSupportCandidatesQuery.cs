using MediatR;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Results;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Queries
{
    /// <summary>
    /// 可被指派支援的人員及其主要實驗室
    /// </summary>
    public sealed record LabSupportCandidateDto(
        Guid UserId, string EmployeeNo, string UserName, string HomeLabName, string HomeRegionName, int? HomeCategoryId, int? HomeLabId = null);

    /// <param name="Keyword">員工編號、姓名或主要實驗室關鍵字；空白＝列出前 N 位</param>
    /// <param name="LabCategoryId">限定人員主要實驗室所屬的實驗室別（區域實驗室的上層組織 Id）；null＝不限。
    /// 新增支援時帶入支援實驗室所屬的實驗室別（與儲存時的後端檢核同一規則）</param>
    /// <param name="Take">最多回傳筆數；預設 50，上限 300（批次支援使用較大值）</param>
    public sealed record SearchLabSupportCandidatesQuery(string? Keyword, int? LabCategoryId = null, int Take = 50)
        : IRequest<Result<List<LabSupportCandidateDto>>>;

    public sealed class SearchLabSupportCandidatesQueryHandler
        : IRequestHandler<SearchLabSupportCandidatesQuery, Result<List<LabSupportCandidateDto>>>
    {
        private const int MaxTake = 300;
        private readonly ICoreDbContext _coreContext;
        private readonly ORGDbContext _orgContext;

        public SearchLabSupportCandidatesQueryHandler(ICoreDbContext coreContext, ORGDbContext orgContext)
        {
            _coreContext = coreContext ?? throw new ArgumentNullException(nameof(coreContext));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
        }

        public async Task<Result<List<LabSupportCandidateDto>>> Handle(
            SearchLabSupportCandidatesQuery query, CancellationToken cancellationToken)
        {
            var keyword = query.Keyword?.Trim();

            var primaries = _coreContext.UserLabMappings.AsNoTracking()
                .Where(m => m.IsActive && m.AssignmentType == LabAssignmentType.Primary);

            if (query.LabCategoryId.HasValue)
            {
                // 主要實驗室須屬於指定的實驗室別（其上層為該類別）
                var categoryId = query.LabCategoryId.Value;
                var labIdsInCategory = await _orgContext.Organizations.AsNoTracking()
                    .Where(o => o.ParentId == categoryId)
                    .Select(o => o.Id)
                    .ToListAsync(cancellationToken);
                primaries = primaries.Where(m => labIdsInCategory.Contains(m.LabId));
            }

            if (!string.IsNullOrEmpty(keyword))
            {
                // 主要實驗室名稱位於 ORG 資料庫：先找出符合的實驗室 Id
                var matchedLabIds = await _orgContext.Organizations.AsNoTracking()
                    .Where(o => o.Name.Contains(keyword) || o.Code.Contains(keyword))
                    .Select(o => o.Id)
                    .ToListAsync(cancellationToken);

                primaries = primaries.Where(m => m.User.FullName.Contains(keyword)
                    || (m.User.EmployeeNo != null && m.User.EmployeeNo.Contains(keyword))
                    || matchedLabIds.Contains(m.LabId));
            }

            var rows = await primaries
                .OrderBy(m => m.User.EmployeeNo).ThenBy(m => m.User.FullName)
                .Select(m => new { m.UserId, EmployeeNo = m.User.EmployeeNo ?? string.Empty, m.User.FullName, m.LabId })
                .Take(Math.Clamp(query.Take, 1, MaxTake))
                .ToListAsync(cancellationToken);

            var labIds = rows.Select(r => r.LabId).Distinct().ToList();
            var labs = await _orgContext.Organizations.AsNoTracking()
                .Include(o => o.Parent)
                .Where(o => labIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, cancellationToken);

            var result = rows.Select(r =>
            {
                labs.TryGetValue(r.LabId, out var lab);
                return new LabSupportCandidateDto(
                    r.UserId, r.EmployeeNo, r.FullName, lab?.Name ?? string.Empty, lab?.Parent?.Name ?? string.Empty, lab?.ParentId, r.LabId);
            }).ToList();

            return Result.Success(result);
        }
    }
}
