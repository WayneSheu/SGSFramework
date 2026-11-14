using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>RCA PDF 的內容（不含任何人員姓名；撰寫原則：以團隊為主詞）。</summary>
public sealed record RcaPdfData(
    string ProjectCode, string ProjectName, string TicketNo, string TicketTitle, RcaTrigger Trigger,
    DateTimeOffset RecoveredAt, DateTimeOffset DueAt, string Summary, IReadOnlyList<RcaTimelineItem> Timeline,
    string RootCause, string ShortTermAction, string LongTermPrevention,
    string StatusLabel, DateTimeOffset GeneratedAt, bool Official);

/// <summary>
/// P1 重大異常分析報告 PDF（FORM-RCA-01，SOP 7.3）：事件資訊、事件摘要、精確時間軸、根本原因、短期止血與長期預防。
/// 字型用 Noto Sans TC（可子集化，PDF 僅數十 KB），微軟正黑體為 TTC 會整檔嵌入約 14 MB，僅作後備。
/// 非正式版本（草稿／待寄送預覽）頁首標示「內部預覽」，寄給客戶的版本為正式版。
/// </summary>
public static class RcaReportPdf
{
    private const string FontFamily = "Noto Sans TC";
    private const string FallbackFontFamily = "Microsoft JhengHei";
    private static readonly string HeaderBlue = Colors.Blue.Darken3;
    private static readonly string GridGrey = Colors.Grey.Lighten2;

    static RcaReportPdf()
    {
        Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    public static byte[] Render(RcaPdfData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var generated = Taipei(data.GeneratedAt);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).Fallback(y => y.FontFamily(FallbackFontFamily)).FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Column(col =>
                {
                    col.Item().Text("重大異常分析報告（RCA）").FontSize(20).Bold().FontColor(HeaderBlue);
                    col.Item().PaddingTop(4).Text($"{data.ProjectCode}　{data.ProjectName}").FontSize(12).SemiBold();
                    if (!data.Official)
                        col.Item().PaddingTop(2).Text($"內部預覽（{data.StatusLabel}）— 非正式版本，請勿對外提供").FontSize(10).Bold().FontColor(Colors.Red.Darken2);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(HeaderBlue);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(1); c.RelativeColumn(3); });
                        Info(table, "單號", $"{data.TicketNo}　{data.TicketTitle}");
                        Info(table, "觸發條件", TriggerLabel(data.Trigger));
                        Info(table, "系統恢復時間", $"{Taipei(data.RecoveredAt)}（UTC+8）");
                        Info(table, "提交期限", $"{Taipei(data.DueAt)}（恢復後 48 小時內）");
                    });

                    Section(col, "一、事件摘要", data.Summary);

                    col.Item().Text("二、精確時間軸").FontSize(13).Bold().FontColor(HeaderBlue);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.ConstantColumn(120); c.RelativeColumn(); });
                        table.Header(h =>
                        {
                            HeaderCell(h.Cell(), "時間（UTC+8）");
                            HeaderCell(h.Cell(), "事件");
                        });

                        foreach (var item in data.Timeline.OrderBy(t => t.At))
                        {
                            table.Cell().BorderBottom(1).BorderColor(GridGrey).Padding(5).Text(Taipei(item.At));
                            table.Cell().BorderBottom(1).BorderColor(GridGrey).Padding(5).Text(item.Event);
                        }
                    });

                    Section(col, "三、根本原因", data.RootCause);
                    Section(col, "四、短期止血措施", data.ShortTermAction);
                    Section(col, "五、長期預防措施", data.LongTermPrevention);
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span($"{data.TicketNo}　產生時間 {generated}（UTC+8）　").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" / ").FontSize(8);
                    t.TotalPages().FontSize(8);
                });
            });
        }).GeneratePdf();
    }

    private static void Section(ColumnDescriptor col, string title, string body)
    {
        col.Item().Text(title).FontSize(13).Bold().FontColor(HeaderBlue);
        col.Item().Text(string.IsNullOrWhiteSpace(body) ? "—" : body);
    }

    private static void Info(TableDescriptor table, string label, string value)
    {
        table.Cell().BorderBottom(1).BorderColor(GridGrey).Padding(5).Text(label).SemiBold();
        table.Cell().BorderBottom(1).BorderColor(GridGrey).Padding(5).Text(value);
    }

    private static void HeaderCell(IContainer cell, string text) =>
        cell.Background(HeaderBlue).Padding(5).Text(text).FontColor(Colors.White).SemiBold();

    private static string Taipei(DateTimeOffset value) => value.ToOffset(SlaCalculator.Offset).ToString("yyyy-MM-dd HH:mm");

    private static string TriggerLabel(RcaTrigger trigger) => trigger switch
    {
        RcaTrigger.P1 => "P1 重大異常",
        RcaTrigger.Downgrade => "插件降級／回滾",
        _ => "處理時間超過合約 SLA"
    };
}
