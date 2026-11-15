using System.Globalization;
using System.Net;
using System.Text;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 協力遲延與工期展延通知書的內容（規範第四章 PM 專屬範本）。<see cref="Subject"/> 與 <see cref="BodyText"/> 可由 PM 編輯；
/// 為空時使用系統預設文字。事項明細表（應完成日、遲延、順延、調整後交付日、請甲方提供期限）由系統依資料產生，不可編輯。
/// </summary>
public sealed record ExtensionNoticeData(
    string ContractNo, string ContractTitle, string CustomerName, string? ContactName, string CoopTitle, string? CoopDescription,
    DateOnly DueDate, DateOnly AsOf, int DelayWorkdays, int ExtensionWorkdays, DateOnly? EffectiveDelivery, DateOnly RequestedBy,
    string SenderName, string CompanyName, string? Message, bool Official, DateTimeOffset GeneratedAt,
    string? Subject = null, string? BodyText = null, string? Closing = null, string? Signature = null);

/// <summary>
/// 《甲方協力遲延與工期展延通知書》PDF（A4）：載明應完成日、累計遲延工作日、順延工作日與調整後預計交付日，並聲明遲延期間不計入乙方履約遲延。
/// 主旨與內文可由 PM 編輯（預設文字依範本產生）；非正式版本（寄出前預覽）頁首標示「預覽」；寄出版存入 FMS 作為免除逾期違約金的書面證據。
/// 字型用 Noto Sans TC（可子集化）。
/// </summary>
public static class ContractExtensionNoticePdf
{
    private const string FontFamily = "Noto Sans TC";
    private const string FallbackFontFamily = "Microsoft JhengHei";
    private static readonly string HeaderBlue = Colors.Blue.Darken3;

    static ContractExtensionNoticePdf()
    {
        Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    private static string Date(DateOnly v) => v.ToString("yyyy 年 M 月 d 日", CultureInfo.InvariantCulture);

    public static string DefaultSubject(string contractTitle) => $"【專案進度通知】關於「{contractTitle}」因待貴司提供協力事項之工期順延說明";

    /// <summary>預設內文（段落以空白行分隔）：說明約定的協力義務、遲延與順延、請甲方提供期限。</summary>
    public static string DefaultBody(ExtensionNoticeData d)
    {
        ArgumentNullException.ThrowIfNull(d);

        return
            $"感謝貴司團隊對本專案的支持與協助。依據雙方簽署之合約（{d.ContractNo}）及專案時程表之約定，為確保系統如期開發與測試，" +
            $"貴司原應於 {Date(d.DueDate)} 前完成或提供「{d.CoopTitle}」（詳如下方事項明細）。\n\n" +
            $"截至 {Date(d.AsOf)}，我方尚未收到貴司之回覆或資料，已累計延遲 {d.DelayWorkdays} 個工作日。由於後續之開發與測試須以此項目為前提方能推進，" +
            $"依合約約定，本專案之後續交付與驗收時程將按實際遲延日數自動順延（目前預計順延 {d.ExtensionWorkdays} 個工作日），且此段期間不計入我方之履約遲延。\n\n" +
            $"為避免影響貴司原定之系統上線計畫，敬請協助於 {Date(d.RequestedBy)} 前提供上述事項。若貴司在準備上有任何需要我方技術團隊協助之處，請隨時與我聯繫。";
    }

    public const string DefaultClosing = "順頌商祺";

    public static string DefaultSignature(ExtensionNoticeData d)
    {
        ArgumentNullException.ThrowIfNull(d);
        return $"{d.CompanyName}　專案經理　{d.SenderName}　敬上";
    }

    public static string EffectiveClosing(ExtensionNoticeData d) =>
        string.IsNullOrWhiteSpace(d.Closing) ? DefaultClosing : d.Closing.Trim();

    public static string EffectiveSignature(ExtensionNoticeData d) =>
        string.IsNullOrWhiteSpace(d.Signature) ? DefaultSignature(d) : d.Signature.Trim();

    public static string EffectiveSubject(ExtensionNoticeData d) =>
        string.IsNullOrWhiteSpace(d.Subject) ? DefaultSubject(d.ContractTitle) : d.Subject.Trim();

    public static string EffectiveBody(ExtensionNoticeData d) =>
        string.IsNullOrWhiteSpace(d.BodyText) ? DefaultBody(d) : d.BodyText.Trim();

    private static IEnumerable<string> Paragraphs(string text) =>
        text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static string Subject(string contractTitle) => DefaultSubject(contractTitle);

    public static string FileName(DateOnly date, string contractNo, int number) => $"{date:yyyyMMdd}_{contractNo}_協力遲延與工期展延通知書_{number}.pdf";

    public static byte[] Render(ExtensionNoticeData d)
    {
        ArgumentNullException.ThrowIfNull(d);

        var generated = d.GeneratedAt.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var subject = EffectiveSubject(d);
        var paragraphs = Paragraphs(EffectiveBody(d)).ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2.2f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).Fallback(y => y.FontFamily(FallbackFontFamily)).FontSize(11).FontColor(Colors.Grey.Darken4).LineHeight(1.6f));

