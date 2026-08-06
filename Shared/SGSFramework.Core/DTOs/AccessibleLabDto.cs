using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.DTOs
{
    /// <summary>
    /// 使用者可存取/巡檢之實驗室/組織節點 DTO
    /// 用於支援多 Root 組織樹結構與高階主管無感切換體驗
    /// </summary>
    public class AccessibleLabDto
    {
        /// <summary>
        /// 實驗室/組織節點唯一識別碼
        /// </summary>
        public Guid LabId { get; set; }

        /// <summary>
        /// 實驗室/組織節點名稱 (例如: "台北食品實驗室", "台中檢驗組")
        /// </summary>
        public string LabName { get; set; } = string.Empty;

        /// <summary>
        /// MSSQL HierarchyId 路徑字串 (例如: "/1/2/" 代表第 1 棵 Root 下的第 2 個子節點)
        /// 方便前端評估節點間的親緣關係與排序
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// 組織節點層級深度 (由 HierarchyId.GetLevel() 計算獲得)
        /// 0 = 總公司/虛擬根, 1 = 獨立實驗室 Root, 2 = 地區/組別
        /// </summary>
        public int HierarchyLevel { get; set; }

        /// <summary>
        /// 標記當前使用者是否以此實驗室為預設主要管轄單位
        /// </summary>
        public bool IsPrimary { get; set; }
    }


}
