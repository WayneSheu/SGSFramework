using System.Text.RegularExpressions;
using SGS.Modules.MAN.Application.Contracts;

namespace SGS.Modules.MAN.Application.Rules;

/// <summary>操作手冊主題驗證規則（純函式）。</summary>
public static partial class ManualTopicRules
{
    public const int MaxFeatureKeyLength = 100;
    public const int MaxTitleLength = 200;
    public const int MaxTooltipLength = 30;
    public const int MaxMarkdownLength = 20000;
    public const int MaxPageSize = 100;

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_\-\.]*$")]
    private static partial Regex FeatureKeyRegex();

    public static bool IsValidFeatureKey(string? key) =>
        !string.IsNullOrEmpty(key) && key.Length <= MaxFeatureKeyLength && FeatureKeyRegex().IsMatch(key);

    /// <summary>驗證儲存請求；回傳 null 代表通過。</summary>
    public static string? Validate(SaveManualTopicRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsValidFeatureKey(request.FeatureKey?.Trim()))
            return "功能代碼僅限小寫英數字、底線、連字號與句點，須以英數字開頭，且不得超過 100 字。";
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > MaxTitleLength)
            return $"標題為必填且不得超過 {MaxTitleLength} 字。";
        if (string.IsNullOrWhiteSpace(request.Tooltip) || request.Tooltip.Trim().Length > MaxTooltipLength)
            return $"內嵌提示為必填且不得超過 {MaxTooltipLength} 字。";
        if ((request.StepsMarkdown?.Length ?? 0) > MaxMarkdownLength || (request.Precautions?.Length ?? 0) > MaxMarkdownLength)
            return $"操作步驟與注意事項各不得超過 {MaxMarkdownLength} 字。";
        return null;
    }

    /// <summary>發布前檢查：已發布內容必須有操作步驟。</summary>
    public static string? ValidateForPublish(string? stepsMarkdown) =>
        string.IsNullOrWhiteSpace(stepsMarkdown) ? "發布前必須填寫操作步驟。" : null;

    public static (int PageIndex, int PageSize) NormalizePaging(int pageIndex, int pageSize) =>
        (Math.Max(1, pageIndex), Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, MaxPageSize));
}