                page.Header().Column(col =>
                {
                    col.Item().Text("協力遲延與工期展延通知書").FontSize(20).Bold().FontColor(HeaderBlue);
                    col.Item().PaddingTop(2).Text($"合約編號 {d.ContractNo}　{d.ContractTitle}").FontSize(11).SemiBold();
                    if (!d.Official)
                        col.Item().PaddingTop(2).Text("預覽版本 — 尚未寄出").FontSize(10).Bold().FontColor(Colors.Red.Darken2);
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(HeaderBlue);
                });

                page.Content().PaddingVertical(14).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text($"主旨：{subject}").Bold();
                    col.Item().Text($"{d.CustomerName}{(string.IsNullOrWhiteSpace(d.ContactName) ? string.Empty : $" {d.ContactName}")} 您好：");

                    foreach (var paragraph in paragraphs) col.Item().Text(paragraph);

                    col.Item().Text("事項明細").SemiBold().FontColor(HeaderBlue);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.ConstantColumn(150); c.RelativeColumn(); });
                        void Row(string label, string value)
                        {
                            table.Cell().Background(Colors.Grey.Lighten4).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(label).SemiBold();
                            table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(5).Text(value);
                        }

                        Row("待辦事項", string.IsNullOrWhiteSpace(d.CoopDescription) ? d.CoopTitle : $"{d.CoopTitle}（{d.CoopDescription.Trim()}）");
                        Row("甲方應完成日", Date(d.DueDate));
                        Row("累計遲延", $"{d.DelayWorkdays} 個工作日（截至 {Date(d.AsOf)}）");
                        Row("順延工作日", $"{d.ExtensionWorkdays} 個工作日");
                        Row("調整後預計交付日", d.EffectiveDelivery is { } e ? Date(e) : "依合約約定順延計算");
                        Row("請甲方提供期限", Date(d.RequestedBy));
                    });

                    if (!string.IsNullOrWhiteSpace(d.Message)) col.Item().Text(d.Message.Trim());

                    col.Item().PaddingTop(8).Text(EffectiveClosing(d));
                    col.Item().Text(EffectiveSignature(d));
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"{generated}（UTC+8）產生　{(d.Official ? "正式版本" : "預覽版本")}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.AutoItem().Text(t =>
                    {
                        t.CurrentPageNumber().FontSize(8);
                        t.Span(" / ").FontSize(8);
                        t.TotalPages().FontSize(8);
                    });
                });
            });
        }).GeneratePdf();
    }

    /// <summary>Email 內文（HTML；動態文字與編輯過的內文一律編碼）：與通知書內文一致，並註明詳見附件。</summary>
    public static string Body(ExtensionNoticeData d)
    {
        ArgumentNullException.ThrowIfNull(d);

        static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family: 'Microsoft JhengHei', Arial, sans-serif; font-size: 14px; line-height: 1.7; color: #212529;\">");
        sb.Append("<p>").Append(E(d.CustomerName)).Append(string.IsNullOrWhiteSpace(d.ContactName) ? null : " " + E(d.ContactName)).Append(" 您好：</p>");
        foreach (var paragraph in Paragraphs(EffectiveBody(d))) sb.Append("<p>").Append(E(paragraph).Replace("\n", "<br />")).Append("</p>");
        if (!string.IsNullOrWhiteSpace(d.Message)) sb.Append("<p>").Append(E(d.Message.Trim())).Append("</p>");
        sb.Append("<p>詳細事項請見附件《協力遲延與工期展延通知書》，如有任何疑問，請直接回覆此信與我們聯繫。</p>");
        sb.Append("<p>").Append(E(EffectiveClosing(d))).Append("<br />").Append(E(EffectiveSignature(d))).Append("</p>");
        sb.Append("</div>");
        return sb.ToString();
    }
}
