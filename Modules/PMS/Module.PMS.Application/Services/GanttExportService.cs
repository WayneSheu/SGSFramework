using SGS.Modules.PMS.Application.Contracts;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>匯出請求：對照基準線（省略＝最新、Guid.Empty＝不對照）與前端已解析的負責人顯示名稱（僅用於顯示）。</summary>
public sealed record GanttExportRequest(Guid? BaselineId, Dictionary<Guid, string>? AssigneeNames);

/// <summary>
/// 甘特圖／WBS 匯出：沿用 <see cref="GanttService.GetAsync"/> 的授權（內部成員可讀，客戶與非成員 404）與資料，
/// 再交給 Excel／PDF 產生器。名稱字典僅作顯示，數量與長度皆受限，並在輸出端做公式注入防護。
/// </summary>
public sealed class GanttExportService(GanttService gantt, TimeProvider clock)
{
    private const int MaxNames = 200;
    private const int MaxNameLength = 60;

    public async Task<PmsResult<GanttExportFile>> ExportWbsAsync(Guid userId, string actor, Guid projectId, GanttExportRequest request, CancellationToken ct)
    {
        var data = await LoadAsync(userId, actor, projectId, request, ct).ConfigureAwait(false);
        if (data.Error is { } error) return error;

        var bytes = await GanttExcelExporter.BuildAsync(data.Value!, ct).ConfigureAwait(false);
        return PmsResult<GanttExportFile>.Success(new GanttExportFile(bytes, FileName(data.Value!, "WBS", "xlsx"),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }

    public async Task<PmsResult<GanttExportFile>> ExportChartPdfAsync(Guid userId, string actor, Guid projectId, GanttExportRequest request, CancellationToken ct)
    {
        var data = await LoadAsync(userId, actor, projectId, request, ct).ConfigureAwait(false);
        if (data.Error is { } error) return error;

        var bytes = await Task.Run(() => GanttChartPdf.Render(data.Value!), ct).ConfigureAwait(false);
        return PmsResult<GanttExportFile>.Success(new GanttExportFile(bytes, FileName(data.Value!, "甘特圖", "pdf"), "application/pdf"));
    }

    private async Task<PmsResult<GanttExportData>> LoadAsync(Guid userId, string actor, Guid projectId, GanttExportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await gantt.GetAsync(userId, projectId, request.BaselineId, ct).ConfigureAwait(false);
        if (result.Error is { } error) return error;
        var g = result.Value!;

        var names = new Dictionary<Guid, string>();
        foreach (var (id, name) in (request.AssigneeNames ?? []).Take(MaxNames))
        {
            var clean = new string((name ?? string.Empty).Where(c => !char.IsControl(c)).ToArray()).Trim();
            if (clean.Length == 0) continue;
            names[id] = clean.Length > MaxNameLength ? clean[..MaxNameLength] : clean;
        }

        var baseline = g.SelectedBaselineId is { } selected ? g.Baselines.FirstOrDefault(b => b.Id == selected) : null;
        var label = baseline is null ? null : $"{baseline.Number} {baseline.Name}";

        return PmsResult<GanttExportData>.Success(new GanttExportData(g, names, clock.GetUtcNow(), actor, label));
    }

    public static string FileName(GanttExportData data, string kind, string extension)
    {
        var date = data.ExportedAt.ToOffset(TimeSpan.FromHours(8)).ToString("yyyyMMdd");
        var invalid = Path.GetInvalidFileNameChars();
        var code = new string(data.Gantt.ProjectCode.Where(c => !invalid.Contains(c) && c != '_').ToArray());
        return $"{date}_{kind}_{(code.Length == 0 ? "project" : code)}.{extension}";
    }
}
