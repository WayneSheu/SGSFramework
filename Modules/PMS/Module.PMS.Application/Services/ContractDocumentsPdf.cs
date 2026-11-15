using System.Globalization;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SGS.Modules.PMS.Application.Contracts;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 合約內部文件 PDF（A4，頁尾標示「內部文件」）：審約檢核表（含偏離旗標與產業合規）、附件一《交付項目清單》與附件二《SLA 分級表》、合約履約摘要。
/// 僅合約成員可匯出；字型用 Noto Sans TC（可子集化）。
/// </summary>
public static class ContractDocumentsPdf
{
    private const string FontFamily = "Noto Sans TC";
    private const string FallbackFontFamily = "Microsoft JhengHei";
    private static readonly string HeaderBlue = Colors.Blue.Darken3;

    static ContractDocumentsPdf()
    {
        Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    private static string D(DateOnly? v) => v?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—";

    private static string M(decimal v) => v.ToString("N0", CultureInfo.InvariantCulture);

    public static string FileName(string kind, string contractNo, DateOnly date) => $"{date:yyyyMMdd}_{contractNo}_{kind}.pdf";

    public static byte[] ReviewChecklist(ContractDto c, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(c);

        return Build($"審約檢核表", c, generatedAt, col =>
        {
            col.Item().Text($"客戶：{c.Terms.CustomerName}　狀態：{c.Status}　核決週期：第 {c.ApprovalCycle} 輪").FontSize(10);
            if (!string.IsNullOrEmpty(c.SnapshotHash)) col.Item().Text($"送審快照 SHA-256：{c.SnapshotHash}").FontSize(8).FontColor(Colors.Grey.Darken1);

            if (c.Flags.Count > 0)
            {
                col.Item().PaddingTop(4).Text("偏離與風險旗標").SemiBold().FontColor(HeaderBlue);
                foreach (var f in c.Flags) col.Item().Text($"• [{f.Severity}] {f.Message}").FontSize(10);
            }

            col.Item().PaddingTop(4).Text("七項審閱結果").SemiBold().FontColor(HeaderBlue);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(x => { x.ConstantColumn(80); x.RelativeColumn(2); x.ConstantColumn(60); x.ConstantColumn(70); x.RelativeColumn(2); });
                foreach (var h in new[] { "項目", "甲方原條款", "乙方立場", "結果", "備註" }) t.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).Padding(4).Text(h).SemiBold().FontSize(9);
                foreach (var i in c.ReviewItems)
                {
                    Cell(t, i.Name);
                    Cell(t, i.ClientClause);
                    Cell(t, i.Stance.ToString());
                    Cell(t, i.Result.ToString());
                    Cell(t, i.Note);
                }
            });

            if (c.ComplianceItems is { Count: > 0 })
            {
                col.Item().PaddingTop(4).Text("產業合規清單").SemiBold().FontColor(HeaderBlue);
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(x => { x.RelativeColumn(2); x.ConstantColumn(70); x.RelativeColumn(2); });
                    foreach (var h in new[] { "項目", "狀態", "備註" }) t.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).Padding(4).Text(h).SemiBold().FontSize(9);
                    foreach (var i in c.ComplianceItems)
                    {
                        Cell(t, i.Name);
                        Cell(t, i.Status.ToString());
                        Cell(t, i.Note);
                    }
                });
            }

            if (c.Approvals.Count > 0)
            {
                col.Item().PaddingTop(4).Text("核決歷程").SemiBold().FontColor(HeaderBlue);
                foreach (var a in c.Approvals)
                    col.Item().Text($"• 第 {a.Cycle} 輪 {a.Step}：{a.Decision}（{a.DecidedByName}，{a.DecidedAt.ToOffset(TimeSpan.FromHours(8)):yyyy-MM-dd HH:mm}）{(string.IsNullOrWhiteSpace(a.Comment) ? string.Empty : "　" + a.Comment)}").FontSize(10);
            }
        });
    }

    public static byte[] Appendices(ContractDto c, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(c);

        return Build("附件一與附件二", c, generatedAt, col =>
        {
            col.Item().Text("附件一　交付項目清單").SemiBold().FontColor(HeaderBlue);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(x => { x.ConstantColumn(60); x.RelativeColumn(2); x.RelativeColumn(); x.RelativeColumn(); x.ConstantColumn(62); x.ConstantColumn(45); });
                foreach (var h in new[] { "階段", "項目", "形式", "確認方式", "指定交付日", "甲方簽認" }) t.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).Padding(4).Text(h).SemiBold().FontSize(9);
                foreach (var i in c.DeliverableItems.OrderBy(i => i.SortOrder))
                {
                    Cell(t, i.Stage);
                    Cell(t, i.Name);
                    Cell(t, i.Format);
                    Cell(t, i.AcceptMethod);
                    Cell(t, D(i.DeliveryDate));
                    Cell(t, i.RequiresCustomerSignoff ? "是" : "否");
                }
            });

            col.Item().PaddingTop(10).Text("附件二　SLA 分級表").SemiBold().FontColor(HeaderBlue);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(x => { x.ConstantColumn(40); x.RelativeColumn(); x.RelativeColumn(2); x.ConstantColumn(60); x.RelativeColumn(); x.ConstantColumn(80); });
                foreach (var h in new[] { "等級", "名稱", "定義", "回應(時)", "目標", "處理期限" }) t.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).Padding(4).Text(h).SemiBold().FontSize(9);
                foreach (var s in c.SlaLevels.OrderBy(s => s.Level))
                {
                    Cell(t, $"L{s.Level}");
                    Cell(t, s.Name);
                    Cell(t, s.Definition);
                    Cell(t, s.ResponseHours.ToString("0.##", CultureInfo.InvariantCulture));
                    Cell(t, s.Target);
                    Cell(t, s.DueHours is { } h ? $"{h:0.##} 小時" : s.DueWorkdays is { } w ? $"{w} 個工作日" : "沿用系統預設");
                }
            });
        });
    }

    public static byte[] FulfillmentSummary(ContractDto c, FulfillmentDto f, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(f);

        return Build("履約摘要", c, generatedAt, col =>
        {
            var s = f.Summary;
            col.Item().Text("金額與交期").SemiBold().FontColor(HeaderBlue);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(x => { x.ConstantColumn(130); x.RelativeColumn(); });
                void Row(string l, string v)
                {
                    t.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).Padding(4).Text(l).SemiBold().FontSize(10);
                    t.Cell().Border(0.5f).Padding(4).Text(v).FontSize(10);
                }

                Row("原合約金額", M(s.OriginalAmount));
                Row("變更累計", M(s.AmendmentTotal));
                Row("現行金額", M(s.CurrentAmount));
                Row("已請款／已收款", $"{M(s.InvoicedAmount)} ／ {M(s.PaidAmount)}");
                Row("原交期 → 現行交期", $"{D(s.OriginalDelivery)} → {D(s.EffectiveDelivery)}（協力展延 {s.ExtensionWorkdays} 個工作日）");
                Row("保固", $"{s.CurrentWarrantyMonths} 個月（至 {D(s.WarrantyEnd)}{(s.WarrantyDaysLeft is { } d ? $"，剩 {d} 天" : string.Empty)}）");
                Row("驗收", $"{f.Acceptance.Status}（交付 {D(f.Acceptance.DeliveredDate)}，驗收 {D(f.Acceptance.AcceptedDate)}）");
            });

            col.Item().PaddingTop(6).Text("甲方協力事項").SemiBold().FontColor(HeaderBlue);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(x => { x.RelativeColumn(2); x.ConstantColumn(70); x.ConstantColumn(70); x.ConstantColumn(60); x.ConstantColumn(50); });
                foreach (var h in new[] { "事項", "應完成日", "實際完成", "遲延(工作日)", "通知書" }) t.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).Padding(4).Text(h).SemiBold().FontSize(9);
                foreach (var i in f.CoopItems)
                {
                    Cell(t, i.Title);
                    Cell(t, D(i.DueDate));
                    Cell(t, D(i.CompletedDate));
                    Cell(t, i.DelayWorkdays.ToString(CultureInfo.InvariantCulture));
                    Cell(t, i.NoticeCount.ToString(CultureInfo.InvariantCulture));
                }
            });

            col.Item().PaddingTop(6).Text("付款期程").SemiBold().FontColor(HeaderBlue);
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(x => { x.RelativeColumn(2); x.ConstantColumn(50); x.ConstantColumn(80); x.ConstantColumn(70); x.ConstantColumn(80); });
                foreach (var h in new[] { "期別", "比例%", "金額", "預計日", "狀態" }) t.Cell().Background(Colors.Grey.Lighten3).Border(0.5f).Padding(4).Text(h).SemiBold().FontSize(9);
                foreach (var p in f.Payments)
                {
                    Cell(t, p.Name);
                    Cell(t, p.Percent.ToString("0.##", CultureInfo.InvariantCulture));
                    Cell(t, M(p.Amount));
                    Cell(t, D(p.PlannedDate));
                    Cell(t, p.Status.ToString());
                }
            });

            if (f.Amendments.Count > 0)
            {
                col.Item().PaddingTop(6).Text("合約變更").SemiBold().FontColor(HeaderBlue);
                foreach (var a in f.Amendments)
                    col.Item().Text($"• {D(a.EffectiveDate)} {a.CrNo ?? "手動"}　{a.Title}　金額 {M(a.AmountDelta)}{(a.NewDeliveryDate is { } nd ? $"　新交期 {D(nd)}" : string.Empty)}{(a.WarrantyMonthsDelta != 0 ? $"　保固 {a.WarrantyMonthsDelta:+0;-0} 月" : string.Empty)}").FontSize(10);
            }
        });
    }

    private static void Cell(TableDescriptor t, string? text) =>
        t.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4).Text(string.IsNullOrWhiteSpace(text) ? "—" : text.Trim()).FontSize(9);

    private static byte[] Build(string title, ContractDto c, DateTimeOffset generatedAt, Action<ColumnDescriptor> body)
    {
        var generated = generatedAt.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.8f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontFamily(FontFamily).Fallback(y => y.FontFamily(FallbackFontFamily)).FontSize(10).FontColor(Colors.Grey.Darken4).LineHeight(1.4f));

            page.Header().Column(col =>
            {
                col.Item().Text(title).FontSize(18).Bold().FontColor(HeaderBlue);
                col.Item().PaddingTop(2).Text($"{c.ContractNo}　{c.Terms.Title}").SemiBold();
                col.Item().PaddingTop(5).LineHorizontal(1).LineColor(HeaderBlue);
            });

            page.Content().PaddingVertical(10).Column(col =>
            {
                col.Spacing(5);
                body(col);
            });

            page.Footer().Row(row =>
            {
                row.RelativeItem().Text($"{generated}（UTC+8）產生　內部文件，請勿外流").FontSize(8).FontColor(Colors.Grey.Darken1);
                row.AutoItem().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" / ").FontSize(8);
                    t.TotalPages().FontSize(8);
                });
            });
        })).GeneratePdf();
    }
}

