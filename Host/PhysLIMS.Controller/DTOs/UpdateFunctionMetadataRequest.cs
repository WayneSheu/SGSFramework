using System.ComponentModel.DataAnnotations;

namespace SGSFramework.ApiInfrastructure.DTOs
{
    /// <summary>
    /// 編輯系統功能顯示欄位之請求（名稱 / 說明 / 排序 / 圖示）
    /// </summary>
    public record UpdateFunctionMetadataRequest
    {
        /// <summary>功能名稱</summary>
        [Required]
        [MaxLength(100)]
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>功能說明</summary>
        [MaxLength(500)]
        public string? Description { get; init; }

        /// <summary>顯示排序</summary>
        [Range(0, 9999)]
        public int DisplayOrder { get; init; }

        /// <summary>圖示 (Font Awesome class，例如 fa-solid fa-list)</summary>
        [MaxLength(100)]
        public string? Icon { get; init; }

        /// <summary>是否為選單入口</summary>
        public bool IsMenu { get; init; }
    }
}
