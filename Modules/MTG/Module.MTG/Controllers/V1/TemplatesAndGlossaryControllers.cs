using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.MTG.Application.Contracts;
using SGS.Modules.MTG.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;

namespace SGS.Modules.MTG.Controllers.V1;

/// <summary>場景 Prompt 模板：四種內建場景可調整提示詞，亦可新增自訂場景。</summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/mtg/templates")]
[ControllerTitle("場景模板", Icon = "fa-solid fa-file-lines", Order = 2, Description = "會議場景的 Map / Reduce 提示詞模板")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class TemplatesController(PromptTemplateService templates) : MtgControllerBase
{
    /// <summary>列出場景模板（activeOnly=true 時僅啟用者）。選單項目以權限鍵為 key，故須使用獨立權限，不可與會議清單共用。</summary>
    [HttpGet]
    [Function("ListMeetingTemplates", "場景模板", Icon = "fa-solid fa-file-lines", Order = 1, Description = "會議場景提示詞模板維護", IsMenu = true, Path = "/sgs.modules.mtg/templates")]
    [RequiresPermission("MTG.TEMPLATE.READ", "檢視場景模板")]
    [ProducesResponseType(typeof(IReadOnlyList<PromptTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PromptTemplateDto>>> List([FromQuery] bool activeOnly = false, CancellationToken ct = default) =>
        Ok(await templates.ListAsync(activeOnly, ct).ConfigureAwait(false));

    /// <summary>啟用中的場景選項（供建立會議、選擇紀要場景的下拉選單使用；只需會議檢視權限）</summary>
    [HttpGet("options")]
    [Function("ListMeetingScenarioOptions", "場景選項", Order = 4, Description = "取得啟用中的會議場景選項")]
    [RequiresPermission("MTG.MEETING.READ", "檢視會議")]
    [ProducesResponseType(typeof(IReadOnlyList<PromptTemplateDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PromptTemplateDto>>> Options(CancellationToken ct = default) =>
        Ok(await templates.ListAsync(activeOnly: true, ct).ConfigureAwait(false));

    /// <summary>新增自訂場景模板</summary>
    [HttpPost("{scenarioCode}")]
    [Function("CreateMeetingTemplate", "新增場景模板", Order = 2, Description = "新增自訂會議場景模板")]
    [RequiresPermission("MTG.TEMPLATE.MANAGE", "維護場景模板", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(PromptTemplateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PromptTemplateDto>> Create(string scenarioCode, [FromBody] SavePromptTemplateRequest request, CancellationToken ct = default) =>
        FromResult(await templates.CreateAsync(Actor, scenarioCode, request, ct).ConfigureAwait(false));

    /// <summary>更新場景模板（版本遞增）</summary>
    [HttpPut("{scenarioCode}")]
    [Function("UpdateMeetingTemplate", "更新場景模板", Order = 3, Description = "更新場景模板提示詞或啟用狀態")]
    [RequiresPermission("MTG.TEMPLATE.MANAGE", "維護場景模板", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(PromptTemplateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PromptTemplateDto>> Update(string scenarioCode, [FromBody] SavePromptTemplateRequest request, CancellationToken ct = default) =>
        FromResult(await templates.UpdateAsync(Actor, scenarioCode, request, ct).ConfigureAwait(false));
}

/// <summary>術語表：標準詞與同音誤聽校正，逐字稿寫入時套用。</summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/mtg/glossary")]
[ControllerTitle("術語表", Icon = "fa-solid fa-spell-check", Order = 3, Description = "企業術語與同音誤聽校正表")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class GlossaryController(GlossaryService glossary) : MtgControllerBase
{
    /// <summary>分頁查詢術語</summary>
    [HttpPost("search")]
    [Function("SearchMeetingGlossary", "術語表", Icon = "fa-solid fa-spell-check", Order = 1, Description = "企業術語與同音誤聽校正表", IsMenu = true, Path = "/sgs.modules.mtg/glossary")]
    [RequiresPermission("MTG.GLOSSARY.READ", "檢視術語表")]
    [ProducesResponseType(typeof(PagedResult<GlossaryEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<GlossaryEntryDto>>> Search([FromBody] GlossaryQueryRequest request, CancellationToken ct = default) =>
        Ok(await glossary.SearchAsync(request, ct).ConfigureAwait(false));

    /// <summary>啟用中的術語（供桌面端快取，在本機即時做同音字校正）</summary>
    [HttpGet("terms")]
    [Function("ListMeetingGlossaryTerms", "術語同步", Order = 5, Description = "桌面端取得啟用中的術語與誤聽詞")]
    [RequiresPermission("MTG.CLIENT.UPLOAD", "桌面端上傳逐字稿", ActionCategory.Operational)]
    [ProducesResponseType(typeof(IReadOnlyList<GlossaryTermDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GlossaryTermDto>>> Terms(CancellationToken ct = default) =>
        Ok(await glossary.GetActiveTermsAsync(ct).ConfigureAwait(false));

    /// <summary>新增術語</summary>
    [HttpPost]
    [Function("CreateMeetingGlossaryEntry", "新增術語", Order = 2, Description = "新增術語與誤聽詞")]
    [RequiresPermission("MTG.GLOSSARY.MANAGE", "維護術語表", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(GlossaryEntryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GlossaryEntryDto>> Create([FromBody] SaveGlossaryEntryRequest request, CancellationToken ct = default) =>
        FromResult(await glossary.CreateAsync(Actor, request, ct).ConfigureAwait(false));

    /// <summary>更新術語</summary>
    [HttpPut("{id:guid}")]
    [Function("UpdateMeetingGlossaryEntry", "更新術語", Order = 3, Description = "更新術語與誤聽詞")]
    [RequiresPermission("MTG.GLOSSARY.MANAGE", "維護術語表", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(GlossaryEntryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GlossaryEntryDto>> Update(Guid id, [FromBody] SaveGlossaryEntryRequest request, CancellationToken ct = default) =>
        FromResult(await glossary.UpdateAsync(Actor, id, request, ct).ConfigureAwait(false));

    /// <summary>刪除術語</summary>
    [HttpDelete("{id:guid}")]
    [Function("DeleteMeetingGlossaryEntry", "刪除術語", Order = 4, Description = "刪除術語")]
    [RequiresPermission("MTG.GLOSSARY.MANAGE", "維護術語表", ActionCategory.Administrative)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct = default) =>
        NoContentFrom(await glossary.DeleteAsync(id, ct).ConfigureAwait(false));
}
