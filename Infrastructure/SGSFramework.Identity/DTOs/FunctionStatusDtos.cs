using System;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 變更系統功能狀態之指令內容（由 Controller 組成，含操作來源資訊）
    /// </summary>
    /// <param name="IsActive">目標狀態</param>
    /// <param name="Reason">內部變更原因（停用時必填）</param>
    /// <param name="Notice">對使用者公告內容（停用時顯示）</param>
    /// <param name="ExpectedRestoreAt">預計恢復時間（僅供公告）</param>
    /// <param name="ClientIp">操作來源 IP</param>
    public sealed record FunctionStatusChange(
        bool IsActive,
        string? Reason,
        string? Notice,
        DateTimeOffset? ExpectedRestoreAt,
        string? ClientIp);

    /// <summary>
    /// 系統功能狀態異動歷程
    /// </summary>
    public sealed class FunctionStatusLogDto
    {
        public Guid Id { get; init; }
        public bool IsActive { get; init; }
        public string? Reason { get; init; }
        public string? Notice { get; init; }
        public DateTimeOffset? ExpectedRestoreAt { get; init; }
        public string? ActorName { get; init; }
        public string? ClientIp { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    /// <summary>
    /// 對全體使用者的停用功能公告
    /// </summary>
    public sealed class FunctionNoticeDto
    {
        public Guid Id { get; init; }
        public string ModuleTitle { get; init; } = string.Empty;
        public string ControllerTitle { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Notice { get; init; } = string.Empty;
        public DateTimeOffset? DisabledAt { get; init; }
        public DateTimeOffset? ExpectedRestoreAt { get; init; }
    }
}
