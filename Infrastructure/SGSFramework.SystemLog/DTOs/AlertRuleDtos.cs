namespace SGSFramework.SystemLog.DTOs
{
    /// <summary>告警規則（含建立 / 修改資訊）</summary>
    public record AlertRuleDto(
        Guid Id,
        string Name,
        string? Description,
        bool IsEnabled,
        int Priority,
        string LogType,
        string MinLevel,
        string? MessageContains,
        string? ModuleContains,
        string? OperationContains,
        string? ExceptionContains,
        string Action,
        int? CooldownSeconds,
        string? Recipients,
        string CreatedBy,
        DateTimeOffset CreatedAtUtc,
        string? UpdatedBy,
        DateTimeOffset? UpdatedAtUtc);

    /// <summary>新增 / 修改告警規則的內容</summary>
    public record AlertRuleSaveRequest
    {
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public bool IsEnabled { get; init; } = true;
        public int Priority { get; init; } = 100;
        public string LogType { get; init; } = "Any";
        public string MinLevel { get; init; } = "Error";
        public string? MessageContains { get; init; }
        public string? ModuleContains { get; init; }
        public string? OperationContains { get; init; }
        public string? ExceptionContains { get; init; }
        public string Action { get; init; } = "Alert";
        public int? CooldownSeconds { get; init; }
        public string? Recipients { get; init; }
    }

    /// <summary>規則測試：模擬一筆日誌，查看會命中哪條規則</summary>
    public record AlertRuleTestRequest
    {
        /// <summary>System 或 Security</summary>
        public string LogType { get; init; } = "System";
        public string Level { get; init; } = "Error";
        public string? Message { get; init; }
        public string? Module { get; init; }
        public string? Operation { get; init; }
        public string? ExceptionType { get; init; }
    }

    public record AlertRuleTestResult(bool WillAlert, string Outcome, Guid? RuleId, string? RuleName, string? Recipients, int? CooldownSeconds);
}
