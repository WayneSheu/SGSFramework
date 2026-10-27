namespace Enterprise.HttpSdk.Options
{
    /// <summary>
    /// 系統帳號（M2M）用戶端設定。ClientSecret 嚴禁寫死於程式或 appsettings，
    /// 請以 K8s Secret / Key Vault 注入環境變數，例如 SystemAccount__Hangfire__ClientSecret。
    /// </summary>
    public sealed class SystemAccountClientOptions
    {
        /// <summary>權杖端點，例如 http://physlims-api.default.svc.cluster.local/api/v1/system-accounts/token</summary>
        public Uri? TokenEndpoint { get; set; }

        public string ClientId { get; set; } = string.Empty;

        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>要求的 Scope（空白分隔）；空白表示核發帳號被授予的全部 Scope</summary>
        public string? Scope { get; set; }

        /// <summary>權杖到期前提前換發的緩衝時間，避免請求途中過期</summary>
        public TimeSpan RefreshBeforeExpiry { get; set; } = TimeSpan.FromSeconds(60);
    }
}
