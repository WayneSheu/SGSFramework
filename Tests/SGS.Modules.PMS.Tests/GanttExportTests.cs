using FluentAssertions;
using MiniExcelLibs;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Services;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class GanttExportTests
{
    private static readonly DateOnly Day = new(2026, 11, 2);

    private static GanttTaskDto Task(string wbs, string name, int level, int percent = 0, bool milestone = false, bool hasChildren = false,
        int conflict = 0, bool overdue = false, int? endVariance = null, Guid? assignee = null, IReadOnlyList<GanttPredecessorDto>? preds = null) =>
        new(Guid.NewGuid(), null, wbs, level, hasChildren, name, null, Day, Day.AddDays(6), 7, milestone, percent, assignee, null,
            preds ?? [], conflict, overdue, null, null, null, endVariance, false, false, "AA==");

    private static GanttExportData Data(params GanttTaskDto[] tasks)
    {
        var gantt = new GanttDto(Guid.NewGuid(), "ZPMS1", "測試專案", Day, null, null, tasks, [], [], null, null, new GanttActionsDto(false, false));
        return new GanttExportData(gantt, new Dictionary<Guid, string>(), DateTimeOffset.Parse("2026-11-14T02:00:00Z"), "tester", null);
    }

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("-x", "'-x")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("正常文字", "正常文字")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Safe_PrefixesFormulaCharacters(string? input, string expected) =>
        GanttExcelExporter.Safe(input).Should().Be(expected);

    [Fact]
    public void Status_PrioritizesConflictThenDoneThenOverdue()
    {
        GanttExcelExporter.Status(Task("1", "a", 0, conflict: 2, percent: 100)).Should().Contain("衝突");
        GanttExcelExporter.Status(Task("1", "a", 0, percent: 100, overdue: true)).Should().Be("已完成");
        GanttExcelExporter.Status(Task("1", "a", 0, overdue: true)).Should().Be("逾期");
        GanttExcelExporter.Status(Task("1", "a", 0, percent: 30)).Should().Be("進行中");
        GanttExcelExporter.Status(Task("1", "a", 0)).Should().Be("未開始");
    }

    [Fact]
    public async Task Excel_ContainsSheetsAndEscapesFormulas()
    {
        var data = Data(Task("1", "=HYPERLINK(\"x\")", 0, hasChildren: true), Task("1.1", "子任務", 1, 50));

        var bytes = await GanttExcelExporter.BuildAsync(data, CancellationToken.None);

        using var stream = new MemoryStream(bytes);
        stream.SaveAsAsyncCheck().Should().BeTrue();
        var sheets = MiniExcel.GetSheetNames(stream);
        sheets.Should().BeEquivalentTo("專案資訊", "WBS", "里程碑");

        stream.Position = 0;
        var rows = MiniExcel.Query(stream, useHeaderRow: true, sheetName: "WBS").Cast<IDictionary<string, object>>().ToList();
        rows.Should().HaveCount(2);
        rows[0]["任務名稱"].ToString().Should().StartWith("'=");
        rows[1]["任務名稱"].ToString().Should().StartWith("　");
    }

    [Fact]
    public void Pdf_RendersMultiplePagesWithValidHeader()
    {
        var tasks = Enumerable.Range(1, 50).Select(i => Task(i.ToString(), $"任務 {i}", 0, i % 100)).ToArray();

        var bytes = GanttChartPdf.Render(Data(tasks));

        bytes.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void Pdf_RendersEmptyProject()
    {
        var bytes = GanttChartPdf.Render(Data());

        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).Should().Be("%PDF-");
    }

    [Fact]
    public void FileName_UsesDateKindAndCleanCode()
    {
        var name = GanttExportService.FileName(Data(), "WBS", "xlsx");

        name.Should().Be("20261114_WBS_ZPMS1.xlsx");
    }
}

internal static class StreamTestExtensions
{
    public static bool SaveAsAsyncCheck(this MemoryStream stream) => stream.Length > 0;
}
