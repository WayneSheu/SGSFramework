using Microsoft.EntityFrameworkCore;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.ProjectContracts;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// Core <see cref="IMeetingProjectLookup"/> 的 PMS 實作（會議助理 MTG → 專案）：與 <see cref="ProjectService"/> 同一套資料列授權——
/// 只有專案成員（或具 PMS.PROJECT.ADMIN 者）看得到專案；看不到的專案不會出現在搜尋與批次查詢結果中。
/// 搜尋只列進行中的專案（已結案或作廢的不該再被新會議關聯）；批次查詢則涵蓋所有狀態，讓既有關聯仍能顯示名稱。
/// </summary>
public sealed class MeetingProjectLookup(PMSDbContext db) : IMeetingProjectLookup
{
    private const int MaxIds = 200;

    public bool IsAvailable => true;

    public async Task<IReadOnlyList<MeetingProjectInfo>> SearchAsync(Guid userId, bool viewAll, string? keyword, int max, CancellationToken cancellationToken = default)
    {
        var query = db.Projects.AsNoTracking().Where(p => p.Status == ProjectStatus.Active);
        if (!viewAll) query = query.Where(p => p.Members.Any(m => m.UserId == userId));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(p => p.Code.Contains(term) || p.Name.Contains(term));
        }

        return await query.OrderBy(p => p.Name).ThenBy(p => p.Id).Take(Math.Clamp(max, 1, 50))
            .Select(p => new MeetingProjectInfo(p.Id, p.Code, p.Name))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<Guid, MeetingProjectInfo>> GetManyAsync(Guid userId, bool viewAll, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return new Dictionary<Guid, MeetingProjectInfo>();

        var wanted = ids.Distinct().Take(MaxIds).ToList();
        var query = db.Projects.AsNoTracking().Where(p => wanted.Contains(p.Id));
        if (!viewAll) query = query.Where(p => p.Members.Any(m => m.UserId == userId));

        var rows = await query.Select(p => new MeetingProjectInfo(p.Id, p.Code, p.Name)).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.ToDictionary(r => r.Id);
    }
}
