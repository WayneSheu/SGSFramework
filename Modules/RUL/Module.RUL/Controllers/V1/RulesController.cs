using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.RUL.Application.Contracts;
using SGS.Modules.RUL.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;
using SGSFramework.Core.Rules;

namespace SGS.Modules.RUL.Controllers.V1;

/// <summary>動態規則：查詢、運算式驗證、試跑、版本與測試案例。規則內容的異動一律經變更申請（ChangesController）審批後生效。</summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/rul/rules")]
[ControllerTitle("動態規則", Icon = "fa-solid fa-scale-balanced", Order = 1, Description = "資料權限、表單驗證、風控與計價折扣的動態規則")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class RulesController(RuleAdminService rules, RuleTestService tests) : RulControllerBase
{
    /// <summary>查詢動態規則</summary>
    [HttpPost("search")]
    [Function("SearchRules", "動態規則", Icon = "fa-solid fa-scale-balanced", Order = 1, Description = "動態規則清單", IsMenu = true, Path = "/sgs.modules.rul/rules")]
    [RequiresPermission("RUL.RULE.READ", "檢視動態規則")]
    [ProducesResponseType(typeof(PagedList<RuleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedList<RuleDto>>> Search([FromBody] RuleSearchRequest request, CancellationToken ct = default) =>
        Ok(await rules.SearchAsync(request ?? new RuleSearchRequest(), ct).ConfigureAwait(false));

    /// <summary>取得單一規則</summary>
    [HttpGet("{id:long}")]
    [Function("GetRule", "取得單一規則", Order = 10, Description = "依 Id 取得規則內容")]
    [RequiresPermission("RUL.RULE.READ", "檢視動態規則")]
    [ProducesResponseType(typeof(RuleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RuleDto>> Get(long id, CancellationToken ct = default) =>
        FromResult(await rules.GetAsync(id, ct).ConfigureAwait(false));

    /// <summary>可被規則引用的目標實體與欄位（白名單）</summary>
    [HttpGet("targets")]
    [Function("GetRuleTargets", "可引用的目標實體", Order = 11, Description = "取得可被規則引用的目標實體與欄位（白名單）")]
    [RequiresPermission("RUL.RULE.READ", "檢視動態規則")]
    [ProducesResponseType(typeof(IReadOnlyList<RuleTargetDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<RuleTargetDto>> Targets() => Ok(rules.GetTargets());

    /// <summary>驗證運算式語法與白名單</summary>
    [HttpPost("validate")]
    [Function("ValidateRuleExpression", "驗證運算式", Order = 2, Description = "檢查運算式語法與白名單")]
    [RequiresPermission("RUL.RULE.MANAGE", "維護動態規則", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(ValidateExpressionResult), StatusCodes.Status200OK)]
    public ActionResult<ValidateExpressionResult> Validate([FromBody] ValidateExpressionRequest request) =>
        Ok(rules.ValidateExpression(request));

    /// <summary>以範例資料試跑一條規則（不影響線上規則）</summary>
    [HttpPost("dry-run")]
    [Function("DryRunRule", "規則試跑", Order = 3, Description = "以範例 JSON 試跑規則條件")]
    [RequiresPermission("RUL.RULE.MANAGE", "維護動態規則", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(RuleTestOutcome), StatusCodes.Status200OK)]
    public ActionResult<RuleTestOutcome> DryRun([FromBody] DryRunRequest request) =>
        FromResult(rules.DryRun(request));

    /// <summary>規則版本歷史</summary>
    [HttpGet("{id:long}/versions")]
    [Function("GetRuleVersions", "規則版本歷史", Order = 12, Description = "查詢規則核准後的版本快照")]
    [RequiresPermission("RUL.RULE.READ", "檢視動態規則")]
    [ProducesResponseType(typeof(IReadOnlyList<RuleVersionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RuleVersionDto>>> Versions(long id, CancellationToken ct = default) =>
        Ok(await rules.GetVersionsAsync(id, ct).ConfigureAwait(false));

    /// <summary>規則的測試案例</summary>
    [HttpGet("{id:long}/tests")]
    [Function("GetRuleTestCases", "規則測試案例清單", Order = 13, Description = "查詢規則的測試案例")]
    [RequiresPermission("RUL.RULE.READ", "檢視動態規則")]
    [ProducesResponseType(typeof(IReadOnlyList<TestCaseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TestCaseDto>>> Tests(long id, CancellationToken ct = default) =>
        Ok(await tests.ListAsync(id, ct).ConfigureAwait(false));

    /// <summary>新增測試案例</summary>
    [HttpPost("{id:long}/tests")]
    [Function("AddRuleTestCase", "新增測試案例", Order = 4, Description = "為規則新增測試案例")]
    [RequiresPermission("RUL.RULE.MANAGE", "維護動態規則", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(TestCaseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TestCaseDto>> AddTest(long id, [FromBody] TestCaseInput input, CancellationToken ct = default) =>
        FromResult(await tests.AddAsync(Actor, id, input, ct).ConfigureAwait(false));

    /// <summary>刪除測試案例</summary>
    [HttpDelete("tests/{testId:long}")]
    [Function("DeleteRuleTestCase", "刪除測試案例", Order = 5, Description = "刪除規則測試案例")]
    [RequiresPermission("RUL.RULE.MANAGE", "維護動態規則", ActionCategory.Administrative)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteTest(long testId, CancellationToken ct = default) =>
        NoContentFrom(await tests.DeleteAsync(testId, ct).ConfigureAwait(false));

    /// <summary>執行規則的全部測試案例</summary>
    [HttpPost("{id:long}/tests/run")]
    [Function("RunRuleTestCases", "執行規則測試案例", Order = 14, Description = "以規則目前內容執行全部測試案例")]
    [RequiresPermission("RUL.RULE.READ", "檢視動態規則")]
    [ProducesResponseType(typeof(IReadOnlyList<TestCaseRunDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TestCaseRunDto>>> RunTests(long id, CancellationToken ct = default) =>
        FromResult(await tests.RunAsync(id, ct).ConfigureAwait(false));
}
