using System.Net;
using System.Net.Sockets;
using SGS.Modules.MAN.Application.Contracts;

namespace SGS.Modules.MAN.Application.Rules;

/// <summary>AI 生成相關的驗證規則（純函式）：Ollama 位址白名單、截圖檔案驗證、作業請求驗證。</summary>
public static class GenerationRules
{
    public const int MaxKeywordLength = 50;

    /// <summary>
    /// Ollama 位址只允許本機或內網：http/https、loopback、私有網段（10/172.16-31/192.168、link-local、IPv6 ULA）
    /// 或單一標籤主機名（如 ollama-host）。其他一律拒絕，避免截圖與規格被送到外部主機。
    /// </summary>
    public static bool IsAllowedOllamaUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.IsNullOrEmpty(uri.UserInfo)) return false;

        var host = uri.Host;
        if (uri.HostNameType == UriHostNameType.Dns)
        {
            if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
            // 單一標籤（不含句點）視為內網主機名；含句點的網域名稱無法證明在內網，一律拒絕
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
            return ip.IsIPv6LinkLocal || (bytes[0] & 0xFE) == 0xFC; // fe80::/10、fc00::/7
        }

        var b = ip.GetAddressBytes();
        return b[0] == 10
               || (b[0] == 172 && b[1] is >= 16 and <= 31)
               || (b[0] == 192 && b[1] == 168)
               || (b[0] == 169 && b[1] == 254);
    }

    /// <summary>辨識截圖格式（以檔案標頭判斷，不信任副檔名或 Content-Type）；不支援時回傳 null。</summary>
    public static string? DetectImageType(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return "image/png";
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "image/jpeg";
        if (data.Length >= 12 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F'
            && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P') return "image/webp";
        return null;
    }

    /// <summary>驗證截圖；回傳 null 代表通過。</summary>
    public static string? ValidateImage(ReadOnlySpan<byte> data, int maxBytes)
    {
        if (data.Length == 0) return "請上傳有效的截圖檔案。";
        if (data.Length > maxBytes) return $"截圖大小不得超過 {maxBytes / 1024 / 1024} MB。";
        return DetectImageType(data) is null ? "僅支援 PNG、JPEG 或 WebP 截圖。" : null;
    }

    /// <summary>驗證生成作業的文字欄位；回傳 null 代表通過。</summary>
    public static string? ValidateRequest(string? featureKey, string? title, string? apiKeyword)
    {
        if (!ManualTopicRules.IsValidFeatureKey(featureKey?.Trim()))
            return "功能代碼僅限小寫英數字、底線、連字號與句點，須以英數字開頭，且不得超過 100 字。";
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > ManualTopicRules.MaxTitleLength)
            return $"標題為必填且不得超過 {ManualTopicRules.MaxTitleLength} 字。";
        if (apiKeyword is { Length: > MaxKeywordLength })
            return $"API 關鍵字不得超過 {MaxKeywordLength} 字。";
        return null;
    }

    /// <summary>將生成結果寫成人工可讀的錯誤摘要（避免把模型原始輸出或堆疊直接顯示給使用者）。</summary>
    public static string SafeMessage(string message, int maxLength = 500) =>
        string.IsNullOrEmpty(message) ? string.Empty : message.Length <= maxLength ? message : message[..maxLength] + "…";
}
