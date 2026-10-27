namespace SGSFramework.Core.Security.DataProtection
{
    /// <summary>
    /// Data Protection 金鑰環保護方式
    /// </summary>
    public enum KeyProtectionMode
    {
        /// <summary>依執行環境自動判斷 (有憑證 → Certificate；Windows 非容器 → Dpapi；其餘 → 正式環境拒絕啟動)</summary>
        Auto,

        /// <summary>Windows DPAPI (LocalMachine)：地端單一主機，金鑰只能在原主機解密</summary>
        Dpapi,

        /// <summary>X.509 憑證：地端多台 / 災備、雲端 VM、容器 (Linux 亦可)，持有憑證的主機皆可解密</summary>
        Certificate,

        /// <summary>不加密金鑰檔：僅限開發環境，正式環境一律拒絕</summary>
        None
    }

    /// <summary>
    /// Data Protection 託管組態 (對應 appsettings 的 DataProtection 區段)。
    /// 機密 (PfxPassword) 須由環境變數 DataProtection__Certificate__PfxPassword 或 claudesettings.json 注入。
    /// </summary>
    public sealed class DataProtectionHostingOptions
    {
        public const string SectionName = "DataProtection";

        public KeyProtectionMode Mode { get; set; } = KeyProtectionMode.Auto;

        /// <summary>金鑰環路徑；未設定時為 ContentRoot/keys。容器 / 雲端須指向持久化磁碟 (Azure Files、EFS、PVC)</summary>
        public string? KeysPath { get; set; }

        /// <summary>同一應用程式的所有執行個體須相同，才能共用金鑰環</summary>
        public string ApplicationName { get; set; } = "PhysLIMS.Enterprise";

        public CertificateOptions Certificate { get; set; } = new();

        public sealed class CertificateOptions
        {
            /// <summary>Windows 憑證存放區中的憑證指紋 (地端 / Azure App Service Windows)</summary>
            public string? Thumbprint { get; set; }

            /// <summary>LocalMachine 或 CurrentUser (Azure App Service 載入的憑證位於 CurrentUser)</summary>
            public string StoreLocation { get; set; } = "LocalMachine";

            /// <summary>PFX 檔路徑 (容器以 Secret / Volume 掛載)</summary>
            public string? PfxPath { get; set; }

            /// <summary>PFX 密碼 (機密，勿寫入 appsettings)</summary>
            public string? PfxPassword { get; set; }

            /// <summary>憑證輪替時，仍需能解密舊金鑰的舊憑證指紋</summary>
            public List<string> PreviousThumbprints { get; set; } = new();

            public bool IsConfigured =>
                !string.IsNullOrWhiteSpace(Thumbprint) || !string.IsNullOrWhiteSpace(PfxPath);
        }
    }
}
