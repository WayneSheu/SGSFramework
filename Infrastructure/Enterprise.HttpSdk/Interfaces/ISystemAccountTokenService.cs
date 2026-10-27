namespace Enterprise.HttpSdk.Interfaces
{
    /// <summary>
    /// 系統帳號權杖服務：以 Client Credentials 換發 JWT 並快取於記憶體，到期前自動換新。
    /// </summary>
    public interface ISystemAccountTokenService
    {
        /// <summary>
        /// 取得指定系統帳號設定（具名 Options）的 Access Token
        /// </summary>
        /// <param name="accountName">AddSystemAccountHttpClient 註冊時的帳號設定名稱</param>
        /// <param name="forceRefresh">true 時忽略快取重新換發（收到 401 時使用）</param>
        Task<string> GetSystemTokenAsync(string accountName, bool forceRefresh, CancellationToken cancellationToken = default);
    }
}
