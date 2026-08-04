using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    /// <summary>
    /// 使用者執行期權限檔案與上下文 DTO
    /// 當使用者切換實驗室或初始化 Session 時，由後端回傳給前端 Vue 3 Pinia Store 進行狀態更新
    /// </summary>
    public class UserPermissionProfileDto
    {
        /// <summary>
        /// 使用者唯一識別碼 (對應 ASP.NET Core Identity 或 JWT Claim 中的 NameIdentifier)
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 使用者顯示名稱
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// 當前作用中（已選擇）的實驗室/組織節點識別碼
        /// 前端後續所有 API 請求均需將此 Guid 附帶於 Header `X-Active-Lab-Id` 中
        /// </summary>
        public Guid ActiveLabId { get; set; }

        /// <summary>
        /// 當前作用中的實驗室/組織節點名稱
        /// </summary>
        public string ActiveLabName { get; set; } = string.Empty;

        /// <summary>
        /// 模組位元遮罩權限字典 (Key: 模組/Controller 名稱, Value: 64 位元權限長整數遮罩)
        /// 前端 v-perm 指令與按鈕會透過 (Value & (1 << BitPosition)) != 0 進行快速位元運算
        /// </summary>
        public Dictionary<string, long> ModulePermissions { get; set; } = new();

        /// <summary>
        /// 該使用者具備管轄/巡檢權限的所有實驗室清單
        /// 供前端高階主管下拉選單 (LabSwitcher.vue) 渲染使用
        /// </summary>
        public List<AccessibleLabDto> AccessibleLabs { get; set; } = new();

        /// <summary>
        /// 根據當前 ActiveLabId 與 Bitmask 動態過濾後的 Vue 3 側邊欄選單樹狀結構
        /// </summary>
        public List<MenuNodeDto> DynamicMenus { get; set; } = new();
    }
}
