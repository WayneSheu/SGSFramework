using System.ComponentModel.DataAnnotations;

namespace SGSFramework.ApiInfrastructure.DTOs
{
    /// <summary>
    /// 編輯模組層級顯示欄位之請求；欄位為 null 時還原為程式碼預設
    /// </summary>
    public record UpdateModuleGroupRequest
    {
        /// <summary>模組顯示名稱</summary>
        [MaxLength(100)]
        public string? Title { get; init; }

        /// <summary>顯示排序</summary>
        [Range(0, 9999)]
        public int? DisplayOrder { get; init; }
    }

    /// <summary>
    /// 編輯 Controller 層級顯示欄位之請求；欄位為 null 時還原為程式碼預設
    /// </summary>
    public record UpdateControllerGroupRequest
    {
        /// <summary>Controller 顯示名稱</summary>
        [MaxLength(100)]
        public string? Title { get; init; }

        /// <summary>圖示 (Font Awesome class)</summary>
        [MaxLength(100)]
        public string? Icon { get; init; }

        /// <summary>顯示排序</summary>
        [Range(0, 9999)]
        public int? DisplayOrder { get; init; }

        /// <summary>改掛顯示於哪個模組之下；為 null 時顯示於原模組</summary>
        [MaxLength(50)]
        public string? TargetModuleName { get; init; }
    }

    /// <summary>
    /// 功能顯示用改掛之請求；TargetControllerName 為 null 時還原顯示於原 Controller
    /// </summary>
    public record UpdateFunctionGroupRequest
    {
        /// <summary>目標 Controller 所屬模組</summary>
        [MaxLength(50)]
        public string? TargetModuleName { get; init; }

        /// <summary>目標 Controller 名稱</summary>
        [MaxLength(100)]
        public string? TargetControllerName { get; init; }
    }
}
