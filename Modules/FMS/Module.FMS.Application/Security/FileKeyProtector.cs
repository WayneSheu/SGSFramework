using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace SGS.Modules.FMS.Application.Security;

/// <summary>
/// 信封加密的金鑰包裝：每個檔案一把隨機 256 位元資料金鑰（DEK），以 ASP.NET Data Protection 主金鑰環包裝後存入中繼資料。
/// 主金鑰環由 Host 的 AddHostedDataProtection 管理（DPAPI / 憑證保護、自動輪替；舊主金鑰仍可解開既有 DEK）。
/// 金鑰環遺失即無法解密檔案，多節點部署須共用並持久保存金鑰環。
/// </summary>
public interface IFileKeyProtector
{
    /// <summary>產生新的資料金鑰並回傳（明文金鑰, 包裝後字串）</summary>
    (byte[] Key, string WrappedKey) CreateKey();

    /// <summary>解開包裝；主金鑰遭撤銷或金鑰環遺失時拋出 <see cref="CryptographicException"/></summary>
    byte[] Unwrap(string wrappedKey);
}

public sealed class FileKeyProtector(IDataProtectionProvider dataProtectionProvider) : IFileKeyProtector
{
    /// <summary>包裝格式前綴（版本化，未來更換包裝機制時可並存）</summary>
    private const string Prefix = "fk:v1:";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("SGS.Modules.FMS.FileKey.v1");

    public (byte[] Key, string WrappedKey) CreateKey()
    {
        var key = RandomNumberGenerator.GetBytes(FileCipherFormat.KeySize);
        return (key, Prefix + Convert.ToBase64String(_protector.Protect(key)));
    }

    public byte[] Unwrap(string wrappedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wrappedKey);
        if (!wrappedKey.StartsWith(Prefix, StringComparison.Ordinal))
            throw new CryptographicException("不支援的資料金鑰包裝格式。");

        var key = _protector.Unprotect(Convert.FromBase64String(wrappedKey[Prefix.Length..]));
        return key.Length == FileCipherFormat.KeySize ? key : throw new CryptographicException("資料金鑰長度不合法。");
    }
}