/// <summary>合約文件匯出：先經 <see cref="ContractService.GetAsync"/> 與履約服務取得成員可見資料（非成員 404），再產生 PDF。</summary>
public sealed class ContractExportService(ContractService contracts, ContractFulfillmentService fulfillment, TimeProvider clock)
{
    public async Task<PmsResult<NoticePdfFile>> ExportAsync(Guid userId, Guid contractId, string kind, CancellationToken ct)
    {
        var contract = await contracts.GetAsync(userId, contractId, ct).ConfigureAwait(false);
        if (contract.Error is { } error) return error;

        var c = contract.Value!;
        var now = clock.GetUtcNow();
        var date = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(8)).DateTime);

        switch (kind)
        {
            case "review-checklist":
                return PmsResult<NoticePdfFile>.Success(new(ContractDocumentsPdf.ReviewChecklist(c, now), ContractDocumentsPdf.FileName("審約檢核表", c.ContractNo, date)));
            case "appendices":
                return PmsResult<NoticePdfFile>.Success(new(ContractDocumentsPdf.Appendices(c, now), ContractDocumentsPdf.FileName("附件一二", c.ContractNo, date)));
            case "fulfillment-summary":
                var f = await fulfillment.GetAsync(userId, contractId, ct).ConfigureAwait(false);
                if (f.Error is { } fe) return fe;
                return PmsResult<NoticePdfFile>.Success(new(ContractDocumentsPdf.FulfillmentSummary(c, f.Value!, now), ContractDocumentsPdf.FileName("履約摘要", c.ContractNo, date)));
            default:
                return PmsError.Validation("不支援的匯出類型。");
        }
    }
}
