using SGS.Modules.QTN.Infrastructure.Entities;

namespace SGS.Modules.QTN.Application.Rules;

public sealed record ClauseSeed(string Code, string Title, string Body);

public sealed record SlaSeed(string Level, string Definition, string ResponseTime, string ResolveTarget);

/// <summary>
/// 對外報價單商業防禦條款（SOP 附錄 A 與四大範本專屬條款）。建立報價時依計價模式帶入，草稿可編修，送審時與內容雜湊一併凍結。
/// 文字中的 {ValidDays}、{OverageRate} 等占位符於 PDF 產出時以報價參數取代。
/// </summary>
public static class ClauseCatalog
{
    private static readonly ClauseSeed Validity = new("VALIDITY", "報價有效期",
        "本報價單自出具日起 {ValidDays} 個日曆天內有效，逾期需重新評估開發排程與人力費率。");

    private static readonly ClauseSeed ChangeControl = new("CHANGE_CONTROL", "需求變更約定",
        "本報價僅涵蓋 WBS 明確列出之功能。執行期間若提出新增功能或變更已定稿之 UI／規格，雙方應另行簽署《專案變更追加協議（CR）》並依增加工時計算費用與順延工期。");

    private static readonly ClauseSeed Cooperation = new("COOPERATION", "客戶協力義務",
        "客戶需於啟動後 5 個工作日內提供所需之第三方 API 金鑰與測試環境權限。因客戶端提供資料或審核延宕導致之工期順延，不計入本公司違約責任。");

    private static readonly ClauseSeed AutoAcceptance = new("AUTO_ACCEPTANCE", "自動驗收條款",
        "本公司交付階段產出物或提出驗收申請後，客戶應於 10 個工作日內完成測試與回饋；逾期未提出書面異議者，視為該階段自動驗收通過。");

    private static readonly ClauseSeed WarrantyIp = new("WARRANTY_IP", "保固與智財權",
        "自正式驗收日起提供 6 個月免費系統瑕疵修復保固（不含新功能與第三方平台政策變更）。客戶付清全額款項後取得客製化程式碼使用權；本公司既有底層框架與通用函式庫之智財權仍歸屬本公司所有。");

    private static readonly ClauseSeed IpBuyout = new("IP_BUYOUT", "原始碼智慧財產權買斷",
        "本報價含底層核心原始碼智慧財產權買斷轉讓，已加計智慧財產權授權費；權利移轉自客戶付清全額款項之日起生效。");

    public static IReadOnlyList<ClauseSeed> Defaults(PricingModel model, bool ipBuyout)
    {
        var list = model switch
        {
            PricingModel.FixedPrice => new List<ClauseSeed> { Validity, ChangeControl, Cooperation, AutoAcceptance, WarrantyIp },

            PricingModel.TimeAndMaterials => new List<ClauseSeed>
            {
                Validity,
                new("CAPACITY_SCOPE", "產能買斷與範圍彈性",
                    "客戶於每個 Sprint 開始前 2 個工作日，擁有功能開發順序的完全決定權。開發團隊承諾全力交付該 Sprint 議定之目標故事點數（Story Points），不對未經完整規格定義的總專案功能清單做固定總價承諾。"),
                new("SPRINT_SETTLEMENT", "結算與規模調整",
                    "簽約時預付首個 Sprint 費用；後續於每個 Sprint 結束並完成成果展示（Sprint Review）後 5 個工作日內結算當期費用。如需增減團隊人力，須於下個 Sprint 開始前 10 個工作日書面通知。"),
                Cooperation, WarrantyIp
            },

            PricingModel.Hybrid => new List<ClauseSeed>
            {
                Validity,
                new("STAGE_GATE_EXIT", "階段退出權（Stage-Gate Exit）",
                    "第一階段完成並交付《系統架構健檢報告》後，雙方若對第二階段之技術方案或預算無法達成共識，任一方皆可書面終止第二階段合作，客戶無須支付任何違約金（第一階段費用不予退還）。"),
                new("LEGACY_DATA", "舊系統資料品質免責",
                    "若遷移過程中因客戶舊系統存在未記錄之損壞資料或異常業務邏輯，導致額外資料清洗（Data Cleansing）工時，將直接計入當期 T&M 工時結算。"),
                ChangeControl, Cooperation, WarrantyIp
            },

            PricingModel.Retainer => new List<ClauseSeed>
            {
                Validity,
                new("USE_IT_OR_LOSE_IT", "工時額度不遞延原則",
                    "每月包含之 {MonthlyDaysCap} 人天技術支援工時，限當月使用完畢，未用完之時數不得累計或遞延至次月，亦不得折抵月費。"),
                new("OVERAGE", "超額工時與非上班時間加乘",
                    "當月需求若超過 {MonthlyDaysCap} 人天額度，經客戶書面同意後，超出部分按 NT$ {OverageRate} ／人天加收。若客戶要求於國定假日或夜間（22:00－08:00）執行非緊急系統變更，工時按 {OffHoursMultiplier} 倍計算。")
            },

            _ => new List<ClauseSeed> { Validity, ChangeControl }
        };

        if (ipBuyout) list.Add(IpBuyout);
        return list;
    }

    public static IReadOnlyList<SlaSeed> DefaultSla(PricingModel model) => model == PricingModel.Retainer
        ?
        [
            new("P1", "緊急：正式環境完全停機、核心交易或金流中斷", "30 分鐘內", "4 小時內"),
            new("P2", "高：部分核心功能異常，但有替代方案可暫時運作", "2 小時內", "1 個工作日內"),
            new("P3", "一般：介面顯示異常、操作諮詢或功能微調需求", "1 個工作日內", "依雙方排程議定")
        ]
        : [];

    /// <summary>以報價參數取代條款占位符。</summary>
    public static string Render(string body, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var text = body ?? string.Empty;
        foreach (var (key, value) in values) text = text.Replace("{" + key + "}", value, StringComparison.Ordinal);
        return text;
    }
}
