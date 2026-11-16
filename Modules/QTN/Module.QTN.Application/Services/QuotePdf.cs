using System.Globalization;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SGS.Modules.QTN.Application.Contracts;
using SGS.Modules.QTN.Application.Rules;
using SGS.Modules.QTN.Infrastructure.Entities;

namespace SGS.Modules.QTN.Application.Services;

/// <summary>
/// 報價 PDF（A4，Noto Sans TC）：
/// 1. 對外報價單——依四大範本版型（固定總價／敏捷產能包／混合計價／月費維運），只使用牌價與成交價，絕不含成本與毛利；
/// 2. 專案工時估算表（內部，無財務金額）；
/// 3. 毛利率報告（內部，限 QTN.COST.READ）。
/// 輸入為已依欄位級權限投影的 <see cref="QuoteDto"/>，呼叫端須先確認權限。
/// </summary>
public static class QuotePdf
{
    private const string FontFamily = "Noto Sans TC";
    private const string FallbackFontFamily = "Microsoft JhengHei";
    private static readonly string HeaderBlue = Colors.Blue.Darken3;

    static QuotePdf()
    {
        Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    private static string M(decimal? v) => v is { } d ? d.ToString("N0", CultureInfo.InvariantCulture) : "—";

    private static string Dy(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string D(DateOnly? v) => v?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—";

    public static string FileName(QuoteDto q, string kind) => $"{q.QuoteNo}_{q.VersionLabel}_{kind}.pdf";

    // ======================= 對外報價單 =======================

    public static byte[] CustomerQuotation(QuoteDto q, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(q);
        if (!q.Access.SeesPrice()) throw new InvalidOperationException("對外報價單需價格檢視權限。");

        var official = q.Status is QuoteStatus.Approved or QuoteStatus.Issued or QuoteStatus.Won;
        var title = q.PricingModel switch
        {
            PricingModel.TimeAndMaterials => "敏捷開發團隊產能報價單（AGILE T&M QUOTATION）",
            PricingModel.Hybrid => "系統重構與遷移專案報價單（HYBRID QUOTATION）",
            PricingModel.Retainer => "系統維運與技術支援服務報價單（RETAINER QUOTATION）",
            _ => "專案開發報價單（FIXED PRICE QUOTATION）"
        };
        var values = ClauseValues(q);

        return Build(title, q, generatedAt, official ? null : "草稿／未核准版本，不得對外提供", "本報價單內容為機密商業資訊", col =>
        {
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(x => { x.ConstantColumn(90); x.RelativeColumn(); x.ConstantColumn(90); x.RelativeColumn(); });
                Kv(t, "報價單編號", $"{q.QuoteNo}（版本：{q.VersionLabel}）");
                Kv(t, "報價有效期", q.ValidUntil is { } v ? $"至 {D(v)} 止" : $"自出具日起 {q.Terms.ValidDays} 個日曆天");
                Kv(t, "客戶名稱", q.CustomerName);
                Kv(t, "專案名稱", q.Title);
                Kv(t, "計價模式", StrategyRules.ModelName(q.PricingModel));
                Kv(t, "出具日", D(q.IssuedDate));
            });

            switch (q.PricingModel)
            {
                case PricingModel.TimeAndMaterials: AgileBody(col, q); break;
                case PricingModel.Hybrid: HybridBody(col, q); break;
                case PricingModel.Retainer: RetainerBody(col, q); break;
                default: FixedPriceBody(col, q); break;
            }

            if (q.Milestones.Count > 0)
            {
                Section(col, q.PricingModel == PricingModel.Retainer ? "請款方式" : "付款里程碑");
                foreach (var m in q.Milestones)
                    col.Item().Text($"• {m.Name}（{m.Percent:0.##}% ／ NT$ {M(m.AmountWithTax)} 含稅）：{m.Trigger}").FontSize(9.5f);
            }

            if (q.SlaLevels.Count > 0)
            {
                Section(col, "服務水準協議（SLA）");
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(x => { x.ConstantColumn(40); x.RelativeColumn(3); x.RelativeColumn(); x.RelativeColumn(); });
                    Head(t, "等級", "定義說明", "首次回應時間", "目標解決／緩解時間");
                    foreach (var s in q.SlaLevels) { Cell(t, s.Level); Cell(t, s.Definition); Cell(t, s.ResponseTime); Cell(t, s.ResolveTarget); }
                });
            }

            if (q.Clauses.Count > 0)
            {
                Section(col, "商業條款");
                var n = 0;
                foreach (var c in q.Clauses)
                    col.Item().Text($"{++n}. {c.Title}：{ClauseCatalog.Render(c.Body, values)}").FontSize(9.5f);
            }
        });
    }

    private static void FixedPriceBody(ColumnDescriptor col, QuoteDto q)
    {
        Section(col, "專案範圍與工時費用明細（WBS）");
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(x => { x.ConstantColumn(34); x.RelativeColumn(1.4f); x.RelativeColumn(2.2f); x.RelativeColumn(1.2f); x.ConstantColumn(46); x.ConstantColumn(58); x.ConstantColumn(70); });
            Head(t, "項目", "模組／階段名稱", "核心交付物說明", "投入角色", "預估人天", "牌價單價", "小計（未稅）");
            foreach (var w in q.Wbs.Where(w => w.ParentId is null))
                WbsRow(t, q, w);
            foreach (var e in q.Expenses)
            {
                Cell(t, "—"); Cell(t, ExpenseName(e.Category)); Cell(t, e.Description + (e.IsCapped ? "（實報實銷上限）" : string.Empty));
                Cell(t, "直接費用"); Cell(t, "—"); Cell(t, "—"); CellR(t, M(e.Amount));
            }
        });
        Totals(col, q);
    }

    private static void WbsRow(TableDescriptor t, QuoteDto q, WbsItemDto w)
    {
        var subtree = Descendants(q, w.Id).Prepend(w).ToList();
        var roles = subtree.SelectMany(x => x.Allocations).Select(a => a.RoleName).Distinct().ToList();
        var deliverable = w.Deliverable ?? string.Join("、", q.Wbs.Where(c => c.ParentId == w.Id).Select(c => c.Name));
        Cell(t, w.WbsCode); Cell(t, w.Name); Cell(t, deliverable); Cell(t, string.Join(" / ", roles));
        CellR(t, $"{Dy(w.QuotedDays)} 天");
        CellR(t, w.QuotedDays > 0 && w.ListAmount is { } amt ? M(Math.Round(amt / w.QuotedDays, 0)) : "—");
        CellR(t, M(w.ListAmount));
    }

    private static void AgileBody(ColumnDescriptor col, QuoteDto q)
    {
        var sprints = q.Terms.SprintCount ?? 1;
        Section(col, $"專屬開發團隊配置與雙週費率（最短合約期：{sprints} 個 Sprints，每 Sprint 為 10 個工作日）");
        var roles = RoleSummary(q);
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(x => { x.RelativeColumn(1.5f); x.ConstantColumn(56); x.ConstantColumn(62); x.ConstantColumn(60); x.ConstantColumn(80); x.RelativeColumn(2); });
            Head(t, "投入角色", "配置比例(FTE)", "單一 Sprint 人天", "牌價單價", "單一 Sprint 費用（未稅）", "角色核心職責");
            foreach (var r in roles)
            {
                Cell(t, r.Role); CellR(t, $"{Math.Round(r.Days / 10m * 100m, 0)}%"); CellR(t, $"{Dy(r.Days)} 天"); CellR(t, M(r.Rate)); CellR(t, M(r.Amount)); Cell(t, r.Duties);
            }
        });
        var perSprint = q.Totals.PeriodListPrice;
        col.Item().PaddingTop(4).Text($"單一 Sprint（2 週）產能包價格（未稅）：NT$ {M(perSprint)}").SemiBold();
        Totals(col, q, $"基礎合約期（{sprints} 個 Sprints）預估總價");
    }

    private static void HybridBody(ColumnDescriptor col, QuoteDto q)
    {
        var stageOne = q.Wbs.Where(w => w.Stage == 1 && w.Allocations.Count > 0).ToList();
        var stageTwo = q.Wbs.Where(w => w.Stage == 2 && w.Allocations.Count > 0).ToList();

        Section(col, "第一階段：系統架構健檢與遷移藍圖規劃（固定總價）");
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(x => { x.ConstantColumn(34); x.RelativeColumn(1.4f); x.RelativeColumn(2.2f); x.RelativeColumn(1.2f); x.ConstantColumn(50); x.ConstantColumn(70); });
            Head(t, "項目", "交付物名稱", "交付內容說明", "投入角色", "預估人天", "小計（未稅）");
            foreach (var w in stageOne)
            {
                Cell(t, w.WbsCode); Cell(t, w.Name); Cell(t, w.Deliverable); Cell(t, string.Join(" / ", w.Allocations.Select(a => a.RoleName)));
                CellR(t, $"{Dy(w.QuotedDays)} 天"); CellR(t, M(w.ListAmount));
            }
        });
        col.Item().PaddingTop(2).Text($"第一階段固定總價（未稅）：NT$ {M(stageOne.Sum(w => w.ListAmount ?? 0))}").SemiBold();

        Section(col, "第二階段：核心重構與資料遷移執行（預估 T&M，實際依雙週工時報表實報實銷）");
        foreach (var r in stageTwo.SelectMany(w => w.Allocations).GroupBy(a => a.RoleName))
        {
            var days = r.Sum(a => a.QuotedDays);
            var amount = r.Sum(a => a.ListAmount ?? 0);
            var rate = r.First().SnapshotListRate;
            col.Item().Text($"• {r.Key}：牌價 NT$ {M(rate)} ／人天（預估 {Dy(days)} 人天，小計 NT$ {M(amount)}）").FontSize(9.5f);
        }

        col.Item().PaddingTop(2).Text($"第二階段預估總金額（未稅）：NT$ {M(stageTwo.Sum(w => w.ListAmount ?? 0))}").SemiBold();
        Totals(col, q, "兩階段合計");
    }

    private static void RetainerBody(ColumnDescriptor col, QuoteDto q)
    {
        var months = q.Terms.RetainerMonths ?? 1;
        var start = q.Terms.RetainerStart;
        Section(col, $"月費方案與標準服務額度明細{(start is { } s ? $"（合約期間：{D(s)} 起共 {months} 個月）" : $"（共 {months} 個月）")}");
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(x => { x.RelativeColumn(1.4f); x.RelativeColumn(2.6f); x.ConstantColumn(70); x.ConstantColumn(70); x.ConstantColumn(80); });
            Head(t, "服務項目", "服務內容說明", "每月包含額度", "月費單價（未稅）", "合約總計（未稅）");
            foreach (var w in q.Wbs.Where(w => w.ParentId is null))
            {
                Cell(t, w.Name); Cell(t, w.Deliverable); CellR(t, $"{Dy(w.QuotedDays)} 人天"); CellR(t, M(w.ListAmount)); CellR(t, M(w.ListAmount * months));
            }

            foreach (var e in q.Expenses)
            {
                Cell(t, ExpenseName(e.Category)); Cell(t, e.Description); CellR(t, "固定包含"); CellR(t, M(e.Amount)); CellR(t, M(e.Amount * months));
            }
        });
        col.Item().PaddingTop(4).Text($"每月固定維運費（未稅）：NT$ {M(q.Totals.PeriodListPrice)}").SemiBold();
        if (q.Terms.RetainerMonthlyDaysCap is { } cap)
            col.Item().Text($"每月標準維運人天上限 {Dy(cap)} 人天；超額按 NT$ {M(q.Terms.OverageDailyRate)} ／人天加收，國定假日或夜間非緊急變更工時按 {q.Terms.OffHoursMultiplier:0.##} 倍計算。").FontSize(9.5f);
        Totals(col, q, $"合約總金額（{months} 個月）");
    }

    private static void Totals(ColumnDescriptor col, QuoteDto q, string? finalLabel = null)
    {
        var tt = q.Totals;
        col.Item().PaddingTop(6).AlignRight().Width(300).Table(t =>
        {
            t.ColumnsDefinition(x => { x.RelativeColumn(); x.ConstantColumn(110); });
            void Row(string l, string v, bool bold = false)
            {
                t.Cell().Padding(2).AlignRight().Text(l).FontSize(10).SemiBold();
                var c = t.Cell().Padding(2).AlignRight().Text(v).FontSize(10);
                if (bold) c.Bold().FontColor(HeaderBlue);
            }

            Row("牌價總計（未稅）", $"NT$ {M(tt.ListTotal)}");
            if (tt.IpLicenseFee is > 0) Row("（含原始碼智財權授權費）", $"NT$ {M(tt.IpLicenseFee)}");
            if (tt.DiscountAmount is > 0) Row("專案合作優惠", $"－NT$ {M(tt.DiscountAmount)}");
            Row(finalLabel is null ? "成交總價（未稅）" : $"{finalLabel}（未稅）", $"NT$ {M(tt.FinalPrice)}", bold: true);
            Row($"營業稅（{tt.TaxRate:P0}）", $"NT$ {M(tt.TaxAmount)}");
            Row("含稅總價", $"NT$ {M(tt.FinalPriceWithTax)}", bold: true);
        });
    }

    // ======================= 內部：專案工時估算表 =======================

    public static byte[] EstimateSheet(QuoteDto q, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(q);
        return Build("專案工時估算表（無財務金額）", q, generatedAt, null, "內部文件，請勿外流", col =>
        {
            col.Item().Text($"客戶：{q.CustomerName}　計價模式：{StrategyRules.ModelName(q.PricingModel)}　狀態：{q.Status}").FontSize(10);
            col.Item().Text($"期望人天合計 {Dy(q.Totals.TotalExpectedDays)}；報價人天合計 {Dy(q.Totals.TotalQuotedDays)}；整體風險係數 {q.Totals.RiskFactor:0.00}").FontSize(10);
            if (q.TechReviewedAt is { } at)
                col.Item().Text($"Tech Lead 技術複核：{at.ToOffset(Workdays.TaipeiOffset):yyyy-MM-dd HH:mm}{(q.TechReviewNote is null ? string.Empty : "　" + q.TechReviewNote)}").FontSize(9);

            Section(col, "WBS 與三點估算（E＝(O＋4M＋P)／6；報價人天＝E × K技術 × K需求）");
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(x => { x.ConstantColumn(40); x.RelativeColumn(2); x.ConstantColumn(56); x.RelativeColumn(1.2f); x.ConstantColumn(30); x.ConstantColumn(30); x.ConstantColumn(30); x.ConstantColumn(34); x.ConstantColumn(30); x.ConstantColumn(30); x.ConstantColumn(42); });
                Head(t, "WBS", "名稱", "階段", "角色", "O", "M", "P", "E", "K技", "K需", "報價人天");
                foreach (var w in q.Wbs)
                {
                    if (w.Allocations.Count == 0)
                    {
                        Cell(t, w.WbsCode); Cell(t, w.Name); Cell(t, PhaseRatioRules.PhaseName(w.Phase)); Cell(t, "—");
                        Cell(t, ""); Cell(t, ""); Cell(t, ""); Cell(t, ""); Cell(t, ""); Cell(t, ""); CellR(t, Dy(w.QuotedDays));
                        continue;
                    }

                    foreach (var a in w.Allocations)
                    {
                        Cell(t, w.WbsCode); Cell(t, w.Name); Cell(t, PhaseRatioRules.PhaseName(w.Phase)); Cell(t, a.RoleName);
                        CellR(t, Dy(a.Optimistic)); CellR(t, Dy(a.MostLikely)); CellR(t, Dy(a.Pessimistic)); CellR(t, Dy(a.ExpectedDays));
                        CellR(t, w.KTech.ToString("0.0", CultureInfo.InvariantCulture)); CellR(t, w.KReq.ToString("0.0", CultureInfo.InvariantCulture)); CellR(t, Dy(a.QuotedDays));
                    }
                }
            });

            Section(col, "標準工時結構比例（SOP 4.1）");
            foreach (var r in q.Evaluation.PhaseRatios)
                col.Item().Text($"• {PhaseRatioRules.PhaseName(r.Phase)}：{Dy(r.Days)} 人天，佔 {r.Ratio:P1}（基準 {r.Min:P0}–{r.Max:P0}）{(r.InRange ? string.Empty : "　⚠ 超出基準")}").FontSize(9.5f);
            foreach (var w in q.Evaluation.Warnings) col.Item().Text($"⚠ {w}").FontSize(9).FontColor(Colors.Orange.Darken2);
        });
    }

    // ======================= 內部：毛利率報告 =======================

    public static byte[] MarginReport(QuoteDto q, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(q);
        if (!q.Access.SeesCost()) throw new InvalidOperationException("毛利率報告需成本檢視權限。");
        var tt = q.Totals;

        return Build("報價毛利率報告（機密）", q, generatedAt, null, "機密文件：含人力成本，僅限財務與決策層", col =>
        {
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(x => { x.ConstantColumn(170); x.RelativeColumn(); });
                Kv2(t, "直接人力成本", M(tt.LaborCost));
                Kv2(t, "管銷分攤（人力成本 × 18%）", M(tt.Overhead));
                Kv2(t, "專案直接費用", M(tt.DirectExpense));
                Kv2(t, "專案總成本", M(tt.TotalCost));
                Kv2(t, "牌價總計（未稅）", M(tt.ListTotal));
                Kv2(t, "折扣", M(tt.DiscountAmount));
                Kv2(t, "成交總價（未稅）", M(tt.FinalPrice));
                Kv2(t, "預估毛利率", tt.MarginRate is { } m ? m.ToString("P2", CultureInfo.InvariantCulture) : "—");
                Kv2(t, "毛利燈號", tt.MarginLight is { } l ? MarginLightPolicy.LightName(l) : "—");
                Kv2(t, "業務獎金提成係數", tt.BonusFactor?.ToString("0.0", CultureInfo.InvariantCulture) ?? "—");
                Kv2(t, "必經審批路徑", q.Evaluation.Route is { } r ? string.Join(" → ", r.Select(MarginLightPolicy.RoleName)) : "—");
            });
            if (!string.IsNullOrWhiteSpace(q.DiscountReason)) col.Item().PaddingTop(4).Text($"折扣說明：{q.DiscountReason}").FontSize(9.5f);
            if (q.SpecialApproval) col.Item().Text($"戰略客戶特批：{q.SpecialApprovalReason}").FontSize(9.5f).FontColor(Colors.Red.Darken2);

            Section(col, "角色成本明細（費率快照）");
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(x => { x.RelativeColumn(1.5f); x.ConstantColumn(56); x.ConstantColumn(66); x.ConstantColumn(66); x.ConstantColumn(76); x.ConstantColumn(76); });
                Head(t, "角色", "人天", "成本單價", "牌價", "成本小計", "牌價小計");
                foreach (var g in q.Wbs.SelectMany(w => w.Allocations).GroupBy(a => (a.RoleName, a.SnapshotCostRate, a.SnapshotListRate)))
                {
                    Cell(t, g.Key.RoleName); CellR(t, Dy(g.Sum(a => a.QuotedDays))); CellR(t, M(g.Key.SnapshotCostRate)); CellR(t, M(g.Key.SnapshotListRate));
                    CellR(t, M(g.Sum(a => a.CostAmount ?? 0))); CellR(t, M(g.Sum(a => a.ListAmount ?? 0)));
                }
            });

            if (q.ApprovalSteps.Count > 0)
            {
                Section(col, "審批歷程");
                foreach (var s in q.ApprovalSteps)
                    col.Item().Text($"• 第 {s.Cycle} 輪 第 {s.Seq} 關 {s.RoleName}：{s.State}{(s.DecidedByName is null ? string.Empty : $"（{s.DecidedByName}，{s.DecidedAt?.ToOffset(Workdays.TaipeiOffset):yyyy-MM-dd HH:mm}）")}{(s.Comment is null ? string.Empty : "　" + s.Comment)}").FontSize(9.5f);
            }

            if (q.ContentHash is not null) col.Item().PaddingTop(4).Text($"送審內容 SHA-256：{q.ContentHash}").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    // ======================= 共用 =======================

    private sealed record RoleLine(string Role, decimal Days, decimal? Rate, decimal? Amount, string Duties);

    private static List<RoleLine> RoleSummary(QuoteDto q) =>
        q.Wbs.SelectMany(w => w.Allocations.Select(a => (w, a))).GroupBy(x => x.a.RoleName)
            .Select(g => new RoleLine(g.Key, g.Sum(x => x.a.QuotedDays), g.First().a.SnapshotListRate, g.Sum(x => x.a.ListAmount ?? 0),
                string.Join("、", g.Select(x => x.w.Deliverable ?? x.w.Name).Distinct().Take(3))))
            .ToList();

    private static IEnumerable<WbsItemDto> Descendants(QuoteDto q, Guid id)
    {
        foreach (var c in q.Wbs.Where(w => w.ParentId == id))
        {
            yield return c;
            foreach (var d in Descendants(q, c.Id)) yield return d;
        }
    }

    private static IReadOnlyDictionary<string, string> ClauseValues(QuoteDto q) => new Dictionary<string, string>
    {
        ["ValidDays"] = q.Terms.ValidDays.ToString(CultureInfo.InvariantCulture),
        ["MonthlyDaysCap"] = q.Terms.RetainerMonthlyDaysCap is { } cap ? Dy(cap) : "—",
        ["OverageRate"] = M(q.Terms.OverageDailyRate),
        ["OffHoursMultiplier"] = q.Terms.OffHoursMultiplier?.ToString("0.##", CultureInfo.InvariantCulture) ?? "1.5"
    };

    private static string ExpenseName(ExpenseCategory c) => c switch
    {
        ExpenseCategory.Cloud => "雲端環境費",
        ExpenseCategory.License => "第三方授權費",
        ExpenseCategory.Travel => "駐點差旅費",
        _ => "代辦直接費用"
    };

    private static void Section(ColumnDescriptor col, string title) => col.Item().PaddingTop(8).Text(title).SemiBold().FontColor(HeaderBlue);

    private static void Head(TableDescriptor t, params string[] headers)
    {
        foreach (var h in headers) t.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).Padding(3).Text(h).SemiBold().FontSize(8.5f);
    }

    private static void Cell(TableDescriptor t, string? text) =>
        t.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).Text(string.IsNullOrWhiteSpace(text) ? "—" : text.Trim()).FontSize(8.5f);

    private static void CellR(TableDescriptor t, string? text) =>
        t.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(3).AlignRight().Text(string.IsNullOrWhiteSpace(text) ? "—" : text.Trim()).FontSize(8.5f);

    private static void Kv(TableDescriptor t, string k, string? v)
    {
        t.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).Padding(3).Text(k).SemiBold().FontSize(9);
        t.Cell().Border(0.5f).Padding(3).Text(string.IsNullOrWhiteSpace(v) ? "—" : v).FontSize(9);
    }

    private static void Kv2(TableDescriptor t, string k, string v)
    {
        t.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).Padding(3).Text(k).SemiBold().FontSize(9.5f);
        t.Cell().Border(0.5f).Padding(3).AlignRight().Text(v).FontSize(9.5f);
    }

    private static byte[] Build(string title, QuoteDto q, DateTimeOffset generatedAt, string? draftBanner, string footerNote, Action<ColumnDescriptor> body)
    {
        var generated = generatedAt.ToOffset(Workdays.TaipeiOffset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.6f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontFamily(FontFamily).Fallback(y => y.FontFamily(FallbackFontFamily)).FontSize(10).FontColor(Colors.Grey.Darken4).LineHeight(1.35f));

            page.Header().Column(col =>
            {
                col.Item().Text(title).FontSize(16).Bold().FontColor(HeaderBlue);
                col.Item().PaddingTop(2).Text($"{q.QuoteNo} {q.VersionLabel}　{q.Title}").SemiBold();
                if (draftBanner is not null) col.Item().PaddingTop(2).Text(draftBanner).FontSize(10).Bold().FontColor(Colors.Red.Darken2);
                col.Item().PaddingTop(4).LineHorizontal(1).LineColor(HeaderBlue);
            });

            page.Content().PaddingVertical(8).Column(col =>
            {
                col.Spacing(4);
                body(col);
            });

            page.Footer().Row(row =>
            {
                row.RelativeItem().Text($"{generated}（UTC+8）產生　{footerNote}").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                row.AutoItem().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(7.5f);
                    t.Span(" / ").FontSize(7.5f);
                    t.TotalPages().FontSize(7.5f);
                });
            });
        })).GeneratePdf();
    }
}

