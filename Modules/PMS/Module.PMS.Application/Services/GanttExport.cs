using System.Globalization;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using MiniExcelLibs.OpenXml;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SkiaSharp;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>匯出檔案內容。</summary>
public sealed record GanttExportFile(byte[] Content, string FileName, string ContentType);

/// <summary>匯出所需的資料（甘特圖本體、人員顯示名稱、匯出者與時間、對照基準線說明）。</summary>
public sealed record GanttExportData(
    GanttDto Gantt, IReadOnlyDictionary<Guid, string> Names, DateTimeOffset ExportedAt, string ExportedBy, string? BaselineLabel);

/// <summary>
/// WBS Excel 匯出（MiniExcel）：專案資訊、WBS（編號、階層縮排、起迄、天數、負責人、進度、前置、基準線偏差、狀態）、里程碑三個工作表，
/// 皆附篩選與凍結標題列。儲存格內容若以 = + - @ 開頭一律加前導單引號，避免被 Excel 當成公式執行（CSV／Excel 注入）。
/// </summary>
public static class GanttExcelExporter
{
    internal static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);

    public static async Task<byte[]> BuildAsync(GanttExportData data, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(data);

        var sheets = new Dictionary<string, object>
        {
            ["專案資訊"] = BuildInfo(data),
            ["WBS"] = BuildWbs(data),
            ["里程碑"] = BuildMilestones(data)
        };

        var configuration = new OpenXmlConfiguration
        {
            AutoFilter = true,
            FreezeRowCount = 1,
            FastMode = true,
            EnableAutoWidth = false,
            TableStyles = TableStyles.Default,
            DynamicColumns = Columns()
        };

        using var stream = new MemoryStream();
        await stream.SaveAsAsync(sheets, configuration: configuration, cancellationToken: ct).ConfigureAwait(false);
        return stream.ToArray();
    }

    private static DynamicExcelColumn[] Columns()
    {
        (string Name, double Width)[] widths =
        [
            ("項目", 22), ("內容", 70),
            ("WBS", 8), ("任務名稱", 38), ("類型", 8), ("開始日", 13), ("結束日", 13), ("天數", 7), ("負責人", 14), ("完成%", 8),
            ("狀態", 16), ("前置任務", 18), ("基準開始", 13), ("基準結束", 13), ("開始偏差", 12), ("結束偏差", 12), ("對應階段", 14), ("說明", 40),
            ("日期", 13), ("類型", 14), ("說明", 50)
        ];
        return widths.GroupBy(w => w.Name).Select(g => new DynamicExcelColumn(g.Key) { Width = g.Max(x => x.Width) }).ToArray();
    }

    internal static string PhaseName(ProjectPhase? phase) => phase switch
    {
        ProjectPhase.Initiation => "啟動與規劃",
        ProjectPhase.Requirement => "需求分析",
        ProjectPhase.Design => "系統設計",
        ProjectPhase.Development => "開發與實作",
        ProjectPhase.Testing => "測試與驗證",
        ProjectPhase.DeployClose => "部署與結案",
        _ => string.Empty
    };

    /// <summary>儲存格安全文字：以 = + - @ 或控制字元開頭者加前導單引號。</summary>
    public static string Safe(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n' ? "'" + value : value;
    }

    public static string Status(GanttTaskDto t) => t switch
    {
        { ConflictDays: > 0 } => $"相依衝突 {t.ConflictDays} 天",
        { Percent: 100 } => "已完成",
        { IsOverdue: true } => "逾期",
        { Percent: > 0 } => "進行中",
        _ => "未開始"
    };

    private static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Variance(int? days) => days is { } d ? (d > 0 ? $"延後 {d} 天" : d < 0 ? $"提前 {-d} 天" : "準時") : string.Empty;

    private static List<Dictionary<string, object?>> BuildInfo(GanttExportData d) =>
    [
        Row(("項目", "專案"), ("內容", Safe($"{d.Gantt.ProjectCode} {d.Gantt.ProjectName}"))),
        Row(("項目", "匯出時間（UTC+8）"), ("內容", d.ExportedAt.ToOffset(TaipeiOffset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))),
        Row(("項目", "匯出者"), ("內容", Safe(d.ExportedBy))),
        Row(("項目", "任務數"), ("內容", d.Gantt.Tasks.Count)),
        Row(("項目", "對照基準線"), ("內容", d.BaselineLabel ?? "無")),
        Row(("項目", "整體結束日偏差"), ("內容", d.Gantt.ProjectEndVarianceDays is null ? "—" : Variance(d.Gantt.ProjectEndVarianceDays))),
        Row(("項目", "日期說明"), ("內容", "日曆日（含起訖日），未扣除週末與國定假日；摘要任務的起迄與進度由子任務彙總。")),
        Row(("項目", "文件性質"), ("內容", "內部文件，請勿對外提供。"))
    ];

    private static List<Dictionary<string, object?>> BuildWbs(GanttExportData d) =>
        d.Gantt.Tasks.Select(t => Row(
            ("WBS", t.Wbs),
            ("任務名稱", new string('　', t.Level) + Safe(t.Name)),
            ("類型", t.IsMilestone ? "里程碑" : t.HasChildren ? "摘要" : "任務"),
            ("開始日", Date(t.Start)),
            ("結束日", Date(t.End)),
            ("天數", t.DurationDays),
            ("負責人", t.AssigneeUserId is { } id && d.Names.TryGetValue(id, out var name) ? Safe(name) : string.Empty),
            ("完成%", t.Percent),
            ("狀態", Status(t)),
            ("前置任務", Safe(string.Join("；", t.Predecessors.Select(p => $"{p.PredecessorWbs}{(p.LagDays != 0 ? $"（落後 {p.LagDays} 天）" : null)}")))),
            ("基準開始", t.BaselineStart is { } bs ? Date(bs) : string.Empty),
            ("基準結束", t.BaselineEnd is { } be ? Date(be) : string.Empty),
            ("開始偏差", Variance(t.StartVarianceDays)),
            ("結束偏差", Variance(t.EndVarianceDays)),
            ("對應階段", PhaseName(t.Phase)),
            ("說明", Safe(t.Description)))).ToList();

    private static List<Dictionary<string, object?>> BuildMilestones(GanttExportData d) =>
        d.Gantt.Milestones.Select(m => Row(
            ("日期", Date(m.Date)),
            ("類型", MilestoneName(m.Kind)),
            ("說明", Safe(m.Label)),
            ("狀態", m.Status ?? string.Empty))).ToList();

    public static string MilestoneName(GanttMilestoneKind kind) => kind switch
    {
        GanttMilestoneKind.FreezeLine => "需求凍結線",
        GanttMilestoneKind.Gate => "Gate 放行",
        GanttMilestoneKind.Release => "上線",
        GanttMilestoneKind.CrDelay => "CR 順延上線",
        _ => "UAT 驗收"
    };

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] cells)
    {
        var row = new Dictionary<string, object?>(cells.Length);
        foreach (var (key, value) in cells) row[key] = value;
        return row;
    }
}

