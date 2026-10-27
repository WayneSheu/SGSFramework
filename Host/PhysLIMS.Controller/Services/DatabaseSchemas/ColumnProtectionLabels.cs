using SGSFramework.ApiInfrastructure.DTOs.DatabaseSchemas;

namespace SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;

/// <summary>
/// 欄位資安標註的顯示文字，供 Excel / PDF 匯出共用 (與前端資料庫結構頁面的徽章用語一致)
/// </summary>
internal static class ColumnProtectionLabels
{
    public const string EncryptedLabel = "加密";
    public const string AuditIgnoredLabel = "不稽核";
    public const string AuditMaskedLabel = "稽核遮蔽";

    public const string Legend =
        "加密 = 資料庫內為密文；遮罩 = 寫入系統日誌時遮蔽；稽核遮蔽 = 資料日誌只記錄「已變更」不記錄值；不稽核 = 資料日誌不記錄此欄位";

    /// <summary>
    /// 統計摘要 (例如「加密 1 欄、日誌遮罩 1 欄」)，供文件資訊區塊呈現個資保護概況
    /// </summary>
    public static string Summary(IEnumerable<ColumnProtectionDto?> protections)
    {
        var list = protections.OfType<ColumnProtectionDto>().ToList();
        return $"加密 {list.Count(p => p.IsEncrypted)} 欄、日誌遮罩 {list.Count(p => p.MaskFormat is not null)} 欄、" +
               $"稽核遮蔽 {list.Count(p => p.AuditHandling == "Masked")} 欄、不稽核 {list.Count(p => p.AuditHandling == "Ignored")} 欄";
    }

    public static string? Encrypted(ColumnProtectionDto? protection) =>
        protection?.IsEncrypted == true ? EncryptedLabel : null;

    public static string? Audit(ColumnProtectionDto? protection) => protection?.AuditHandling switch
    {
        "Ignored" => AuditIgnoredLabel,
        "Masked" => AuditMaskedLabel,
        _ => null
    };

    /// <summary>
    /// 遮罩格式簡稱：去除括號內範例與「遮蔽」字尾 (「姓名遮蔽 (陳○明)」→「姓名」)
    /// </summary>
    public static string? MaskShort(ColumnProtectionDto? protection)
    {
        if (protection?.MaskFormat is not { } format) return null;

        var name = format.Split('(')[0].Trim();
        return name.EndsWith("遮蔽", StringComparison.Ordinal) && name.Length > 2 ? name[..^2] : name;
    }

    /// <summary>
    /// 全部標註 (依保護強度排序：加密 → 遮罩 → 稽核)
    /// </summary>
    public static IReadOnlyList<string> All(ColumnProtectionDto? protection)
    {
        var mask = MaskShort(protection);
        return new[] { Encrypted(protection), mask is null ? null : $"遮罩：{mask}", Audit(protection) }
            .OfType<string>()
            .ToList();
    }
}