/// <summary>報價文件匯出：先經 <see cref="QuoteService.GetAsync"/> 取得依欄位級權限投影後的資料（非成員 404），再依權限產生 PDF 並留稽核軌跡。</summary>
public sealed class QuoteExportService(QuoteService quotes, Infrastructure.Dbcontexts.QTNDbContext db, TimeProvider clock)
{
    public async Task<QtnResult<QuotePdfFile>> ExportAsync(QtnCaller caller, Guid id, string kind, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var loaded = await quotes.GetAsync(caller, id, ct).ConfigureAwait(false);
        if (loaded.Error is { } error) return error;
        var q = loaded.Value!;
        var now = clock.GetUtcNow();

        QuotePdfFile file;
        switch (kind)
        {
            case "customer":
                if (!q.Access.SeesPrice()) return QtnError.Forbidden("需具報價價格檢視權限（QTN.PRICE.READ）。");
                file = new QuotePdfFile(QuotePdf.CustomerQuotation(q, now), QuotePdf.FileName(q, "報價單"));
                break;
            case "estimate":
                file = new QuotePdfFile(QuotePdf.EstimateSheet(q, now), QuotePdf.FileName(q, "工時估算表"));
                break;
            case "margin":
                if (!q.Access.SeesCost()) return QtnError.Forbidden("需具成本檢視權限（QTN.COST.READ）。");
                file = new QuotePdfFile(QuotePdf.MarginReport(q, now), QuotePdf.FileName(q, "毛利率報告"));
                break;
            default:
                return QtnError.Validation("不支援的匯出類型（customer／estimate／margin）。");
        }

        db.ApprovalLogs.Add(new Infrastructure.Entities.ApprovalLog
        {
            Id = Guid.NewGuid(), QuoteVersionId = q.Id, Action = QuoteLogAction.PdfExported, Cycle = q.ApprovalCycle, ActorUserId = caller.UserId,
            ActorName = caller.Name, IpAddress = caller.IpAddress, At = now, Message = $"匯出 {file.FileName}", ContentHash = q.ContentHash
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return QtnResult<QuotePdfFile>.Success(file);
    }
}
