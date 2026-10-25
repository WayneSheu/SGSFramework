using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.ApiInfrastructure.DTOs.DatabaseSchemas;
using SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Controllers.Base;

namespace SGSFramework.ApiInfrastructure.Controllers.V1;

/// <summary>
/// 資料庫結構文件 (SSD Table Schema) 與 ERD 控制器
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/database-schemas")]
[ControllerTitle("資料庫結構文件", Icon = "fa-solid fa-database", Order = 23, Description = "提供資料表結構 (異動日期、欄位屬性、索引、外鍵)、ERD 與 Excel / PDF 匯出")]
[RequiresPermission("SYSTEM.DATABASESCHEMA.READ", "資料庫結構文件")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class DatabaseSchemaController : ApiControllerBase
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IDatabaseSchemaService _schemaService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DatabaseSchemaController> _logger;

    public DatabaseSchemaController(
        IDatabaseSchemaService schemaService,
        TimeProvider timeProvider,
        ILogger<DatabaseSchemaController> logger)
    {
        ArgumentNullException.ThrowIfNull(schemaService);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _schemaService = schemaService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// 取得資料庫結構 (資料表建立 / 異動日期、欄位屬性、索引、外鍵)
    /// </summary>
    [HttpGet]
    [Function("GetDatabaseSchema", "資料庫結構文件", Icon = "fa-solid fa-table-list", Order = 1, Description = "查詢資料表結構、欄位屬性與 ERD", IsMenu = true, Path = "/sgsframework.system/databaseschema")]
    [RequiresPermission("SYSTEM.DATABASESCHEMA.READ")]
    [EndpointSummary("資料庫結構文件")]
    [EndpointDescription("自 SQL Server 系統目錄讀取資料表、欄位、索引與外鍵定義。")]
    [ProducesResponseType(typeof(DatabaseSchemaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> GetSchema([FromQuery] SchemaFilterRequest filter, CancellationToken cancellationToken = default) =>
        ExecuteAsync("查詢資料庫結構", async () => Ok(await _schemaService.GetSchemaAsync(filter, cancellationToken)));

    /// <summary>
    /// 取得 ERD 圖面 (已完成版面配置)
    /// </summary>
    [HttpGet("erd")]
    [Function("GetErdDiagram", "ERD 實體關聯圖", Icon = "fa-solid fa-diagram-project", Order = 2, Description = "依篩選條件產生 ERD 圖面座標")]
    [RequiresPermission("SYSTEM.DATABASESCHEMA.READ")]
    [EndpointSummary("ERD 實體關聯圖")]
    [ProducesResponseType(typeof(ErdDiagramDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetErd([FromQuery] SchemaFilterRequest filter, CancellationToken cancellationToken = default) =>
        ExecuteAsync("產生 ERD", async () =>
        {
            var schema = await _schemaService.GetSchemaAsync(filter, cancellationToken);
            return Ok(ErdLayoutEngine.Build(schema.Tables, filter.KeysOnly));
        });

    /// <summary>
    /// 匯出 SSD Table Schema Excel
    /// </summary>
    [HttpGet("export/excel")]
    [Function("ExportSchemaExcel", "匯出結構 Excel", Icon = "fa-solid fa-file-excel", Order = 3, Description = "匯出資料表清單、欄位明細、索引與外鍵為 Excel")]
    [RequiresPermission("SYSTEM.DATABASESCHEMA.EXPORT", "匯出資料庫結構")]
    [EndpointSummary("匯出結構 Excel")]
    [Produces(ExcelContentType)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public Task<IActionResult> ExportExcel([FromQuery] SchemaFilterRequest filter, CancellationToken cancellationToken = default) =>
        ExecuteAsync("匯出結構 Excel", async () =>
        {
            var schema = await _schemaService.GetSchemaAsync(filter, cancellationToken);
            var bytes = await SchemaExcelExporter.ExportAsync(schema, cancellationToken);
            return FileResult(bytes, ExcelContentType, schema.DatabaseName, "xlsx");
        });

    /// <summary>
    /// 匯出 SSD Table Schema PDF (含 ERD)
    /// </summary>
    [HttpGet("export/pdf")]
    [Function("ExportSchemaPdf", "匯出結構 PDF", Icon = "fa-solid fa-file-pdf", Order = 4, Description = "匯出資料表結構明細與 ERD 為 PDF")]
    [RequiresPermission("SYSTEM.DATABASESCHEMA.EXPORT", "匯出資料庫結構")]
    [EndpointSummary("匯出結構 PDF")]
    [Produces(MediaTypeNames.Application.Pdf)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public Task<IActionResult> ExportPdf([FromQuery] SchemaFilterRequest filter, CancellationToken cancellationToken = default) =>
        ExecuteAsync("匯出結構 PDF", async () =>
        {
            var schema = await _schemaService.GetSchemaAsync(filter, cancellationToken);
            var bytes = await Task.Run(() => SchemaPdfExporter.Export(schema, filter.IncludeErd, filter.KeysOnly, filter.Orientation, filter.IncludeHistory), cancellationToken);
            return FileResult(bytes, MediaTypeNames.Application.Pdf, schema.DatabaseName, "pdf");
        });

    private FileContentResult FileResult(byte[] bytes, string contentType, string databaseName, string extension)
    {
        var fileName = $"SSD_TableSchema_{databaseName}_{_timeProvider.GetLocalNow():yyyyMMdd_HHmmss}.{extension}";
        Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
        _logger.LogInformation("匯出資料庫結構文件 {FileName}，大小 {Size} bytes", fileName, bytes.Length);
        return File(bytes, contentType, fileName);
    }

    private async Task<IActionResult> ExecuteAsync(string operation, Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("{Operation} 之請求已被使用者取消。", operation);
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Operation} 時發生非預期異常。", operation);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = $"{operation}時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }
}
