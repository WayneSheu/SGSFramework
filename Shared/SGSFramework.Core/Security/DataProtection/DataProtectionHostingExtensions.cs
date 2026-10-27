using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography.X509Certificates;

namespace SGSFramework.Core.Security.DataProtection
{
    /// <summary>
    /// 依執行環境 (地端 IIS / 地端多台 / 雲端 VM / 容器) 選擇 Data Protection 金鑰環的保護策略。
    /// 金鑰環同時保護登入 Cookie 與 [Encrypted] 個資欄位，因此正式環境一律 Fail-Closed：
    /// 無法確保金鑰「加密存放」與「持久保存」時拒絕啟動，而不是默默以明文或暫存磁碟運作。
    /// </summary>
    public static class DataProtectionHostingExtensions
    {
        /// <summary>啟動時選定的策略摘要 (不含任何機密，可直接寫入日誌)</summary>
        public sealed record DataProtectionSetup(KeyProtectionMode Mode, string KeysPath, string Reason);

        public static DataProtectionSetup AddHostedDataProtection(
            this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(environment);

            var options = configuration.GetSection(DataProtectionHostingOptions.SectionName)
                              .Get<DataProtectionHostingOptions>() ?? new DataProtectionHostingOptions();

            var isContainer = IsRunningInContainer();
            var (mode, reason) = ResolveMode(options, environment, isContainer);

            // 容器檔案系統為暫存：未指定持久化路徑，重新部署即遺失金鑰 (= 加密個資無法解密)
            if (isContainer && string.IsNullOrWhiteSpace(options.KeysPath) && !environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "容器環境須以 DataProtection__KeysPath 指定持久化磁碟 (Azure Files / EFS / PVC)，否則重新部署將遺失金鑰。");
            }

            var keysPath = string.IsNullOrWhiteSpace(options.KeysPath)
                ? Path.Combine(environment.ContentRootPath, "keys")
                : options.KeysPath;
            Directory.CreateDirectory(keysPath);

            var builder = services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
                .SetApplicationName(options.ApplicationName);

            switch (mode)
            {
                case KeyProtectionMode.Dpapi:
                    if (!OperatingSystem.IsWindows())
                        throw new PlatformNotSupportedException("DataProtection:Mode=Dpapi 僅支援 Windows，非 Windows 主機請改用 Certificate。");
                    builder.ProtectKeysWithDpapi(protectToLocalMachine: true);
                    break;

                case KeyProtectionMode.Certificate:
                    var certificate = LoadCertificate(options.Certificate);
                    builder.ProtectKeysWithCertificate(certificate);

                    // 憑證輪替：新金鑰以新憑證加密，舊金鑰仍可用舊憑證解密
                    var previous = options.Certificate.PreviousThumbprints
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Select(t => LoadFromStore(t, options.Certificate.StoreLocation))
                        .ToArray();
                    builder.UnprotectKeysWithAnyCertificate([certificate, .. previous]);
                    break;

                case KeyProtectionMode.None:
                    // 僅開發環境會走到這裡 (ResolveMode 已擋下正式環境)
                    break;
            }

            return new DataProtectionSetup(mode, keysPath, reason);
        }

        /// <summary>
        /// 選擇保護方式 (internal static 以便單元測試，不依賴實際主機環境)
        /// </summary>
        internal static (KeyProtectionMode Mode, string Reason) ResolveMode(
            DataProtectionHostingOptions options, IHostEnvironment environment, bool isContainer)
        {
            var mode = options.Mode;
            string reason;

            if (mode == KeyProtectionMode.Auto)
            {
                if (options.Certificate.IsConfigured)
                {
                    (mode, reason) = (KeyProtectionMode.Certificate, "已設定憑證 (多台 / 災備 / 雲端)");
                }
                else if (OperatingSystem.IsWindows() && !isContainer)
                {
                    (mode, reason) = (KeyProtectionMode.Dpapi, "Windows 地端主機");
                }
                else if (environment.IsDevelopment())
                {
                    (mode, reason) = (KeyProtectionMode.None, "非 Windows 開發環境 (金鑰未加密)");
                }
                else
                {
                    throw new InvalidOperationException(
                        "非 Windows 或容器環境無法使用 DPAPI，請設定 DataProtection__Certificate__PfxPath (+ PfxPassword) 或 Thumbprint 以加密金鑰環。");
                }
            }
            else
            {
                reason = "組態明確指定";
            }

            if (mode == KeyProtectionMode.Certificate && !options.Certificate.IsConfigured)
                throw new InvalidOperationException("DataProtection:Mode=Certificate 但未設定 Certificate:Thumbprint 或 Certificate:PfxPath。");

            if (mode == KeyProtectionMode.None && !environment.IsDevelopment())
                throw new InvalidOperationException("DataProtection:Mode=None (金鑰明文存放) 僅允許於開發環境使用。");

            return (mode, reason);
        }

        private static X509Certificate2 LoadCertificate(DataProtectionHostingOptions.CertificateOptions options)
        {
            var certificate = !string.IsNullOrWhiteSpace(options.PfxPath)
                ? X509CertificateLoader.LoadPkcs12FromFile(options.PfxPath, options.PfxPassword)
                : LoadFromStore(options.Thumbprint!, options.StoreLocation);

            if (!certificate.HasPrivateKey)
                throw new InvalidOperationException("Data Protection 憑證缺少私密金鑰，無法解密金鑰環。");

            return certificate;
        }

        private static X509Certificate2 LoadFromStore(string thumbprint, string storeLocation)
        {
            var location = Enum.Parse<StoreLocation>(storeLocation, ignoreCase: true);
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly);

            var normalized = thumbprint.Replace(" ", string.Empty, StringComparison.Ordinal);
            var found = store.Certificates.Find(X509FindType.FindByThumbprint, normalized, validOnly: false);

            return found.Count > 0
                ? found[0]
                : throw new InvalidOperationException($"憑證存放區 {location}\\My 找不到指紋為 {normalized} 的憑證。");
        }

        /// <summary>官方 .NET 容器映像皆設定 DOTNET_RUNNING_IN_CONTAINER=true</summary>
        public static bool IsRunningInContainer() =>
            string.Equals(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase);
    }
}
