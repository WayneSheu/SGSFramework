using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SGS.Modules.MTG.Application.Rules;

/// <summary>從紀要解析出的一筆待辦（尚未寫入資料庫）。</summary>
public sealed record ExtractedActionItem(string Description, string? Owner, string? DueText, DateOnly? DueDate, string? Note);

/// <summary>
/// 待辦事項的純函式規則：解析紀要中的待辦表格、解析模型輸出的 JSON、把「下週五」「11/30」之類的期限文字轉成日期、
/// 去重鍵與收件人驗證。全部沒有 I/O，方便單元測試。
/// </summary>
public static partial class ActionItemRules
{
    public const int MaxDescriptionLength = 500;
    public const int MaxOwnerLength = 100;
    public const int MaxDueTextLength = 100;
    public const int MaxNoteLength = 500;

    /// <summary>台灣沒有日光節約時間，固定 UTC+8；用於把會議時間（UTC）換算成「會議當天」的日期。</summary>
    public static readonly TimeSpan TaiwanOffset = TimeSpan.FromHours(8);

    private static readonly HashSet<string> UnknownWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "未提及", "沒有提及", "無", "沒有", "無提及", "未指定", "未定", "待確認", "待定", "不明", "尚無", "n/a", "na", "none", "null", "-", "—", "–", "無待辦", "無待辦事項"
    };

    // ---------- 欄位清理 ----------

    /// <summary>去除 Markdown 強調符號與多餘空白；「未提及」「無」等占位字樣視為沒有值（回傳 null）；超過長度截斷。</summary>
    public static string? CleanField(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Replace("**", string.Empty).Replace("__", string.Empty).Replace("`", string.Empty);
        text = Regex.Replace(text, @"<br\s*/?>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\s+", " ").Trim().Trim('「', '」', '"');
        if (text.Length == 0 || UnknownWords.Contains(text)) return null;
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    // ---------- Markdown 表格 ----------

    /// <summary>
    /// 找出標題含「待辦」的段落，解析其中的表格。欄位依表頭辨識（待辦事項／負責人／期限／備註），表頭不認得時依序當作這四欄。
    /// 事項欄是占位字樣或空白的列會被略過。
    /// </summary>
    public static IReadOnlyList<ExtractedActionItem> ParseTable(string? markdown, DateOnly reference)
    {
        var result = new List<ExtractedActionItem>();
        if (string.IsNullOrWhiteSpace(markdown)) return result;

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var inSection = false;
        int descCol = 0, ownerCol = 1, dueCol = 2, noteCol = 3;
        var headerSeen = false;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith('#'))
            {
                inSection = line.Contains("待辦", StringComparison.Ordinal) || line.Contains("Action Item", StringComparison.OrdinalIgnoreCase);
                headerSeen = false;
                continue;
            }

            if (!inSection || !line.StartsWith('|')) continue;

            var cells = SplitRow(line);
            if (cells.All(c => Regex.IsMatch(c, @"^:?-{2,}:?$") || c.Length == 0)) continue; // 分隔列 |---|---|

            if (!headerSeen)
            {
                headerSeen = true;
                var d = IndexOf(cells, "待辦", "事項", "內容", "任務", "行動");
                if (d >= 0)
                {
                    descCol = d;
                    ownerCol = IndexOf(cells, "負責", "owner", "指派");
                    dueCol = IndexOf(cells, "期限", "到期", "完成日", "deadline", "due");
                    noteCol = IndexOf(cells, "備註", "說明", "note");
                    continue; // 這是表頭列
                }
                // 表頭認不得：沿用預設欄位順序，且這一列當作資料
            }

            var description = CleanField(Cell(cells, descCol), MaxDescriptionLength);
            if (description is null) continue;

            var dueText = CleanField(Cell(cells, dueCol), MaxDueTextLength);
            result.Add(new ExtractedActionItem(description, CleanField(Cell(cells, ownerCol), MaxOwnerLength), dueText,
                ParseDue(dueText, reference), CleanField(Cell(cells, noteCol), MaxNoteLength)));
        }

        return result;
    }

    private static List<string> SplitRow(string line)
    {
        var trimmed = line.Trim().Trim('|');
        return trimmed.Split('|').Select(c => c.Trim()).ToList();
    }

    private static int IndexOf(List<string> headers, params string[] keywords)
    {
        for (var i = 0; i < headers.Count; i++)
            if (keywords.Any(k => headers[i].Contains(k, StringComparison.OrdinalIgnoreCase))) return i;
        return -1;
    }

    private static string? Cell(List<string> cells, int index) => index >= 0 && index < cells.Count ? cells[index] : null;

    // ---------- 模型 JSON ----------

    /// <summary>
    /// 解析模型輸出的 JSON 陣列（容忍 Markdown 圍欄與前後說明文字）。元素欄位：description（或 task、item）、owner、due、note。
    /// 解析失敗回傳空清單，由呼叫端決定如何處理。
    /// </summary>
    public static IReadOnlyList<ExtractedActionItem> ParseJson(string? text, DateOnly reference)
    {
        var result = new List<ExtractedActionItem>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start) return result;

        try
        {
            using var doc = JsonDocument.Parse(text[start..(end + 1)]);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) continue;
                var description = CleanField(Str(element, "description", "task", "item", "title"), MaxDescriptionLength);
                if (description is null) continue;

                var dueText = CleanField(Str(element, "due", "dueDate", "deadline"), MaxDueTextLength);
                result.Add(new ExtractedActionItem(description, CleanField(Str(element, "owner", "assignee", "responsible"), MaxOwnerLength),
                    dueText, ParseDue(dueText, reference), CleanField(Str(element, "note", "remark"), MaxNoteLength)));
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return result;
    }

    private static string? Str(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var prop in obj.EnumerateObject())
            {
                if (!prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                return prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => prop.Value.ToString(),
                    _ => null
                };
            }
        }

        return null;
    }

    /// <summary>給模型的抽取提示。只輸出 JSON，禁止編造負責人與期限。</summary>
    public static string BuildExtractionPrompt(string minutesMarkdown) =>
        "你是會議待辦事項抽取器。從下列會議紀要中，抽出所有「需要有人去執行」的待辦事項。\n" +
        "只輸出一個 JSON 陣列，不要任何說明文字，也不要 Markdown 程式碼圍欄。每個元素的格式：\n" +
        "{\"description\":\"待辦內容（一句話，動詞開頭）\",\"owner\":\"負責人姓名；紀要沒有明確提到就填 null\",\"due\":\"期限原文；沒有提到就填 null\",\"note\":\"補充；沒有就填 null\"}\n" +
        "規則：不要編造負責人或期限；已達成的決議與討論結論不是待辦；風險或待釐清問題只有在明確要有人跟進時才算待辦；沒有任何待辦就輸出 []。\n\n" +
        "【會議紀要】\n" + minutesMarkdown;

    // ---------- 期限 ----------

    [GeneratedRegex(@"(\d{4})[-/.年](\d{1,2})[-/.月](\d{1,2})日?")]
    private static partial Regex FullDate();

    [GeneratedRegex(@"(?<![\d/])(\d{1,2})[/月](\d{1,2})日?(?![\d/])")]
    private static partial Regex MonthDay();

    [GeneratedRegex(@"(下下|下|本|這|这|該)?\s*(?:週|周|星期|禮拜|礼拜)\s*([一二三四五六日天])")]
    private static partial Regex Weekday();

    /// <summary>
    /// 把期限文字轉成日期；無法確定就回傳 null（原文仍保留在 DueText 供人工判讀）。
    /// 支援：2026-11-30、2026/11/30、11/30、11月30日、今天／明天／後天、（下|本）週五、週五前（沒寫「下」就取最近一個不早於參考日的該星期）。
    /// 「第三周」「兩週內」之類無法對應到確切日期的說法不猜。
    /// </summary>
    public static DateOnly? ParseDue(string? text, DateOnly reference)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var full = FullDate().Match(text);
        if (full.Success && TryDate(int.Parse(full.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(full.Groups[2].Value, CultureInfo.InvariantCulture), int.Parse(full.Groups[3].Value, CultureInfo.InvariantCulture), out var d1))
            return d1;

        var md = MonthDay().Match(text);
        if (md.Success)
        {
            var month = int.Parse(md.Groups[1].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(md.Groups[2].Value, CultureInfo.InvariantCulture);
            if (TryDate(reference.Year, month, day, out var d2))
            {
                // 例如 12 月開的會說「1/15」，指的是明年
                if (d2 < reference.AddDays(-30) && TryDate(reference.Year + 1, month, day, out var next)) return next;
                return d2;
            }
        }

        if (text.Contains("今天", StringComparison.Ordinal) || text.Contains("今日", StringComparison.Ordinal)) return reference;
        if (text.Contains("明天", StringComparison.Ordinal) || text.Contains("明日", StringComparison.Ordinal)) return reference.AddDays(1);
        if (text.Contains("後天", StringComparison.Ordinal) || text.Contains("后天", StringComparison.Ordinal)) return reference.AddDays(2);

        var wd = Weekday().Match(text);
        if (wd.Success)
        {
            var target = wd.Groups[2].Value switch { "一" => DayOfWeek.Monday, "二" => DayOfWeek.Tuesday, "三" => DayOfWeek.Wednesday, "四" => DayOfWeek.Thursday, "五" => DayOfWeek.Friday, "六" => DayOfWeek.Saturday, _ => DayOfWeek.Sunday };
            var prefix = wd.Groups[1].Value;

            // 以「週一為一週的開始」計算參考日所在週的週一
            var mondayOffset = ((int)reference.DayOfWeek + 6) % 7;
            var thisMonday = reference.AddDays(-mondayOffset);
            var targetOffset = ((int)target + 6) % 7;

            return prefix switch
            {
                "下下" => thisMonday.AddDays(14 + targetOffset),
                "下" => thisMonday.AddDays(7 + targetOffset),
                "本" or "這" or "这" or "該" => thisMonday.AddDays(targetOffset),
                _ => thisMonday.AddDays(targetOffset) >= reference ? thisMonday.AddDays(targetOffset) : thisMonday.AddDays(7 + targetOffset)
            };
        }

        return null;
    }

    private static bool TryDate(int year, int month, int day, out DateOnly date)
    {
        date = default;
        if (year < 2000 || year > 2100 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return false;
        date = new DateOnly(year, month, day);
        return true;
    }

    /// <summary>會議時間（UTC）換算成台灣當地的日期，當作相對期限（「下週五」）的參考日。</summary>
    public static DateOnly ReferenceDate(DateTimeOffset meetingTimeUtc) => DateOnly.FromDateTime(meetingTimeUtc.ToOffset(TaiwanOffset).DateTime);

    // ---------- 去重與收件人 ----------

    /// <summary>去重鍵：只留文字與數字並轉小寫，所以標點、空白或全半形差異不會讓同一件待辦重複出現。</summary>
    public static string NormalizeKey(string description) =>
        string.Concat((description ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();

    /// <summary>是否為可寄送的單一 Email（不含換行、不是 "名稱 &lt;a@b&gt;" 這種複合格式）。</summary>
    public static bool IsValidEmail(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();
        if (text.Length > 254 || text.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c is '<' or '>' or ',' or ';' or '"')) return false;
        if (!MailAddress.TryCreate(text, out var address) || address.Address != text || !address.Host.Contains('.')) return false;

        normalized = text;
        return true;
    }

    /// <summary>郵件主旨不得含換行；過長截斷。</summary>
    public static string SafeSubject(string subject)
    {
        var flat = Regex.Replace(subject ?? string.Empty, @"[\r\n\t]+", " ").Trim();
        return flat.Length <= 150 ? flat : flat[..150];
    }
}
