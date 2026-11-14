using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

/// <summary>範本項目（SOP 附錄 A 上線前資安與發布檢核表）。</summary>
public sealed record ReleaseTemplateItem(string Code, string Section, string Title, string Standard, ReleaseSeverity Severity);

/// <summary>檢核表範本：建立檢核表時依此展開 SEC-01~11、REL-01~06；標題與合格標準由範本提供，不逐筆存入資料庫。</summary>
public static class ReleaseChecklistTemplate
{
    private const string A1 = "A-1 系統設計與機敏資料保護";
    private const string A2 = "A-2 安全編碼與原始碼掃描";
    private const string A3 = "A-3 動態測試與環境安全設定";
    private const string A4 = "A-4 發布與回滾";

    public static IReadOnlyList<ReleaseTemplateItem> Items { get; } =
    [
        new("SEC-01", A1, "金鑰與密碼管理", "程式碼與 Git 儲存庫無寫死任何資料庫密碼、API Key 或私鑰，全數改用環境變數或 KMS。", ReleaseSeverity.Blocking),
        new("SEC-02", A1, "機敏資料加密", "使用者密碼以強雜湊（bcrypt／Argon2）加鹽儲存；身分證、金流等機敏欄位已加密儲存或遮罩顯示。", ReleaseSeverity.Blocking),
        new("SEC-03", A1, "傳輸安全設定", "正式環境強制 HTTPS（TLS 1.2 以上），已設定 HSTS 與安全 Cookie 屬性（HttpOnly、Secure）。", ReleaseSeverity.Blocking),
        new("SEC-04", A2, "注入攻擊防護", "所有資料庫查詢使用參數化查詢（Parameterized Query／ORM），防範 SQL Injection 與 XSS。", ReleaseSeverity.Blocking),
        new("SEC-05", A2, "權限與越權驗證", "所有 API 端點已驗證存取權限（RBAC），無法透過修改網址 ID 存取他人資料（防範 IDOR）。", ReleaseSeverity.Blocking),
        new("SEC-06", A2, "SAST 源碼掃描", "已執行靜態掃描（如 SonarQube），Critical 與 High 漏洞均已修復，報告已歸檔。", ReleaseSeverity.Blocking),
        new("SEC-07", A2, "第三方套件檢查", "已執行 SCA，確認開源套件無已知重大 CVE（高風險項目）。", ReleaseSeverity.Warning),
        new("SEC-08", A3, "DAST 弱點掃描", "已對測試環境執行動態掃描（如 OWASP ZAP），中高風險漏洞均已修復並複測，報告歸檔至 04_測試與品保/02_測試報告。", ReleaseSeverity.Blocking),
        new("SEC-09", A3, "錯誤訊息與除錯模式", "正式環境已關閉 Debug 模式；錯誤時不對外顯示伺服器路徑、框架版本或堆疊追蹤。", ReleaseSeverity.Blocking),
        new("SEC-10", A3, "檔案上傳限制", "若有上傳功能：副檔名白名單、檔案大小限制，且存放於無執行權限的獨立儲存空間（如 S3）。", ReleaseSeverity.Blocking),
        new("SEC-11", A3, "異常登入與速率限制", "登入頁、簡訊驗證與核心 API 已設定速率限制或圖形驗證，防範暴力破解與惡意爬蟲。", ReleaseSeverity.Warning),
        new("REL-01", A4, "資料庫向後相容", "本次 Migration 未刪除或重命名舊欄位（Expand & Contract），退回舊版插件仍可正常運作。", ReleaseSeverity.Blocking),
        new("REL-02", A4, "回滾計畫", "回滾步驟已歸檔至 05_部署與維運/01_上線計畫，並確認可卸載插件或切回舊版。", ReleaseSeverity.Blocking),
        new("REL-03", A4, "發布順序與健康檢查", "後端插件先載入並通過 Health Check，再發布前端靜態資源。", ReleaseSeverity.Blocking),
        new("REL-04", A4, "自動化測試與冒煙測試", "CI 單元與整合測試全數通過；部署後 E2E 冒煙測試通過。", ReleaseSeverity.Blocking),
        new("REL-05", A4, "API 契約相容", "openapi.yaml 變更已獲 BE 與 FE 雙方 Approve；破壞性變更已同步前端。", ReleaseSeverity.Blocking),
        new("REL-06", A4, "交叉驗證", "本次變更已由非主要作者完成驗證，紀錄已歸檔至 04_測試與品保/02_測試報告。", ReleaseSeverity.Blocking)
    ];

    private static readonly Dictionary<string, ReleaseTemplateItem> ByCode = Items.ToDictionary(i => i.Code, StringComparer.OrdinalIgnoreCase);

    public static ReleaseTemplateItem? Find(string code) => ByCode.GetValueOrDefault(code?.Trim() ?? string.Empty);
}

/// <summary>檢核表尚未通過的項目統計。</summary>
public sealed record ReleaseOpenItems(IReadOnlyList<string> Blocking, IReadOnlyList<string> Warning);

public readonly record struct ReleaseSignVerdict(RuleResult Result, IReadOnlyList<string>? BlockingCodes = null)
{
    public bool IsOk => Result.IsOk;
}

/// <summary>
/// 上線檢核規則（SOP 附錄 A、3.3 Release Gate），純函式：
/// 阻斷項須全數 Pass（或標示不適用並附說明）；警告項未完成須附例外備註；
/// 簽核順序為交叉驗證（不得為主要作者）→ Tech Lead 放行 → PM 確認；放行後內容鎖定。
/// </summary>
public static class ReleaseChecklistRules
{
    public static bool IsDone(ReleaseItemResult result) => result is ReleaseItemResult.Pass or ReleaseItemResult.NotApplicable;

