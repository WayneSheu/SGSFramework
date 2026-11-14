using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 月度維運報告 PDF（SOP 7.2：總進線單數、SLA 準時達成率、MTTR、問題類型佔比），QuestPDF 產出單份 A4。
/// 字型用 Noto Sans TC（可子集化，PDF 僅數十 KB），微軟正黑體為 TTC 會整檔嵌入約 14 MB，僅作後備。
/// </summary>
public static class MonthlyReportPdf
{
    private const string FontFamily = "Noto Sans TC";
    private const string FallbackFontFamily = "Microsoft JhengHei";
    private static readonly string HeaderBlue = Colors.Blue.Darken3;
    private static readonly string GridGrey = Colors.Grey.Lighten2;

    static MonthlyReportPdf()
    {
        Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    public static byte[] Render(string projectCode, string projectName, MonthlyStats stats, int revision, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(stats);

        var generated = generatedAt.ToOffset(SlaCalculator.Offset).ToString("yyyy-MM-dd HH:mm");
        var asOf = stats.AsOf.ToOffset(SlaCalculator.Offset).ToString("yyyy-MM-dd HH:mm");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).Fallback(y => y.FontFamily(FallbackFontFamily)).FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Column(col =>
                {
                    col.Item().Text("月度維運報告").FontSize(22).Bold().FontColor(HeaderBlue);
                    col.Item().PaddingTop(4).Text($"{projectCode}　{projectName}").FontSize(12).SemiBold();
                    col.Item().Text($"統計月份：{stats.Period}（台灣時間）").FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(HeaderBlue);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Text("一、服務指標").FontSize(13).Bold().FontColor(HeaderBlue);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(3); });
                        table.Header(h =>
                        {
                            Header(h.Cell(), "指標");
                            Header(h.Cell(), "數值");
                            Header(h.Cell(), "說明");
                        });

                        Row(table, "總進線單數", $"{stats.TotalIncoming} 張", "當月受理（不含已取消）");
                        Row(table, "SLA 準時達成率",
                            stats.SlaOnTimeRate is { } r ? $"{r * 100:0.#}%（{stats.SlaMetOnTimeCount} / {stats.SlaApplicableCount}）" : "—",
                            "準時完成 ÷（準時＋超時完成＋已超時未結）；進行中不計");
                        Row(table, "平均修復時間（MTTR）",
                            stats.MttrHours is { } m ? $"{m:0.##} 小時" : "—", "當月修復完成的系統異常單，受理到修復的平均時數");
                        Row(table, "已逾期未結案", $"{stats.OverdueCount} 張", $"統計基準時點 {asOf}");
                        Row(table, "2 小時內到期", $"{stats.DueSoonCount} 張", $"統計基準時點 {asOf}");
                    });

                    col.Item().Text("二、問題類型佔比").FontSize(13).Bold().FontColor(HeaderBlue);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(1); });
                        table.Header(h =>
                        {
                            Header(h.Cell(), "類型");
                            Header(h.Cell(), "單數");
                            Header(h.Cell(), "佔比");
                        });

                        foreach (var t in stats.TypeRatio)
                        {
                            var pct = stats.TotalIncoming == 0 ? "—" : $"{(decimal)t.Count / stats.TotalIncoming * 100:0.#}%";
                            Row(table, TypeLabel(t.Type), t.Count.ToString(), pct);
                        }
                    });

                    col.Item().PaddingTop(6).Text("本報告由專案管理系統依問題單資料產生，PM 審閱後轉交客戶備查。").FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span($"第 {revision} 版　產生時間 {generated}（UTC+8）　").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" / ").FontSize(8);
                    t.TotalPages().FontSize(8);
                });
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer cell, string text) =>
        cell.Background(HeaderBlue).Padding(6).Text(text).FontColor(Colors.White).SemiBold();

    private static void Row(TableDescriptor table, params string[] cells)
    {
        foreach (var text in cells)
            table.Cell().BorderBottom(1).BorderColor(GridGrey).Padding(6).Text(text);
    }

    private static string TypeLabel(TicketType type) => type switch
    {
        TicketType.Consultation => "操作諮詢",
        TicketType.ChangeRequest => "需求變更（CR）",
        TicketType.Incident => "系統異常",
        _ => "資料撈取"
    };
}
