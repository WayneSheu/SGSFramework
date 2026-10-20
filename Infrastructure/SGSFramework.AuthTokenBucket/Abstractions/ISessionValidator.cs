namespace SGSFramework.AuthTokenBucket.Abstractions;

/// <summary>
/// 驗證 Access Token 所屬的裝置工作階段是否仍有效。
/// JWT 為無狀態憑證，若不在每次請求檢查工作階段，撤銷 Refresh Token 後
/// 其他裝置仍可使用尚未過期的 Access Token 繼續存取（最長可達 Access Token 效期）。
/// </summary>
public interface ISessionValidator
{
    /// <summary>檢查 (UserId, DeviceId) 是否存在未撤銷的工作階段</summary>
    Task<bool> IsSessionActiveAsync(string userId, string deviceId, CancellationToken cancellationToken = default);

    /// <summary>清除單一裝置的驗證快取，使撤銷立即生效</summary>
    void InvalidateDevice(string userId, string deviceId);

    /// <summary>清除該使用者所有裝置的驗證快取，使全裝置撤銷立即生效</summary>
    void InvalidateUser(string userId);
}
