using System.Text;
using System.Text.RegularExpressions;

namespace SGS.Modules.PMS.Application.Rules;

public enum ProjectCodeCheck
{
    Available,
    Invalid,
    Reserved,
    AlreadyUsed,
    Retired
}

/// <summary>
/// 專案代號政策（SDD 2.6.1）：2~10 碼大寫英數、首碼為字母、非保留字；核發後不可變、永不重用。
/// 純函式，不依賴資料庫。
/// </summary>
public static partial class ProjectCodePolicy
{
    public const int MinLength = 2;
    public const int MaxLength = 10;

    [GeneratedRegex("^[A-Z][A-Z0-9]{1,9}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();

    private static readonly HashSet<string> ReservedWords = new(StringComparer.Ordinal)
    {
        "SYS", "SYSTEM", "TEST", "ADMIN", "ROOT", "NULL", "API", "PMS", "CR"
    };

    public static bool IsValidFormat(string? code) => code is not null && CodeRegex().IsMatch(code);

    public static bool IsReservedWord(string code, IEnumerable<string>? extraReserved = null) =>
        ReservedWords.Contains(code) || (extraReserved?.Contains(code, StringComparer.Ordinal) ?? false);

    /// <summary>正規化（去空白、轉大寫）。</summary>
    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// 判斷代號可用性。<paramref name="existing"/> 為保留表中該代號的狀態（無則為 null）。
    /// </summary>
    public static ProjectCodeCheck Check(string? code, ProjectCodeUsage? existing, IEnumerable<string>? extraReserved = null)
    {
        if (!IsValidFormat(code)) return ProjectCodeCheck.Invalid;
        if (IsReservedWord(code!, extraReserved)) return ProjectCodeCheck.Reserved;

        return existing switch
        {
            ProjectCodeUsage.Retired => ProjectCodeCheck.Retired,
            ProjectCodeUsage.InUse => ProjectCodeCheck.AlreadyUsed,
            _ => ProjectCodeCheck.Available
        };
    }

    /// <summary>
    /// 依專案名稱建議代號：取英文字母／數字的詞首縮寫，不足補字元；衝突時加序號。
    /// 中文名稱無法縮寫時回傳以 "P" 為首的序號候選。
    /// </summary>
    public static IReadOnlyList<string> Suggest(string? name, Func<string, bool> isTaken, int max = 3)
    {
        ArgumentNullException.ThrowIfNull(isTaken);

        var bases = BuildBases(name);
        var result = new List<string>();

        foreach (var b in bases)
        {
            if (result.Count >= max) break;
            if (TryAdd(b)) continue;

            for (var i = 2; i <= 99 && result.Count < max; i++)
            {
                var suffix = i.ToString();
                var candidate = (b.Length + suffix.Length > MaxLength ? b[..(MaxLength - suffix.Length)] : b) + suffix;
                if (TryAdd(candidate)) break;
            }
        }

        // 保底：仍不足時產生 PRJ+序號
        for (var i = 1; result.Count < max && i <= 999; i++)
        {
            TryAdd($"PRJ{i}");
        }

        return result;

        bool TryAdd(string candidate)
        {
            if (!IsValidFormat(candidate) || IsReservedWord(candidate) || isTaken(candidate) || result.Contains(candidate))
                return false;
            result.Add(candidate);
            return true;
        }
    }

    private static List<string> BuildBases(string? name)
    {
        var words = Regex.Matches(name ?? string.Empty, "[A-Za-z][A-Za-z0-9]*")
            .Select(m => m.Value.ToUpperInvariant())
            .ToList();

        var bases = new List<string>();
        if (words.Count == 0) return ["PRJ"];

        // 詞首縮寫（例：Smart Lab System → SLS）
        var initials = new StringBuilder();
        foreach (var w in words) initials.Append(w[0]);
        if (initials.Length >= MinLength) bases.Add(Trim(initials.ToString()));

        // 第一個詞（例：SGSLIMS → SGSLIMS）
        bases.Add(Trim(words[0]));

        // 前 4 碼
        if (words[0].Length > 4) bases.Add(words[0][..4]);

        return bases.Where(b => b.Length >= MinLength).Distinct().ToList();

        static string Trim(string s) => s.Length > MaxLength ? s[..MaxLength] : s;
    }
}

/// <summary>代號於保留表中的使用情形（Application 層與資料列狀態解耦）。</summary>
public enum ProjectCodeUsage
{
    InUse,
    Retired
}
