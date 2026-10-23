using SGSFramework.Identity.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.Identity.Abstractions
{
    /// <summary>
    /// 系統配置管理服務（外觀識別、Identity 密碼與鎖定規則）
    /// </summary>
    public interface ISystemSettingsService
    {
        /// <summary>取得完整系統配置（含 Identity 預設值）</summary>
        Task<SystemSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);

        /// <summary>取得外觀識別設定（快取，供登入頁與 Layout 使用）</summary>
        Task<BrandingSettingsDto> GetBrandingAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新外觀識別設定
        /// </summary>
        /// <exception cref="System.ArgumentException">Logo 格式或大小不符規定</exception>
        Task<BrandingSettingsDto> UpdateBrandingAsync(BrandingSettingsDto branding, string? clientIp, CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新 Identity 密碼與鎖定規則，儲存後立即生效
        /// </summary>
        /// <exception cref="System.ArgumentException">規則組合無效</exception>
        Task<IdentityPolicySettingsDto> UpdateIdentityPolicyAsync(IdentityPolicySettingsDto policy, string? clientIp, CancellationToken cancellationToken = default);

        /// <summary>取得系統配置異動歷程（新到舊）</summary>
        Task<IReadOnlyList<SystemSettingChangeLogDto>> GetChangeLogsAsync(int take, CancellationToken cancellationToken = default);
    }
}
