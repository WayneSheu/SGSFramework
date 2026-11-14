using System.Globalization;
using System.Text.RegularExpressions;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

public sealed record NamingResult(bool Valid, IReadOnlyList<string> Violations)
{
    public static NamingResult Pass { get; } = new(true, []);
}

/// <summary>系統產生檔名的結果。</summary>
public sealed record GeneratedName(bool Success, string? FileName, string? Error)
{
    public static GeneratedName Ok(string fileName) => new(true, fileName, null);
    public static GeneratedName Fail(string error) => new(false, null, error);
}

/// <summary>
/// 檔案命名鐵律（SOP 5.5）：
/// 一般文件 <c>YYYYMMDD_文件名稱_版本號_作者</c>、問題附件 <c>YYYYMMDD_[單號]_檔案說明</c>、
/// 模組文件 <c>YYYYMMDD_Mxx_模組名稱_文件名稱_版本號_作者</c>；禁止「最終版」「最新修正」等字眼，版本一律以版本號管理。
/// </summary>
public static partial class DeliverableNaming
{
    private static readonly string[] ForbiddenWords = ["最終版", "最新修正", "最新版", "final", "latest"];

    [GeneratedRegex(@"^(?<date>\d{8})_(?<name>[^_]+)_(?<ver>v\d+(\.\d+){1,2})_(?<author>[^_]+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex GeneralRegex();

    [GeneratedRegex(@"^(?<date>\d{8})_(?<ticket>[A-Z][A-Z0-9]{1,9}-\d{3,})_(?<desc>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex IssueRegex();

    [GeneratedRegex(@"^(?<date>\d{8})_(?<module>M\d{2})_(?<mname>[^_]+)_(?<doc>[^_]+)_(?<ver>v\d+(\.\d+){1,2})_(?<author>[^_]+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ModuleRegex();

    public static NamingResult Check(NamingKind kind, string? fileName)
    {
        var violations = new List<string>();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new NamingResult(false, ["檔名不可為空。"]);
        }

        // 去除路徑與副檔名後檢查本名
        var name = Path.GetFileName(fileName.Trim());
        var extension = Path.GetExtension(name);
        var stem = string.IsNullOrEmpty(extension) ? name : name[..^extension.Length];

        if (string.IsNullOrEmpty(extension))
            violations.Add("檔名須含副檔名。");

        foreach (var word in ForbiddenWords)
        {
            if (stem.Contains(word, StringComparison.OrdinalIgnoreCase))
                violations.Add($"含禁用字眼：{word}（版本一律以版本號管理）。");
        }

        var regex = kind switch
        {
            NamingKind.General => GeneralRegex(),
            NamingKind.Issue => IssueRegex(),
            NamingKind.Module => ModuleRegex(),
            _ => GeneralRegex()
        };

        var match = regex.Match(stem);
        if (!match.Success)
        {
            violations.Add($"檔名不符格式：{Pattern(kind)}。");
        }
        else if (!IsValidDate(match.Groups["date"].Value))
        {
            violations.Add("日期須為有效的 YYYYMMDD。");
        }

        return violations.Count == 0 ? NamingResult.Pass : new NamingResult(false, violations);
    }

    /// <summary>
    /// 依 SOP 5.5 由中繼資料產生標準檔名：一般文件 <c>YYYYMMDD_文件名稱_版本號_作者.ext</c>，
    /// 模組文件 <c>YYYYMMDD_Mxx_模組名稱_文件名稱_版本號_作者.ext</c>。
    /// 各段中的底線、路徑與檔案系統禁字會被清理；文件名稱含「最終版」等禁用字眼時回傳錯誤（不默默改寫）。
    /// 產生結果必定再通過 <see cref="Check"/>。
    /// </summary>
    public static GeneratedName Generate(
        DateOnly date,
        string? docName,
        string? version,
        string? author,
        string? extension,
        string? moduleCode = null,
        string? moduleName = null)
    {
        var name = Sanitize(docName);
        if (name.Length == 0) return GeneratedName.Fail("文件名稱為必填。");

        foreach (var word in ForbiddenWords)
        {
            if (name.Contains(word, StringComparison.OrdinalIgnoreCase))
                return GeneratedName.Fail($"文件名稱含禁用字眼：{word}（版本一律以版本號管理）。");
        }

        var ver = NormalizeVersion(version);
        if (ver is null) return GeneratedName.Fail("版本號格式須為 vX.Y 或 vX.Y.Z（例：v1.0）。");

        var who = Sanitize(StripEmailDomain(author));
        if (who.Length == 0) who = "unknown";

        var ext = (extension ?? string.Empty).Trim().ToLowerInvariant();
        if (ext.Length > 0 && ext[0] != '.') ext = "." + ext;
        if (!ExtensionRegex().IsMatch(ext)) return GeneratedName.Fail("檔案須具有有效的副檔名。");

        var hasModule = !string.IsNullOrWhiteSpace(moduleCode);
        string[] parts;
        if (hasModule)
        {
            var code = moduleCode!.Trim().ToUpperInvariant();
            var mname = Sanitize(moduleName);
            if (mname.Length == 0) return GeneratedName.Fail("模組名稱不可為空。");
            parts = [date.ToString("yyyyMMdd", CultureInfo.InvariantCulture), code, mname, name, ver, who];
        }
        else
        {
            parts = [date.ToString("yyyyMMdd", CultureInfo.InvariantCulture), name, ver, who];
        }

        var fileName = string.Join('_', parts) + ext;
        if (fileName.Length > MaxFileNameLength)
            return GeneratedName.Fail($"檔名過長（上限 {MaxFileNameLength} 字元），請縮短文件名稱。");

        var check = Check(hasModule ? NamingKind.Module : NamingKind.General, fileName);
        return check.Valid ? GeneratedName.Ok(fileName) : GeneratedName.Fail(string.Join(" ", check.Violations));
    }

    /// <summary>
    /// 產生問題單附件檔名（SOP 5.5）：<c>YYYYMMDD_[單號]_檔案說明.ext</c>。
    /// 檔案說明中的底線與禁字會被清理；說明為空時取原始檔名主檔名。
    /// </summary>
    public static GeneratedName GenerateIssue(DateOnly date, string? ticketNo, string? description, string? extension)
    {
        var no = (ticketNo ?? string.Empty).Trim().ToUpperInvariant();
        var desc = Sanitize(description);
        if (desc.Length == 0) return GeneratedName.Fail("檔案說明為必填。");

        var ext = (extension ?? string.Empty).Trim().ToLowerInvariant();
        if (ext.Length > 0 && ext[0] != '.') ext = "." + ext;
        if (!ExtensionRegex().IsMatch(ext)) return GeneratedName.Fail("檔案須具有有效的副檔名。");

        var fileName = string.Join('_', date.ToString("yyyyMMdd", CultureInfo.InvariantCulture), no, desc) + ext;
        if (fileName.Length > MaxFileNameLength)
            return GeneratedName.Fail($"檔名過長（上限 {MaxFileNameLength} 字元），請縮短檔案說明。");

        var check = Check(NamingKind.Issue, fileName);
        return check.Valid ? GeneratedName.Ok(fileName) : GeneratedName.Fail(string.Join(" ", check.Violations));
    }

    /// <summary>
    /// 產生 UAT 驗收附件檔名：<c>YYYYMMDD_專案代號_UAT_版本號_檔案說明.ext</c>（客戶回覆信件、簽署掃描檔、驗收截圖等）。
    /// 檔案說明中的底線與禁字會被清理；說明為空時取原始檔名主檔名。
    /// </summary>
    public static GeneratedName GenerateUat(DateOnly date, string? projectCode, string? version, string? description, string? extension)
    {
        var code = (projectCode ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 0 || code.Contains('_')) return GeneratedName.Fail("專案代號不正確。");

        var ver = NormalizeVersion(version);
        if (ver is null) return GeneratedName.Fail("版本號格式須為 vX.Y 或 vX.Y.Z（例：v1.0）。");

        var desc = Sanitize(description);
        if (desc.Length == 0) return GeneratedName.Fail("檔案說明為必填。");

        var ext = (extension ?? string.Empty).Trim().ToLowerInvariant();
        if (ext.Length > 0 && ext[0] != '.') ext = "." + ext;
        if (!ExtensionRegex().IsMatch(ext)) return GeneratedName.Fail("檔案須具有有效的副檔名。");

        var fileName = string.Join('_', date.ToString("yyyyMMdd", CultureInfo.InvariantCulture), code, "UAT", ver, desc) + ext;
        return fileName.Length > MaxFileNameLength
            ? GeneratedName.Fail($"檔名過長（上限 {MaxFileNameLength} 字元），請縮短檔案說明。")
            : GeneratedName.Ok(fileName);
    }

    /// <summary>正規化版本號：接受 v1.0／1.0／V1.2.3；回傳小寫 v 開頭，不合規回傳 null。</summary>
    public static string? NormalizeVersion(string? version)
    {
        var v = (version ?? string.Empty).Trim();
        if (v.Length == 0) return null;
        if (v[0] is 'V' or 'v') v = v[1..];
        return VersionRegex().IsMatch(v) ? "v" + v : null;
    }

    /// <summary>清理單一檔名片段：移除路徑與檔案系統禁字、控制字元；底線與空白（含全形）改為連字號。</summary>
    internal static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var sb = new System.Text.StringBuilder(value.Length);
        var lastWasSeparator = false;
        foreach (var ch in value.Trim())
        {
            if (char.IsControl(ch) || InvalidChars.Contains(ch)) continue;

            if (ch == '_' || char.IsWhiteSpace(ch))
            {
                if (!lastWasSeparator && sb.Length > 0) sb.Append('-');
                lastWasSeparator = true;
                continue;
            }

            sb.Append(ch);
            lastWasSeparator = false;
        }

        return sb.ToString().Trim('-', '.');
    }

    private static string? StripEmailDomain(string? value)
    {
        if (value is null) return null;
        var at = value.IndexOf('@');
        return at > 0 ? value[..at] : value;
    }

    private const int MaxFileNameLength = 200;

    private static readonly HashSet<char> InvalidChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    [GeneratedRegex(@"^\.[a-z0-9]{1,10}$", RegexOptions.CultureInvariant)]
    private static partial Regex ExtensionRegex();

    [GeneratedRegex(@"^\d+(\.\d+){1,2}$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();

    public static string Pattern(NamingKind kind) => kind switch
    {
        NamingKind.General => "YYYYMMDD_文件名稱_版本號_作者",
        NamingKind.Issue => "YYYYMMDD_[單號]_檔案說明",
        NamingKind.Module => "YYYYMMDD_Mxx_模組名稱_文件名稱_版本號_作者",
        _ => string.Empty
    };

    private static bool IsValidDate(string yyyymmdd) =>
        DateTime.TryParseExact(yyyymmdd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
