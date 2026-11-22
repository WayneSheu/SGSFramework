using System.Net;
using System.Net.Sockets;

namespace SGS.Modules.MTG.Application.Rules;

/// <summary>
/// 內網位址白名單（純函式），套用於 Ollama 與語音轉錄服務。邏輯與 MAN 模組的 GenerationRules.IsAllowedOllamaUrl 相同；
/// 外掛模組各自獨立載入，無法互相參考，故在此保留一份（兩處規則須同步維護）。
/// </summary>
public static class InternalUrlRules
{
    /// <summary>只允許本機或內網：http/https、loopback、私有網段或單一標籤主機名，且不得含帳密。</summary>
    public static bool IsAllowed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.IsNullOrEmpty(uri.UserInfo)) return false;

        var host = uri.Host;
        if (uri.HostNameType == UriHostNameType.Dns)
        {
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
            // 含句點的網域名稱無法證明在內網，一律拒絕
            return !host.Contains('.');
        }

        return IPAddress.TryParse(host, out var ip) && IsPrivateOrLoopback(ip);
    }

    private static bool IsPrivateOrLoopback(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv4MappedToIPv6) return IsPrivateOrLoopback(ip.MapToIPv4());
            var bytes = ip.GetAddressBytes();
            return ip.IsIPv6LinkLocal || (bytes[0] & 0xFE) == 0xFC;
        }

        var b = ip.GetAddressBytes();
        return b[0] == 10
               || (b[0] == 172 && b[1] is >= 16 and <= 31)
               || (b[0] == 192 && b[1] == 168)
               || (b[0] == 169 && b[1] == 254);
    }
}
