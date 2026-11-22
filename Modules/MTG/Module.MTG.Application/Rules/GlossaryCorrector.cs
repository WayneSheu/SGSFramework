using System.Text;
using System.Text.RegularExpressions;

namespace SGS.Modules.MTG.Application.Rules;

/// <summary>術語表項目：標準詞與其常見誤聽寫法。</summary>
public sealed record GlossaryTerm(string Term, IReadOnlyList<string> Variants);

/// <summary>
/// 術語校正（純函式）：把誤聽詞換成標準詞，並把標準詞的大小寫標準化（api → API）。
/// 英數詞以「不被英數字夾住」為邊界，避免把 capital 中的 api 改掉；含中文的詞直接比對。
/// </summary>
public static class GlossaryCorrector
{
    public const char VariantSeparator = '|';

    public static IReadOnlyList<string> ParseVariants(string? variants) =>
        string.IsNullOrWhiteSpace(variants)
            ? []
            : variants.Split(VariantSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string Correct(string? text, IReadOnlyList<GlossaryTerm> terms)
    {
        if (string.IsNullOrEmpty(text) || terms.Count == 0) return text ?? string.Empty;

        // 較長的詞先處理，避免短詞先吃掉長詞的一部分
        var rules = terms
            .Where(t => !string.IsNullOrWhiteSpace(t.Term))
            .SelectMany(t => t.Variants.Append(t.Term).Select(v => (Match: v.Trim(), Target: t.Term.Trim())))
            .Where(r => r.Match.Length > 0)
            .DistinctBy(r => r.Match, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(r => r.Match.Length)
            .ToList();

        // 單趟掃描：所有規則合成一個 alternation，已替換的文字不會被後面的規則二次處理
        var map = rules.ToDictionary(r => r.Match, r => r.Target, StringComparer.OrdinalIgnoreCase);
        var pattern = string.Join('|', rules.Select(r => BuildPattern(r.Match)));
        return Regex.Replace(text, pattern, m => map.TryGetValue(m.Value, out var target) ? target : m.Value,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    }

    private static string BuildPattern(string word)
    {
        var escaped = Regex.Escape(word);
        return IsAsciiWord(word) ? $"(?<![A-Za-z0-9]){escaped}(?![A-Za-z0-9])" : escaped;
    }

    private static bool IsAsciiWord(string s) => s.All(c => c < 128 && (char.IsLetterOrDigit(c) || c == ' ' || c == '.' || c == '-' || c == '_'))
                                                  && char.IsLetterOrDigit(s[0]) && char.IsLetterOrDigit(s[^1]);

    /// <summary>繁體中文提示句。Whisper 對中文預設輸出簡體；實測沒有提示詞時整段都是簡體，任何繁體提示詞都能讓輸出改為繁體。</summary>
    public const string TraditionalChineseHint = "以下是繁體中文的會議記錄。";

    /// <summary>
    /// 產生語音辨識的 Initial Prompt：以自然語句列出術語，引導中英夾雜與專有名詞的辨識。
    /// <paramref name="traditionalChinese"/> 為 true 時永遠帶上繁體中文提示句（術語表是空的也一樣），避免輸出變成簡體；
    /// 語言不是中文時設為 false，沒有術語就不送提示詞。
    /// </summary>
    public static string BuildInitialPrompt(IReadOnlyList<GlossaryTerm> terms, int maxTerms = 60, bool traditionalChinese = true)
    {
        var names = terms.Select(t => t.Term.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(maxTerms).ToList();
        if (names.Count == 0) return traditionalChinese ? TraditionalChineseHint : string.Empty;

        var sb = new StringBuilder(traditionalChinese ? "以下是繁體中文的會議記錄，中英夾雜，內容可能提到：" : "以下是一場中英夾雜的技術會議，內容可能提到：");
        sb.Append(string.Join('、', names)).Append('。');
        return sb.ToString();
    }

    /// <summary>轉錄語言是中文（zh、zh-TW…）或未指定（自動偵測）時，輸出需要繁體中文提示。</summary>
    public static bool IsChineseOrAuto(string? language) =>
        string.IsNullOrWhiteSpace(language) || language.Trim().StartsWith("zh", StringComparison.OrdinalIgnoreCase)
        || language.Trim().Equals("auto", StringComparison.OrdinalIgnoreCase);
}
