using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SGS.Modules.RPT.Application.Pdf;

public sealed record ReportPdfResultRow(string ItemName, string? Value, string? Unit, string? LimitText, string? Method);

/// <summary>PDF 輸出資料；IsPreview 為未發行之預覽版（加註浮水印，不具效力）</summary>
public sealed record ReportPdfData(
    string ReportNo,
    int Version,
    string Title,
    string CategoryName,
    string LabName,
    string? CustomerName,
    string SampleNo,
    string? SampleDescription,
    IReadOnlyList<ReportPdfResultRow> Results,
    string? SignerName,
    string? SignerCertificateNo,
    DateTimeOffset? SignedAt,
    string? VerificationToken,
    string? VerificationUrl,
    string? SupersedesReportNo,
    bool IsPreview,
    string? WatermarkText);

/// <summary>
/// 檢驗報告 PDF 產生器。正式版於簽署時產生一次並保存（SHA-256 供真偽查驗比對）；預覽版加註浮水印。
/// 字型採 Noto Sans TC（可子集化，檔案僅數十 KB），微軟正黑體為後備。
/// </summary>
public static class ReportPdfGenerator
{
    private const string FontFamily = "Noto Sans TC";
    private const string FallbackFontFamily = "Microsoft JhengHei";

    public static byte[] Generate(ReportPdfData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).Fallback(y => y.FontFamily(FallbackFontFamily)).FontSize(10).FontColor(Colors.Grey.Darken4));

                if (!string.IsNullOrWhiteSpace(data.WatermarkText))
                {
                    page.Foreground().AlignCenter().AlignMiddle().Rotate(-30)
                        .Text(data.WatermarkText).FontSize(40).FontColor(Colors.Red.Lighten3).SemiBold();
                }

                page.Header().Element(c => ComposeHeader(c, data));
                page.Content().PaddingVertical(10).Element(c => ComposeContent(c, data));
                page.Footer().Element(c => ComposeFooter(c, data));
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, ReportPdfData data)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("檢 驗 報 告").FontSize(20).SemiBold();
                    c.Item().Text($"{data.LabName}　|　{data.CategoryName}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(200).AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text($"報告編號：{data.ReportNo}").SemiBold();
                    c.Item().AlignRight().Text($"版本：第 {data.Version} 版").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(data.SupersedesReportNo))
                    {
                        c.Item().AlignRight().Text($"取代報告：{data.SupersedesReportNo}").FontSize(9).FontColor(Colors.Red.Darken1);
                    }
                });
            });
            column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void ComposeContent(IContainer container, ReportPdfData data)
    {
        container.Column(column =>
        {
            column.Spacing(8);

            column.Item().Text(data.Title).FontSize(14).SemiBold();

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(80);
                    c.RelativeColumn();
                });

                void Field(string label, string? value)
                {
                    table.Cell().Background(Colors.Grey.Lighten4).Padding(4).Text(label).SemiBold();
                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
                }

                Field("委託客戶", data.CustomerName);
                Field("樣品編號", data.SampleNo);
                Field("樣品描述", data.SampleDescription);
            });

            column.Item().PaddingTop(6).Text("檢驗結果").FontSize(12).SemiBold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(28);
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(1);
                    c.RelativeColumn(2);
                    c.RelativeColumn(3);
                });

                table.Header(header =>
                {
                    foreach (var title in new[] { "#", "檢驗項目", "結果", "單位", "限值 / 規格", "檢驗方法" })
                    {
                        header.Cell().Background(Colors.Grey.Darken3).Padding(4).Text(title).FontColor(Colors.White).SemiBold().FontSize(9);
                    }
                });

                int index = 1;
                foreach (var row in data.Results)
                {
                    var background = index % 2 == 0 ? Colors.Grey.Lighten5 : Colors.White;
                    table.Cell().Background(background).Padding(4).Text(index.ToString()).FontSize(9);
                    table.Cell().Background(background).Padding(4).Text(row.ItemName).FontSize(9);
                    table.Cell().Background(background).Padding(4).Text(row.Value ?? "—").FontSize(9).SemiBold();
                    table.Cell().Background(background).Padding(4).Text(row.Unit ?? string.Empty).FontSize(9);
                    table.Cell().Background(background).Padding(4).Text(row.LimitText ?? string.Empty).FontSize(9);
                    table.Cell().Background(background).Padding(4).Text(row.Method ?? string.Empty).FontSize(9);
                    index++;
                }

                if (data.Results.Count == 0)
                {
                    table.Cell().ColumnSpan(6).Padding(8).AlignCenter().Text("（無檢驗結果）").FontColor(Colors.Grey.Medium);
                }
            });

            column.Item().PaddingTop(16).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("報告簽署").FontSize(11).SemiBold();
                    if (data.IsPreview)
                    {
                        c.Item().Text("（預覽版：尚未簽署發行，不具效力）").FontColor(Colors.Red.Darken1);
                    }
                    else
                    {
                        c.Item().Text($"簽署人：{data.SignerName}");
                        c.Item().Text($"簽署人資格證號：{data.SignerCertificateNo}");
                        c.Item().Text($"簽署時間：{data.SignedAt?.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}");
                    }
                });

                if (!data.IsPreview && !string.IsNullOrWhiteSpace(data.VerificationToken))
                {
                    row.ConstantItem(230).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Column(c =>
                    {
                        c.Item().Text("報告真偽查驗").SemiBold().FontSize(9);
                        c.Item().Text($"查驗碼：{data.VerificationToken}").FontSize(9);
                        if (!string.IsNullOrWhiteSpace(data.VerificationUrl))
                        {
                            c.Item().Text(data.VerificationUrl).FontSize(7).FontColor(Colors.Blue.Darken2);
                        }
                        c.Item().Text("可於查驗頁上傳本檔比對 SHA-256 雜湊，確認未遭變造。").FontSize(7).FontColor(Colors.Grey.Darken1);
                    });
                }
            });
        });
    }

    private static void ComposeFooter(IContainer container, ReportPdfData data)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text("本報告僅對所測樣品負責；未經本實驗室書面同意，不得部分複製。").FontSize(7).FontColor(Colors.Grey.Darken1);
            row.ConstantItem(120).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(8));
                text.Span($"{data.ReportNo}　第 ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
                text.Span(" 頁");
            });
        });
    }
}
