using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Dtos
{
    /// <summary>
    /// 實驗室樹狀階層結構資料傳輸物件 (DTO)
    /// </summary>
    public record LaboratoryTreeDto
    {
        /// <summary>
        /// 實驗室唯一識別碼 (Primary Key)
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// 實驗室代碼
        /// </summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// 實驗室顯示名稱
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 實驗室描述/備註
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// 父階層實驗室識別碼 (若為根節點則為 null)
        /// </summary>
        public int? ParentId { get; init; }

        /// <summary>
        /// 樹狀節點排序權重 (數字越小越靠前)
        /// </summary>
        public int SortOrder { get; init; } = 100;

        /// <summary>
        /// 是否啟用
        /// </summary>
        public bool IsActive { get; init; } = true;

        /// <summary>
        /// 階層路徑 (如: /1/4/12/)，用於高效能樹狀搜尋與權限比對
        /// </summary>
        public string? NodePath { get; init; }

        /// <summary>
        /// 當前節點所屬的樹狀深度階層 (Root 為 0 或 1)
        /// </summary>
        public int Depth { get; init; }

        /// <summary>
        /// 建立時間 (UTC)
        /// </summary>
        public DateTime CreatedAtUtc { get; init; }

        /// <summary>
        /// 直屬下階層實驗室節點清單 (子樹)
        /// </summary>
        [JsonPropertyName("children")]
        public List<LaboratoryTreeDto> Children { get; init; } = new();

    }
}
