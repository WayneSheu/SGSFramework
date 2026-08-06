using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    /// <summary>
    /// 使用者執行期範疇與動態權限運算服務
    /// </summary>
    public interface IUserRuntimeScopeService
    {
        /// <summary>
        /// 無感切換實驗室上下文：驗證權限並回傳該實驗室的 Bitmask 權限與動態 Menu
        /// </summary>
        /// <param name="userId">使用者識別碼</param>
        /// <param name="targetLabId">欲切換到的目標實驗室 Guid</param>
        /// <param name="cancellationToken">取消權杖</param>
        Task<UserPermissionProfileDto?> SwitchLaboratoryAsync(
            string userId,
            Guid targetLabId,
            CancellationToken cancellationToken = default);


        /// <summary>
        /// 後端 API 執行期驗證：檢查使用者在指定實驗室下，是否具備特定模組的指定位元權限
        /// </summary>
        /// <param name="userId">使用者識別碼</param>
        /// <param name="activeLabId">當前作用中的實驗室 Guid (來自 X-Active-Lab-Id)</param>
        /// <param name="module">系統模組名稱 (如 "ReportManagement")</param>
        /// <param name="bitPosition">權限位元位置 (0~63)</param>
        /// <param name="cancellationToken">取消權杖</param>
        Task<bool> ValidateRuntimePermissionAsync(
            string userId,
            Guid activeLabId,
            string module,
            int bitPosition,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 獲取使用者當前具備管轄權的所有實驗室清單 (供前端下拉選單渲染)
        /// </summary>
        Task<List<AccessibleLabDto>> GetAccessibleLabsAsync(
            string userId,
            CancellationToken cancellationToken = default);
    }
}
