namespace SGS.Modules.RUL.Application.Rules;

/// <summary>Maker-Checker 職責分離（純函式）。</summary>
public static class RuleApprovalRules
{
    /// <summary>申請者與審核者為同一人（忽略大小寫與前後空白）。</summary>
    public static bool IsSelfApproval(string requestedBy, string reviewer) =>
        string.Equals(requestedBy?.Trim(), reviewer?.Trim(), StringComparison.OrdinalIgnoreCase);
}