/// <summary>
/// 專案甘特圖 PDF（QuestPDF，A4 橫式）：左側任務表格＋右側時間軸（任務條與進度、摘要、里程碑、基準線、相依線、今天線、週末底色、
/// 凍結線／上線／CR 順延虛線）；任務多時分頁，時間軸以同一比例縮放，整張圖寬度剛好容納；最後一頁為里程碑清單與圖例。
/// 字型用 Noto Sans TC（可子集化），圖面本身只畫圖形與 ASCII 文字，中文由表格文字負責，避免 Skia 字型缺字。
/// </summary>
public static class GanttChartPdf
{
    private static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);

    private const string FontFamily = "Noto Sans TC";
    private const string FallbackFontFamily = "Microsoft JhengHei";

    private const float PageMargin = 28f;
    private const float LeftWidth = 262f;
    private const float RowHeight = 18f;
    private const float HeadHeight = 34f;
    private const float PageWidth = 841.89f;
    private const int RowsPerPage = 22;
    private const float MaxDayWidth = 16f;

    private static readonly string HeaderBlue = Colors.Blue.Darken3;

    static GanttChartPdf()
    {
        Settings.CheckIfAllTextGlyphsAreAvailable = false;
    }

    public static byte[] Render(GanttExportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var g = data.Gantt;
        var tasks = g.Tasks;
        var layout = ChartLayout.Create(g);
        var pages = tasks.Chunk(RowsPerPage).Select(chunk => chunk.ToList()).ToList();
        if (pages.Count == 0) pages.Add([]);

        var exported = data.ExportedAt.ToOffset(TaipeiOffset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        return Document.Create(container =>
        {
            for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                var rows = pages[pageIndex];
                var offset = pageIndex * RowsPerPage;

                container.Page(page =>
                {
                    ConfigurePage(page, data, exported);
                    page.Content().PaddingTop(8).Row(row =>
                    {
                        row.ConstantItem(LeftWidth).Element(c => ComposeTable(c, rows, data));
                        row.RelativeItem().Element(c => c.Height(HeadHeight + rows.Count * RowHeight)
                            .Canvas((canvas, size) => DrawChart(canvas, size, rows, offset, g, layout)));
                    });
                });
            }

            container.Page(page =>
            {
                ConfigurePage(page, data, exported);
                page.Content().PaddingTop(8).Element(c => ComposeLegend(c, data));
            });
        }).GeneratePdf();
    }

    private static void ConfigurePage(PageDescriptor page, GanttExportData data, string exported)
    {
        page.Size(PageSizes.A4.Landscape());
        page.Margin(PageMargin);
        page.DefaultTextStyle(x => x.FontFamily(FontFamily).Fallback(y => y.FontFamily(FallbackFontFamily)).FontSize(8).FontColor(Colors.Grey.Darken4));

        page.Header().Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text($"專案甘特圖　{data.Gantt.ProjectCode}　{data.Gantt.ProjectName}").FontSize(14).Bold().FontColor(HeaderBlue);
                row.AutoItem().Text($"對照基準線：{data.BaselineLabel ?? "無"}").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
            col.Item().PaddingTop(2).LineHorizontal(0.8f).LineColor(HeaderBlue);
        });

        page.Footer().Row(row =>
        {
            row.RelativeItem().Text($"匯出者 {data.ExportedBy}　{exported}（UTC+8）　內部文件，請勿對外提供").FontSize(7).FontColor(Colors.Grey.Darken1);
            row.AutoItem().Text(t =>
            {
                t.CurrentPageNumber().FontSize(7);
                t.Span(" / ").FontSize(7);
                t.TotalPages().FontSize(7);
            });
        });
    }

    // ---------- 左側任務表格（每列固定高度，與右側圖面逐列對齊） ----------

    private static void ComposeTable(IContainer container, List<GanttTaskDto> rows, GanttExportData data)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(30);   // WBS
                c.RelativeColumn();     // 任務
                c.ConstantColumn(46);   // 負責人
                c.ConstantColumn(32);   // 開始
                c.ConstantColumn(32);   // 結束
                c.ConstantColumn(22);   // %
            });

            foreach (var title in new[] { "WBS", "任務", "負責人", "開始", "結束", "%" })
                table.Cell().Height(HeadHeight).Background(Colors.Grey.Lighten3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1)
                    .AlignBottom().PaddingBottom(3).PaddingLeft(2).Text(title).FontSize(7).SemiBold();

            foreach (var t in rows)
            {
                var bold = t.HasChildren;
                var assignee = t.AssigneeUserId is { } id && data.Names.TryGetValue(id, out var name) ? name : "—";

                Cell(table, t.Wbs, 0, bold, 4.5f, muted: true);
                Cell(table, (t.IsMilestone ? "◆ " : string.Empty) + t.Name, t.Level * 6, bold, Math.Max(4f, (100f - t.Level * 6) / 7.2f), flag: t.IsOverdue);
                Cell(table, assignee, 0, false, 6f);
                Cell(table, t.Start.ToString("MM/dd", CultureInfo.InvariantCulture), 0, false, 6f);
                Cell(table, t.End.ToString("MM/dd", CultureInfo.InvariantCulture), 0, false, 6f);
                Cell(table, $"{t.Percent}", 0, false, 4f);
            }
        });
    }

    private static string Fit(string text, float maxUnits)
    {
        var used = 0f;
        for (var i = 0; i < text.Length; i++)
        {
            used += text[i] > 0x2E80 ? 1f : 0.55f;
            if (used > maxUnits) return text[..Math.Max(0, i - 1)] + "…";
        }

        return text;
    }

    private static void Cell(TableDescriptor table, string text, float indent, bool bold, float maxUnits, bool muted = false, bool flag = false)
    {
        var cell = table.Cell().Height(RowHeight).BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2)
            .PaddingLeft(2 + indent).AlignMiddle();
        if (bold) cell = cell.Background(Colors.Grey.Lighten4);

        var span = cell.Text(Fit(text, maxUnits)).FontSize(7);
        if (bold) span.SemiBold();
        if (muted) span.FontColor(Colors.Grey.Darken1);
        if (flag) span.FontColor(Colors.Red.Darken2);
    }

    // ---------- 右側時間軸（Skia 繪圖） ----------

    private sealed record ChartLayout(DateOnly Origin, int TotalDays)
    {
        public static ChartLayout Create(GanttDto g)
        {
            var dates = g.Tasks.SelectMany(t => new[] { t.Start, t.End })
                .Concat(g.Tasks.Where(t => t.BaselineStart is not null).SelectMany(t => new[] { t.BaselineStart!.Value, t.BaselineEnd!.Value }))
                .ToList();
            if (dates.Count == 0) dates.Add(g.Today);

            var min = dates.Min();
            var max = dates.Max();

            // 今天與里程碑落在範圍附近才納入，避免一個遠端的日期把整張圖壓得太窄
            foreach (var extra in g.Milestones.Select(m => m.Date).Append(g.Today))
                if (extra >= min.AddDays(-30) && extra <= max.AddDays(30)) { if (extra < min) min = extra; if (extra > max) max = extra; }

            var origin = min.AddDays(-2);
            var total = Math.Max(14, max.AddDays(9).DayNumber - origin.DayNumber + 1);
            return new ChartLayout(origin, total);
        }
    }

    private static void DrawChart(SKCanvas canvas, Size size, List<GanttTaskDto> rows, int rowOffset, GanttDto g, ChartLayout layout)
    {
        var width = size.Width;
        var height = HeadHeight + rows.Count * RowHeight;
        var dayWidth = Math.Min(MaxDayWidth, width / layout.TotalDays);

        float X(DateOnly d) => (d.DayNumber - layout.Origin.DayNumber) * dayWidth;

        using var typeface = SKTypeface.FromFamilyName(FontFamily) ?? SKTypeface.Default;
        using var text = new SKPaint { Typeface = typeface, TextSize = 6.5f, IsAntialias = true, Color = new SKColor(70, 70, 70) };
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.6f };

        // 表頭底色
        fill.Color = new SKColor(243, 243, 243);
        canvas.DrawRect(0, 0, width, HeadHeight, fill);

        // 週末底色（比例夠大時）
        if (dayWidth >= 3f)
        {
            fill.Color = new SKColor(0, 0, 0, 12);
            for (var i = 0; i < layout.TotalDays; i++)
            {
                var date = layout.Origin.AddDays(i);
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) canvas.DrawRect(i * dayWidth, HeadHeight, dayWidth, height - HeadHeight, fill);
            }
        }

        // 月份格線與標籤；週格線與日期（比例夠大時）
        stroke.Color = new SKColor(200, 200, 200);
        for (var i = 0; i < layout.TotalDays; i++)
        {
            var date = layout.Origin.AddDays(i);
            var x = i * dayWidth;

            if (date.Day == 1 || i == 0)
            {
                stroke.Color = new SKColor(170, 170, 170);
                canvas.DrawLine(x, 0, x, height, stroke);
                canvas.DrawText($"{date.Year}-{date.Month:00}", x + 2, 10, text);
            }
            else if (date.DayOfWeek == DayOfWeek.Monday && dayWidth * 7 >= 22)
            {
                stroke.Color = new SKColor(225, 225, 225);
                canvas.DrawLine(x, HeadHeight / 2, x, height, stroke);
                canvas.DrawText($"{date.Month}/{date.Day}", x + 1.5f, HeadHeight - 5, text);
            }
        }

        stroke.Color = new SKColor(200, 200, 200);
        canvas.DrawLine(0, HeadHeight, width, HeadHeight, stroke);

        // 列分隔線
        stroke.Color = new SKColor(232, 232, 232);
        for (var i = 1; i <= rows.Count; i++) canvas.DrawLine(0, HeadHeight + i * RowHeight, width, HeadHeight + i * RowHeight, stroke);

        // 凍結線／上線／CR 順延：整頁虛線；Gate／UAT：表頭小方塊
        foreach (var m in g.Milestones)
        {
            var x = X(m.Date) + dayWidth / 2;
            if (x < 0 || x > width) continue;

            var color = m.Kind switch
            {
                GanttMilestoneKind.FreezeLine => new SKColor(11, 139, 214),
                GanttMilestoneKind.Release => new SKColor(26, 158, 85),
                GanttMilestoneKind.CrDelay => new SKColor(217, 131, 0),
                GanttMilestoneKind.Gate => new SKColor(90, 90, 90),
                _ => new SKColor(46, 160, 67)
            };

            if (m.Kind is GanttMilestoneKind.FreezeLine or GanttMilestoneKind.Release or GanttMilestoneKind.CrDelay)
            {
                stroke.Color = color;
                stroke.PathEffect = SKPathEffect.CreateDash([3f, 2f], 0);
                canvas.DrawLine(x, HeadHeight, x, height, stroke);
                stroke.PathEffect = null;
            }

            fill.Color = color;
            canvas.DrawRect(x - 2.5f, HeadHeight / 2 - 2.5f, 5, 5, fill);
        }

        // 今天線
        var todayX = X(g.Today) + dayWidth / 2;
        if (todayX >= 0 && todayX <= width)
        {
            stroke.Color = new SKColor(229, 72, 77);
            stroke.StrokeWidth = 1.1f;
            canvas.DrawLine(todayX, HeadHeight, todayX, height, stroke);
            stroke.StrokeWidth = 0.6f;
        }

        // 任務條
        var indexById = rows.Select((t, i) => (t.Id, i)).ToDictionary(x => x.Id, x => x.i);
        for (var i = 0; i < rows.Count; i++)
        {
            var t = rows[i];
            var y = HeadHeight + i * RowHeight;
            var x1 = X(t.Start);
            var w = Math.Max(3f, (t.End.DayNumber - t.Start.DayNumber + 1) * dayWidth);

            // 基準線（細條在任務條下方）
            if (t.BaselineStart is { } bs && t.BaselineEnd is { } be)
            {
                fill.Color = new SKColor(154, 164, 178);
                canvas.DrawRect(X(bs), y + 13.5f, Math.Max(2f, (be.DayNumber - bs.DayNumber + 1) * dayWidth), 2.2f, fill);
            }

            if (t.IsMilestone)
            {
                var cx = x1 + dayWidth / 2;
                using var diamond = new SKPath();
                diamond.MoveTo(cx, y + 3.5f);
                diamond.LineTo(cx + 5, y + 8.5f);
                diamond.LineTo(cx, y + 13.5f);
                diamond.LineTo(cx - 5, y + 8.5f);
                diamond.Close();
                fill.Color = t.IsOverdue ? new SKColor(209, 52, 56) : new SKColor(123, 63, 228);
                canvas.DrawPath(diamond, fill);
            }
            else if (t.HasChildren)
            {
                fill.Color = new SKColor(120, 120, 120);
                canvas.DrawRect(x1, y + 5.5f, w, 4.5f, fill);
                fill.Color = new SKColor(40, 40, 40);
                canvas.DrawRect(x1, y + 5.5f, w * t.Percent / 100f, 4.5f, fill);
            }
            else
            {
                var late = t.IsOverdue;
                fill.Color = late ? new SKColor(246, 201, 203) : new SKColor(188, 215, 240);
                canvas.DrawRect(x1, y + 3.5f, w, 9, fill);
                fill.Color = late ? new SKColor(209, 52, 56) : new SKColor(59, 130, 196);
                canvas.DrawRect(x1, y + 3.5f, w * t.Percent / 100f, 9, fill);
                stroke.Color = t.ConflictDays > 0 ? new SKColor(247, 166, 0) : late ? new SKColor(209, 52, 56) : new SKColor(59, 130, 196);
                stroke.StrokeWidth = t.ConflictDays > 0 ? 1.2f : 0.6f;
                canvas.DrawRect(x1, y + 3.5f, w, 9, stroke);
                stroke.StrokeWidth = 0.6f;
            }

            // 進度與偏差（ASCII）
            var label = t.IsMilestone ? string.Empty : $"{t.Percent}%";
            if (t.EndVarianceDays is { } v and not 0) label += $" {(v > 0 ? "+" : string.Empty)}{v}d";
            if (label.Length > 0)
            {
                text.Color = t.EndVarianceDays is > 0 ? new SKColor(209, 52, 56) : new SKColor(90, 90, 90);
                canvas.DrawText(label, x1 + w + 3, y + 11, text);
                text.Color = new SKColor(70, 70, 70);
            }
        }

        // 相依線（前後任務都在本頁才畫）
        stroke.StrokeWidth = 0.7f;
        foreach (var succ in rows)
        {
            foreach (var dep in succ.Predecessors)
            {
                if (!indexById.TryGetValue(dep.PredecessorId, out var pi) || !indexById.TryGetValue(succ.Id, out var si)) continue;

                var pred = rows[pi];
                var x1 = pred.IsMilestone ? X(pred.Start) + dayWidth / 2 + 5 : X(pred.End) + dayWidth;
                var x2 = succ.IsMilestone ? X(succ.Start) + dayWidth / 2 - 5 : X(succ.Start);
                var y1 = HeadHeight + pi * RowHeight + RowHeight / 2;
                var y2 = HeadHeight + si * RowHeight + RowHeight / 2;

                stroke.Color = succ.ConflictDays > 0 ? new SKColor(209, 52, 56) : new SKColor(122, 134, 154);
                using var path = new SKPath();
                path.MoveTo(x1, y1);
                if (x2 >= x1 + 4)
                {
                    path.LineTo(x1 + 2, y1);
                    path.LineTo(x1 + 2, y2);
                    path.LineTo(x2, y2);
                }
                else
                {
                    var ym = si > pi ? HeadHeight + pi * RowHeight + RowHeight - 1.5f : HeadHeight + pi * RowHeight + 1.5f;
                    path.LineTo(x1 + 2, y1);
                    path.LineTo(x1 + 2, ym);
                    path.LineTo(x2 - 3, ym);
                    path.LineTo(x2 - 3, y2);
                    path.LineTo(x2, y2);
                }

                canvas.DrawPath(path, stroke);

                using var arrow = new SKPath();
                arrow.MoveTo(x2, y2);
                arrow.LineTo(x2 - 3.5f, y2 - 2.2f);
                arrow.LineTo(x2 - 3.5f, y2 + 2.2f);
                arrow.Close();
                fill.Color = stroke.Color;
                canvas.DrawPath(arrow, fill);
            }
        }

        _ = rowOffset;
    }

    // ---------- 末頁：里程碑與圖例 ----------

    private static void ComposeLegend(IContainer container, GanttExportData data)
    {
        var g = data.Gantt;

        container.Column(col =>
        {
            col.Spacing(10);

            col.Item().Text("里程碑").FontSize(12).Bold().FontColor(HeaderBlue);
            if (g.Milestones.Count == 0)
            {
                col.Item().Text("尚無里程碑（需求凍結線、Gate 放行、上線、CR 順延與 UAT 驗收會自動出現在這裡）。").FontColor(Colors.Grey.Darken1);
            }
            else
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.ConstantColumn(80); c.ConstantColumn(90); c.RelativeColumn(); });
                    foreach (var title in new[] { "日期", "類型", "說明" })
                        table.Cell().Background(HeaderBlue).Padding(3).Text(title).FontColor(Colors.White).SemiBold();

                    foreach (var m in g.Milestones)
                    {
                        table.Cell().BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(m.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                        table.Cell().BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(GanttExcelExporter.MilestoneName(m.Kind));
                        table.Cell().BorderBottom(0.4f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(m.Label);
                    }
                });
            }

            col.Item().Text("圖例與說明").FontSize(12).Bold().FontColor(HeaderBlue);
            col.Item().Column(legend =>
            {
                legend.Spacing(3);
                legend.Item().Text("藍色條＝任務（深色為已完成進度）；深灰細條＝摘要任務（由子任務彙總）；紫色菱形＝里程碑；紅色＝逾期未完成；橘框＝違反前置相依。");
                legend.Item().Text("灰色細條（任務條下方）＝基準線；任務旁標示完成百分比與相對基準線的結束日偏差（+Nd 延後、-Nd 提前）。");
                legend.Item().Text("紅色直線＝今天；藍／綠／橘色虛線＝需求凍結線／上線／CR 順延上線日；表頭小方塊＝里程碑日期（含 Gate 放行、UAT 驗收）。");
                legend.Item().Text("日期為日曆日（含起訖日），未扣除週末與國定假日；灰色底色為週末。").FontColor(Colors.Grey.Darken1);
                if (g.ProjectEndVarianceDays is { } v)
                    legend.Item().Text($"整體結束日相對基準線：{(v == 0 ? "準時" : v > 0 ? $"延後 {v} 天" : $"提前 {-v} 天")}。").SemiBold();
            });
        });
    }
}
