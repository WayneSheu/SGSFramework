using System.Security.Cryptography;
using System.Text;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

/// <summary>
/// UAT 驗收簽核規則（SOP 第 5 章），純函式：驗收內容以雜湊固化，簽核須對同一雜湊；僅待簽核可記錄結果或取消；
/// 不通過須說明原因。客戶外部簽核帳號開放前，由專案 PM 代錄客戶結果並附原因與依據。
/// </summary>
public static class UatRules
{
    /// <summary>驗收內容雜湊：專案代號、版本、驗收範圍（範圍去除前後空白，正規化換行）。</summary>
    public static string ComputeHash(string projectCode, string version, string scopeNote)
    {
        var scope = (scopeNote ?? string.Empty).Trim().Replace("\r\n", "\n");
        var payload = $"UAT|{projectCode.Trim().ToUpperInvariant()}|{version}|{scope}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static string? ValidateCreate(string? scopeNote)
    {
        if (string.IsNullOrWhiteSpace(scopeNote)) return "驗收範圍為必填（列出納入驗收的功能與測試情境）。";
        if (scopeNote.Trim().Length > 2000) return "驗收範圍不得超過 2000 字。";
        return null;
    }

    /// <summary>記錄驗收結果：僅待簽核；結果須為通過或不通過；代錄須附原因；不通過須說明。</summary>
    public static RuleResult ValidateRecord(UatStatus current, UatStatus result, string? proxyReason, string? comment)
    {
        if (current != UatStatus.PendingSign)
            return RuleResult.Fail(RuleOutcome.InvalidTransition, $"驗收單目前狀態為 {current}，不可再記錄結果。");
        if (result is not (UatStatus.Accepted or UatStatus.Rejected))
            return RuleResult.Fail(RuleOutcome.InvalidTransition, "驗收結果須為通過或不通過。");
        if (string.IsNullOrWhiteSpace(proxyReason))
            return RuleResult.Fail(RuleOutcome.InvalidTransition, "客戶外部簽核帳號尚未開放，由 PM 代錄須填寫原因與依據（例如客戶 Email 回覆）。");
        if (result == UatStatus.Rejected && string.IsNullOrWhiteSpace(comment))
            return RuleResult.Fail(RuleOutcome.InvalidTransition, "驗收不通過須說明原因（缺陷或未達驗收標準之處）。");
        if (proxyReason.Length > 1000 || (comment?.Length ?? 0) > 1000)
            return RuleResult.Fail(RuleOutcome.InvalidTransition, "原因與意見各不得超過 1000 字。");
        return RuleResult.Ok();
    }

    public static RuleResult ValidateCancel(UatStatus current, string? reason)
    {
        if (current != UatStatus.PendingSign)
            return RuleResult.Fail(RuleOutcome.InvalidTransition, "僅待簽核的驗收單可取消。");
        if (string.IsNullOrWhiteSpace(reason))
            return RuleResult.Fail(RuleOutcome.InvalidTransition, "取消原因為必填。");
        return RuleResult.Ok();
    }
}