    public static ReleaseOpenItems OpenItems(IEnumerable<(string Code, ReleaseSeverity Severity, ReleaseItemResult Result)> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var open = items.Where(i => !IsDone(i.Result)).OrderBy(i => i.Code, StringComparer.Ordinal).ToList();
        return new ReleaseOpenItems(
            open.Where(i => i.Severity == ReleaseSeverity.Blocking).Select(i => i.Code).ToList(),
            open.Where(i => i.Severity == ReleaseSeverity.Warning).Select(i => i.Code).ToList());
    }

    /// <summary>更新檢核項：僅檢核中可改；結果須為 Pass／Fail／NotApplicable；阻斷項標示不適用須說明原因；Fail 建議附說明。</summary>
    public static string? ValidateItemUpdate(ReleaseChecklistStatus status, ReleaseSeverity severity, ReleaseItemResult result, string? note)
    {
        if (status != ReleaseChecklistStatus.Draft) return "檢核表已放行，內容鎖定。";
        if (result == ReleaseItemResult.Pending) return "結果須為通過、未通過或不適用。";
        if (!Enum.IsDefined(result)) return "結果不正確。";
        if (result == ReleaseItemResult.NotApplicable && severity == ReleaseSeverity.Blocking && string.IsNullOrWhiteSpace(note))
            return "阻斷項標示不適用須填寫原因。";
        if (note is { Length: > 1000 }) return "備註不得超過 1000 字。";
        return null;
    }

    public static ReleaseSignVerdict ValidateSign(
        ReleaseSignRole role,
        Guid actorUserId,
        IReadOnlyList<(Guid UserId, RaciRole Role)> members,
        Guid mainAuthorUserId,
        ReleaseChecklistStatus status,
        IReadOnlyCollection<ReleaseSignRole> alreadySigned,
        ReleaseOpenItems open,
        string? exceptionNote)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(alreadySigned);
        ArgumentNullException.ThrowIfNull(open);

        if (alreadySigned.Contains(role))
            return Fail(RuleOutcome.InvalidTransition, "此角色已簽核。");

        switch (role)
        {
            case ReleaseSignRole.CrossVerifier:
                if (status != ReleaseChecklistStatus.Draft)
                    return Fail(RuleOutcome.InvalidTransition, "檢核表已放行，不可再簽核交叉驗證。");
                if (!members.Any(m => m.UserId == actorUserId && m.Role is RaciRole.Be or RaciRole.Fe or RaciRole.TechLead))
                    return Fail(RuleOutcome.RoleNotAllowed, "交叉驗證須由專案 BE／FE／Tech Lead 擔任。");
                if (actorUserId == mainAuthorUserId)
                    return Fail(RuleOutcome.SeparationOfDuties, "交叉驗證工程師不得為本次變更的主要作者。");
                return Ok();

            case ReleaseSignRole.TechLead:
                if (status != ReleaseChecklistStatus.Draft)
                    return Fail(RuleOutcome.InvalidTransition, "檢核表已放行。");
                if (!ProjectAccessPolicy.IsTechLead(actorUserId, members))
                    return Fail(RuleOutcome.RoleNotAllowed, "僅專案 Tech Lead 可放行。");
                if (!alreadySigned.Contains(ReleaseSignRole.CrossVerifier))
                    return Fail(RuleOutcome.InvalidTransition, "須先完成交叉驗證簽核，Tech Lead 才能放行。");
                if (open.Blocking.Count > 0)
                    return new ReleaseSignVerdict(RuleResult.Fail(RuleOutcome.GatePrerequisiteNotMet, "尚有阻斷項未通過。"), open.Blocking);
                if (open.Warning.Count > 0 && string.IsNullOrWhiteSpace(exceptionNote))
                    return Fail(RuleOutcome.InvalidTransition, $"警告項（{string.Join("、", open.Warning)}）未完成，須填寫例外放行備註（原因與預計補齊日）。");
                return Ok();

            case ReleaseSignRole.Pm:
                if (status != ReleaseChecklistStatus.Released)
                    return Fail(RuleOutcome.InvalidTransition, "須待 Tech Lead 放行後，PM 才能確認。");
                if (!ProjectAccessPolicy.IsPm(actorUserId, members))
                    return Fail(RuleOutcome.RoleNotAllowed, "僅專案 PM 可確認。");
                return Ok();

            default:
                return Fail(RuleOutcome.InvalidTransition, "簽核角色不正確。");
        }
    }

    /// <summary>使用者在此檢核表上目前可以簽核的角色（僅依狀態與身分，不含阻斷項檢查；供 UI 顯示按鈕）。</summary>
    public static IReadOnlyList<ReleaseSignRole> SignableRoles(
        Guid userId, IReadOnlyList<(Guid UserId, RaciRole Role)> members, Guid mainAuthorUserId,
        ReleaseChecklistStatus status, IReadOnlyCollection<ReleaseSignRole> alreadySigned)
    {
        var roles = new List<ReleaseSignRole>();
        var noOpen = new ReleaseOpenItems([], []);
        foreach (var role in Enum.GetValues<ReleaseSignRole>())
        {
            if (ValidateSign(role, userId, members, mainAuthorUserId, status, alreadySigned, noOpen, "x").IsOk) roles.Add(role);
        }

        return roles;
    }

    private static ReleaseSignVerdict Ok() => new(RuleResult.Ok());
    private static ReleaseSignVerdict Fail(RuleOutcome outcome, string detail) => new(RuleResult.Fail(outcome, detail));
}
