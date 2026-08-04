namespace SGSFramework.Core.Abstractions.Logings
{
    /// <summary>
    /// 通用的日誌分錄模型，用於在不同層次間傳遞日誌數據
    /// </summary>
    public record LogEntry
    {
        public DateTime TimeStamp { get; set; }
        public string Level { get; init; } = "Information";
        public string Message { get; init; } = string.Empty;
        public string? Exception { get; init; }

        // 對齊 appsettings.Production.json 的額外欄位
        public string? Module { get; init; }
        public string? TenantId { get; init; }
        public string? CorrelationId { get; init; }
        public string? Roles { get; init; }
        public string? UserId { get; init; }
        public string? UserName { get; init; }

        // 可擴充的字典，用於處理其他動態屬性
        public Dictionary<string, object>? Properties { get; init; }
    }
}
