namespace SGSFramework.Core.ProjectContracts;

/// <summary>會議可關聯的專案（只含顯示所需的最少欄位）。</summary>
public sealed record MeetingProjectInfo(Guid Id, string Code, string Name);

/// <summary>
/// 會議 → 專案查詢（Core 契約）：由 SGS.Modules.PMS 外掛實作，會議助理模組（SGS.Modules.MTG）只依賴此介面，不直接參照 PMS。
/// 實作必須套用 PMS 的資料列授權：只回傳 <c>userId</c> 看得到的專案（成員，或 <c>viewAll</c> 為 true），
/// 看不到的專案一律當作不存在，避免藉由會議關聯探測專案是否存在或其名稱（IDOR）。
/// PMS 未安裝時由 <see cref="NullMeetingProjectLookup"/> 接管。
/// </summary>
public interface IMeetingProjectLookup
{
    bool IsAvailable { get; }

    /// <summary>依代號或名稱關鍵字搜尋可見專案（最多 <paramref name="max"/> 筆，依名稱排序）。</summary>
    Task<IReadOnlyList<MeetingProjectInfo>> SearchAsync(Guid userId, bool viewAll, string? keyword, int max, CancellationToken cancellationToken = default);

    /// <summary>以識別碼批次取得可見的專案；不存在或看不到的不會出現在結果中。</summary>
    Task<IReadOnlyDictionary<Guid, MeetingProjectInfo>> GetManyAsync(Guid userId, bool viewAll, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}

/// <summary>未載入 SGS.Modules.PMS 時的預設實作：沒有任何可關聯的專案。</summary>
public sealed class NullMeetingProjectLookup : IMeetingProjectLookup
{
    public bool IsAvailable => false;

    public Task<IReadOnlyList<MeetingProjectInfo>> SearchAsync(Guid userId, bool viewAll, string? keyword, int max, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MeetingProjectInfo>>([]);

    public Task<IReadOnlyDictionary<Guid, MeetingProjectInfo>> GetManyAsync(Guid userId, bool viewAll, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, MeetingProjectInfo>>(new Dictionary<Guid, MeetingProjectInfo>());
}
