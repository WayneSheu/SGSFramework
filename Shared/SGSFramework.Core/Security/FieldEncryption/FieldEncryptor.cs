using Microsoft.AspNetCore.DataProtection;

namespace SGSFramework.Core.Security.FieldEncryption
{
    /// <summary>
    /// 欄位層級加解密 (供 EF Core ValueConverter 使用)。
    /// EF Core 的模型 (含 ValueConverter) 以 DbContext 型別為單位快取，無法逐次注入服務，
    /// 因此於應用程式啟動時以 <see cref="Initialize"/> 綁定 DI 中的 IDataProtectionProvider。
    /// </summary>
    public static class FieldEncryptor
    {
        /// <summary>密文前綴 (含版本)，用以區分既有明文資料與未來金鑰演算法升級</summary>
        public const string CipherPrefix = "enc:v1:";

        private const string Purpose = "SGSFramework.FieldEncryption.v1";

        private static IDataProtector? _protector;

        /// <summary>是否已完成初始化</summary>
        public static bool IsInitialized => _protector is not null;

        public static void Initialize(IDataProtectionProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            _protector = provider.CreateProtector(Purpose);
        }

        /// <summary>
        /// 加密。未初始化時直接拋出例外 (Fail-Closed)，絕不以明文寫入資料庫。
        /// </summary>
        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText) || plainText.StartsWith(CipherPrefix, StringComparison.Ordinal))
                return plainText;

            return CipherPrefix + GetProtector().Protect(plainText);
        }

        /// <summary>
        /// 解密。無前綴的值視為導入加密前的既有明文，原樣回傳 (下次儲存時即自動加密)。
        /// </summary>
        public static string Unprotect(string storedValue)
        {
            if (string.IsNullOrEmpty(storedValue) || !storedValue.StartsWith(CipherPrefix, StringComparison.Ordinal))
                return storedValue;

            return GetProtector().Unprotect(storedValue[CipherPrefix.Length..]);
        }

        private static IDataProtector GetProtector() =>
            _protector ?? throw new InvalidOperationException(
                "FieldEncryptor 尚未初始化，請於應用程式啟動時呼叫 app.UseFieldEncryption()。");
    }
}
