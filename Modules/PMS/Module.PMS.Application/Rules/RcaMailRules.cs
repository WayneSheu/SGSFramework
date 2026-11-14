using System.Net;
using System.Net.Mail;
using System.Text;

namespace SGS.Modules.PMS.Application.Rules;

/// <summary>
/// RCA 代寄客戶的郵件規則（純函式）：收件人解析與驗證、主旨、內文、檔名。
/// 內文一律以團隊為主詞（不出現個人姓名），動態文字一律 HTML 編碼。
/// </summary>
public static class RcaMail
{
    public const int MaxRecipients = 10;
    public const int MaxMessageLength = 1000;

    private static readonly char[] Separators = [',', ';', '，', '；', '、', '\n', '\r', '\t', ' '];

    /// <summary>
    /// 解析收件人：以逗號、分號、換行或空白分隔，去除重複（不分大小寫）；每一筆須為純 Email 地址（不接受「姓名 &lt;地址&gt;」寫法）。
    /// </summary>
    public static (IReadOnlyList<string> Addresses, string? Error) ParseRecipients(IEnumerable<string>? raw, string label, bool required)
    {
        var tokens = (raw ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .SelectMany(s => s.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tokens.Count == 0) return ([], required ? $"請填寫{label}。" : null);
        if (tokens.Count > MaxRecipients) return ([], $"{label}最多 {MaxRecipients} 位。");

        foreach (var token in tokens)
        {
            if (!IsValidAddress(token)) return ([], $"{label}「{token}」不是有效的 Email 地址。");
        }

        return (tokens, null);
    }

    public static bool IsValidAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 254) return false;
        if (value.Any(char.IsControl) || value.Contains('<') || value.Contains('>')) return false;
        if (!MailAddress.TryCreate(value, out var address) || !string.Equals(address.Address, value, StringComparison.Ordinal)) return false;

        var at = value.LastIndexOf('@');
        return at > 0 && value[(at + 1)..].Contains('.') && !value.EndsWith('.');
    }

    public static string Subject(string projectCode, string ticketNo) => $"[{projectCode}] {ticketNo} 重大異常分析報告（RCA）";

    public static string FileName(DateOnly date, string ticketNo) => $"{date:yyyyMMdd}_{ticketNo}_RCA重大異常分析報告.pdf";

    /// <summary>信件內容（HTML）：PM 的補充說明（可空）＋固定說明；結尾署名為專案團隊，不署個人。</summary>
    public static string Body(string projectName, string ticketNo, string? message)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family: 'Microsoft JhengHei', Arial, sans-serif; font-size: 14px; line-height: 1.7; color: #212529;\">");
        sb.Append("<p>您好，</p>");

        if (!string.IsNullOrWhiteSpace(message))
            sb.Append("<p>").Append(Encode(message.Trim())).Append("</p>");

        sb.Append("<p>附件為 ").Append(Encode(projectName)).Append("（單號 ").Append(Encode(ticketNo))
          .Append("）的重大異常分析報告（RCA），內容包含事件摘要、精確時間軸、根本原因，以及短期止血與長期預防措施。</p>");
        sb.Append("<p>如有任何疑問，請直接回覆此信與我們聯繫。</p>");
        sb.Append("<p>").Append(Encode(projectName)).Append(" 專案團隊 敬上</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>紀錄用的寄送對象摘要（收件與副本）。</summary>
    public static string FormatSentTo(IReadOnlyList<string> to, IReadOnlyList<string> cc) =>
        cc.Count == 0 ? $"收件：{string.Join("、", to)}" : $"收件：{string.Join("、", to)}；副本：{string.Join("、", cc)}";

    private static string Encode(string value) => WebUtility.HtmlEncode(value).Replace("\r\n", "\n").Replace("\n", "<br/>");
}
