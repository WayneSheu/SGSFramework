using SGSFramework.Core.Paginations;

namespace SGSFramework.SystemLog.DTOs
{
    /// <summary>
    /// 資料日誌（core.AuditLogs）篩選條件
    /// </summary>
    public record AuditLogFilter
    {
        /// <summary>資料日誌來源（AuditLogSourceDto.Key，例如 PhysLIMSDbContext / ORGDbContext）；空值為主系統</summary>
        public string? Source { get; init; }

        /// <summary>
        /// 資料日誌來源（可多選）：外掛模組可動態載入，查閱時可同時勾選多個來源。
        /// 有值時優先於 <see cref="Source"/>；多來源時依時間排序合併，排序僅支援 Timestamp。
        /// </summary>
        public IReadOnlyList<string>? Sources { get; init; }

        /// <summary>資料表名稱（包含比對）</summary>
        public string? TableName { get; init; }

        /// <summary>操作類型（Added / Modified / Deleted / SoftDeleted / Restored，精確比對）</summary>
        public string? Action { get; init; }

        /// <summary>操作者 UserId（精確比對）</summary>
        public string? UserId { get; init; }

        /// <summary>操作者人員姓名（包含比對 FullName / 帳號；操作者欄位記錄的是登入帳號，故先換算成帳號再比對）</summary>
        public string? UserName { get; init; }

        /// <summary>全鏈路追蹤碼（前綴比對，可略去 '-'）</summary>
        public string? TraceId { get; init; }

        /// <summary>主鍵值（包含比對，例如貼上某筆資料的 Id）</summary>
        public string? KeyValue { get; init; }

        /// <summary>變更欄位名稱（包含比對，例如 Status）</summary>
        public string? ChangedColumn { get; init; }

        public DateTimeOffset? StartTimestamp { get; init; }
        public DateTimeOffset? EndTimestamp { get; init; }
    }

    public record AuditLogQueryRequest : PagedQueryParameters<AuditLogFilter>;

    /// <summary>資料日誌來源（主系統或外掛模組各自的 AuditLogs 資料表）</summary>
    public record AuditLogSourceDto(string Key, string DisplayName, string Schema, bool IsMain);

    /// <summary>
    /// 資料日誌清單項目（不含新 / 舊值，避免清單傳輸大量 JSON；明細另以 Id 取得）
    /// </summary>
    public record AuditLogListItemDto(
        long Id,
        DateTimeOffset Timestamp,
        string? Schema,
        string TableName,
        string Action,
        string? KeyValues,
        string? ChangedColumns,
        string? UserId,
        string? RemoteIp,
        string TraceId,
        bool IsRepaired,
        string? UserName = null,
        string? Source = null);

    /// <summary>
    /// 資料日誌完整明細（含新 / 舊值與防篡改鏈路資訊）
    /// </summary>
    public record AuditLogDetailDto(
        long Id,
        DateTimeOffset Timestamp,
        DateTimeOffset CreatedAt,
        string? Schema,
        string TableName,
        string Action,
        string? KeyValues,
        string? OldValues,
        string? NewValues,
        string? ChangedColumns,
        string? UserId,
        string? RemoteIp,
        string TraceId,
        string PreviousHash,
        string StoredHash,
        bool IsRepaired,
        DateTimeOffset? RepairedAt,
        string? GapReason,
        string? UserName = null);
}
