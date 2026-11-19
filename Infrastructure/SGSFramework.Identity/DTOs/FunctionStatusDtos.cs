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
    /// 編輯系統功能顯示欄位之指令內容
    /// </summary>
    /// <param name="DisplayName">功能名稱（必填）</param>
    /// <param name="Description">功能說明</param>
    /// <param name="DisplayOrder">顯示排序</param>
    /// <param name="Icon">圖示（Font Awesome class）</param>
    /// <param name="IsMenu">是否為選單入口</param>
    /// <param name="ClientIp">操作來源 IP</param>
    public sealed record FunctionMetadataChange(
        string DisplayName,
        string? Description,
        int DisplayOrder,
        string? Icon,
        bool IsMenu,
        string? ClientIp);

    /// <summary>
    /// 編輯模組層級顯示欄位（名稱 / 排序）；欄位為 null 時還原為程式碼預設
    /// </summary>
    public sealed record ModuleGroupChange(string? Title, int? DisplayOrder, string? ClientIp);

    /// <summary>
    /// 編輯 Controller 層級顯示欄位（名稱 / 圖示 / 排序 / 顯示用父模組）；欄位為 null 時還原為程式碼預設
    /// </summary>
    public sealed record ControllerGroupChange(string? Title, string? Icon, int? DisplayOrder, string? TargetModuleName, string? ClientIp);

    /// <summary>
    /// 功能顯示用改掛：TargetControllerName 為 null 時還原顯示於原 Controller
    /// </summary>
    public sealed record FunctionGroupChange(string? TargetModuleName, string? TargetControllerName, string? ClientIp);

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
