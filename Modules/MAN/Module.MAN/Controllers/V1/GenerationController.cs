using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.MAN.Application.Contracts;
using SGS.Modules.MAN.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;

namespace SGS.Modules.MAN.Controllers.V1;

/// <summary>multipart 表單：截圖與生成參數。</summary>
public sealed class GenerateTopicForm
{
    /// <summary>軟體畫面截圖（PNG／JPEG／WebP）。</summary>
    public IFormFile? Screenshot { get; set; }

    /// <summary>功能代碼（小寫英數、底線、連字號、句點）。</summary>
    public string FeatureKey { get; set; } = string.Empty;

    /// <summary>功能標題。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>所屬模組／章節（選填）。</summary>
    public string? ModuleName { get; set; }

    /// <summary>用來挑選後端規格的 API 路徑或資料模型關鍵字（選填，例如 Order）。</summary>
    public string? ApiKeyword { get; set; }

    /// <summary>主題已存在時是否以 AI 草稿覆寫。</summary>
    public bool Overwrite { get; set; }
}

/// <summary>
/// AI 生成：以本機 Ollama 依截圖（與後端規格）產生手冊草稿。截圖只在記憶體處理、不落地；
/// 生成結果一律是草稿，須人工審核並發布後才會對全站生效。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/man/generation")]
[ControllerTitle("AI 生成", Icon = "fa-solid fa-wand-magic-sparkles", Order = 2, Description = "以本機 Ollama 依截圖生成操作教學草稿")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class GenerationController(ManualGenerationService generation) : ManControllerBase
{
    /// <summary>AI 生成服務狀態（是否啟用、Ollama 與模型是否可用）</summary>
    [HttpGet("status")]
    [Function("GetGenerationStatus", "AI 生成", Icon = "fa-solid fa-wand-magic-sparkles", Order = 1, Description = "以截圖生成操作教學草稿", IsMenu = true, Path = "/sgs.modules.man/generate")]
    [RequiresPermission("MAN.GENERATION.READ", "檢視 AI 生成")]
    [ProducesResponseType(typeof(GenerationStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GenerationStatusDto>> Status(CancellationToken ct = default) =>
        Ok(await generation.GetStatusAsync(ct).ConfigureAwait(false));

    /// <summary>送出生成作業（立即回傳作業，生成於背景進行；以作業查詢取得進度）</summary>
    [HttpPost("jobs")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [Function("CreateGenerationJob", "送出 AI 生成", Order = 2, Description = "上傳截圖並送出生成作業")]
    [RequiresPermission("MAN.TOPIC.GENERATE", "執行 AI 生成", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(GenerationJobDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<GenerationJobDto>> Create([FromForm] GenerateTopicForm form, CancellationToken ct = default)
    {
        if (form.Screenshot is null || form.Screenshot.Length == 0)
            return ProblemFrom(ManError.Validation("請上傳有效的截圖檔案。"));

        // 先以宣告長度擋掉過大檔案，避免讀入記憶體後才拒絕
        if (form.Screenshot.Length > 12 * 1024 * 1024)
            return ProblemFrom(ManError.Validation("截圖檔案過大。"));

        await using var stream = form.Screenshot.OpenReadStream();
        using var ms = new MemoryStream((int)form.Screenshot.Length);
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);

        var result = await generation.EnqueueAsync(
            Actor, new GenerateTopicRequest(form.FeatureKey, form.Title, form.ModuleName, form.ApiKeyword, form.Overwrite), ms.ToArray(), ct)
            .ConfigureAwait(false);

        return result.IsSuccess ? Accepted(result.Value) : ProblemFrom(result.Error!);
    }

    /// <summary>分頁查詢生成作業</summary>
    [HttpPost("jobs/search")]
    [Function("SearchGenerationJobs", "查詢 AI 生成作業", Order = 3, Description = "查詢生成作業與結果")]
    [RequiresPermission("MAN.GENERATION.READ", "檢視 AI 生成")]
    [ProducesResponseType(typeof(PagedResult<GenerationJobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<GenerationJobDto>>> Search([FromBody] GenerationJobQueryRequest request, CancellationToken ct = default) =>
        Ok(await generation.SearchAsync(request, ct).ConfigureAwait(false));

    /// <summary>取得單一生成作業</summary>
    [HttpGet("jobs/{id:guid}")]
    [Function("GetGenerationJob", "取得 AI 生成作業", Order = 4, Description = "取得單一生成作業狀態")]
    [RequiresPermission("MAN.GENERATION.READ", "檢視 AI 生成")]
    [ProducesResponseType(typeof(GenerationJobDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GenerationJobDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await generation.GetAsync(id, ct).ConfigureAwait(false));
}
