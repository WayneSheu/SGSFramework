using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGS.Modules.QTN.Application.Contracts;
using SGS.Modules.QTN.Application.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions.Enums;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.QTN.Controllers.V1;

/// <summary>報價客戶主檔：統編、名稱、聯絡窗口（Email／電話落地加密）、戰略標竿客戶旗標。</summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/qtn/customers")]
[ControllerTitle("報價客戶", Icon = "fa-solid fa-building", Order = 7, Description = "報價客戶主檔")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class CustomersController(MasterDataService master) : QtnControllerBase
{
    [HttpPost("search")]
    [Function("SearchQtnCustomers", "報價客戶", Icon = "fa-solid fa-building", Order = 1, Description = "報價客戶主檔", IsMenu = true, Path = "/sgs.modules.qtn/customers")]
    [RequiresPermission("QTN.CUSTOMER.READ", "檢視報價客戶")]
    [ProducesResponseType(typeof(PagedResult<CustomerListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerListDto>>> Search([FromBody] CustomerSearchRequest request, CancellationToken ct = default) =>
        Ok(await master.SearchCustomersAsync(request ?? new CustomerSearchRequest(), ct).ConfigureAwait(false));

    [HttpGet("{id:guid}")]
    [Function("GetQtnCustomer", "客戶詳情", Order = 2, Description = "檢視客戶與聯絡窗口")]
    [RequiresPermission("QTN.CUSTOMER.READ", "檢視報價客戶")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await master.GetCustomerAsync(id, ct).ConfigureAwait(false));

    [HttpPost]
    [Function("CreateQtnCustomer", "新增客戶", Order = 3, Description = "新增報價客戶")]
    [RequiresPermission("QTN.CUSTOMER.MANAGE", "維護報價客戶", ActionCategory.Operational)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] UpsertCustomerRequest request, CancellationToken ct = default) =>
        FromResult(await master.CreateCustomerAsync(Actor, request, ct).ConfigureAwait(false));

    [HttpPut("{id:guid}")]
    [Function("UpdateQtnCustomer", "修改客戶", Order = 4, Description = "修改報價客戶")]
    [RequiresPermission("QTN.CUSTOMER.MANAGE", "維護報價客戶", ActionCategory.Operational)]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, [FromBody] UpsertCustomerRequest request, CancellationToken ct = default) =>
        FromResult(await master.UpdateCustomerAsync(Actor, id, request, ct).ConfigureAwait(false));
}

/// <summary>
/// 角色費率（SOP 6.1 roles）：內部成本單價為企業最高機密，僅 QTN.COST.READ 可見（後端過濾為 null）；調整費率須填原因並留歷程，
/// 不回溯影響已建立之報價（費率快照）。
/// </summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/qtn/rate-roles")]
[ControllerTitle("角色費率", Icon = "fa-solid fa-user-tag", Order = 8, Description = "角色內部成本單價與對外牌價")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class RateRolesController(MasterDataService master) : QtnControllerBase
{
    [HttpPost("search")]
    [Function("SearchQtnRateRoles", "角色費率", Icon = "fa-solid fa-user-tag", Order = 1, Description = "角色成本單價與牌價", IsMenu = true, Path = "/sgs.modules.qtn/rate-roles")]
    [RequiresPermission("QTN.RATE.READ", "檢視角色費率")]
    [ProducesResponseType(typeof(IReadOnlyList<RateRoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RateRoleDto>>> Search([FromBody] ListRequest? request, CancellationToken ct = default) =>
        Ok(await master.ListRateRolesAsync(await HasPermissionAsync(CostRead, ct).ConfigureAwait(false), request?.IncludeInactive ?? false, ct).ConfigureAwait(false));

    [HttpGet("{id:guid}/history")]
    [Function("QtnRateHistory", "費率歷程", Order = 2, Description = "角色費率調整歷程")]
    [RequiresPermission("QTN.RATE.READ", "檢視角色費率")]
    [ProducesResponseType(typeof(IReadOnlyList<RateRoleHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RateRoleHistoryDto>>> History(Guid id, CancellationToken ct = default) =>
        FromResult(await master.RateHistoryAsync(id, await HasPermissionAsync(CostRead, ct).ConfigureAwait(false), ct).ConfigureAwait(false));

    [HttpPost]
    [Function("CreateQtnRateRole", "新增角色費率", Order = 3, Description = "新增角色與費率")]
    [RequiresPermission("QTN.RATE.MANAGE", "維護角色費率", ActionCategory.Critical)]
    [ProducesResponseType(typeof(RateRoleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RateRoleDto>> Create([FromBody] UpsertRateRoleRequest request, CancellationToken ct = default)
    {
        if (!await HasPermissionAsync(CostRead, ct).ConfigureAwait(false)) return ProblemFrom(QtnError.Forbidden("維護成本單價須具 QTN.COST.READ。"));
        return FromResult(await master.CreateRateRoleAsync(Actor, request, ct).ConfigureAwait(false));
    }

    [HttpPut("{id:guid}")]
    [Function("UpdateQtnRateRole", "調整角色費率", Order = 4, Description = "調整角色費率（留歷程，不回溯既有報價）")]
    [RequiresPermission("QTN.RATE.MANAGE", "維護角色費率", ActionCategory.Critical)]
    [ProducesResponseType(typeof(RateRoleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RateRoleDto>> Update(Guid id, [FromBody] UpsertRateRoleRequest request, CancellationToken ct = default)
    {
        if (!await HasPermissionAsync(CostRead, ct).ConfigureAwait(false)) return ProblemFrom(QtnError.Forbidden("維護成本單價須具 QTN.COST.READ。"));
        return FromResult(await master.UpdateRateRoleAsync(Actor, id, request, ct).ConfigureAwait(false));
    }

    /// <summary>欄位級權限探測：具 QTN.COST.READ 者回傳 true（前端據以顯示成本欄位；登錄此權限碼供角色管理授權）。</summary>
    [HttpPost("access/cost")]
    [Function("QtnCostAccess", "檢視人力成本與毛利", Order = 9, Description = "可檢視內部成本單價、總成本與毛利率（欄位級權限）")]
    [RequiresPermission(CostRead, "檢視人力成本與毛利", ActionCategory.Critical)]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public ActionResult<bool> CostAccess() => Ok(true);

    /// <summary>欄位級權限探測：具 QTN.PRICE.READ 者回傳 true（業務：牌價、成交價與毛利燈號）。</summary>
    [HttpPost("access/price")]
    [Function("QtnPriceAccess", "檢視報價金額", Order = 10, Description = "可檢視牌價、成交價與毛利燈號（欄位級權限）")]
    [RequiresPermission(PriceRead, "檢視報價金額")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public ActionResult<bool> PriceAccess() => Ok(true);
}

/// <summary>標準 WBS 工時庫：報價拆解的模組範本（預設三點工時與角色），結案復盤偏差超過 ±15% 時校準。</summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/qtn/wbs-templates")]
[ControllerTitle("標準 WBS 工時庫", Icon = "fa-solid fa-sitemap", Order = 9, Description = "標準模組與預設三點工時")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class WbsTemplatesController(MasterDataService master) : QtnControllerBase
{
    [HttpPost("search")]
    [Function("SearchQtnWbsTemplates", "標準 WBS 工時庫", Icon = "fa-solid fa-sitemap", Order = 1, Description = "標準模組工時庫", IsMenu = true, Path = "/sgs.modules.qtn/wbs-templates")]
    [RequiresPermission("QTN.TEMPLATE.READ", "檢視標準工時庫")]
    [ProducesResponseType(typeof(IReadOnlyList<WbsTemplateListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WbsTemplateListDto>>> Search([FromBody] ListRequest? request, CancellationToken ct = default) =>
        Ok(await master.ListTemplatesAsync(request?.IncludeInactive ?? false, ct).ConfigureAwait(false));

    [HttpGet("{id:guid}")]
    [Function("GetQtnWbsTemplate", "標準模組詳情", Order = 2, Description = "檢視標準模組工作項")]
    [RequiresPermission("QTN.TEMPLATE.READ", "檢視標準工時庫")]
    [ProducesResponseType(typeof(WbsTemplateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WbsTemplateDto>> Get(Guid id, CancellationToken ct = default) =>
        FromResult(await master.GetTemplateAsync(id, ct).ConfigureAwait(false));

    [HttpPost]
    [Function("CreateQtnWbsTemplate", "新增標準模組", Order = 3, Description = "新增標準 WBS 模組")]
    [RequiresPermission("QTN.TEMPLATE.MANAGE", "維護標準工時庫", ActionCategory.Operational)]
    [ProducesResponseType(typeof(WbsTemplateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WbsTemplateDto>> Create([FromBody] UpsertWbsTemplateRequest request, CancellationToken ct = default) =>
        FromResult(await master.CreateTemplateAsync(Actor, request, ct).ConfigureAwait(false));

    [HttpPut("{id:guid}")]
    [Function("UpdateQtnWbsTemplate", "修改標準模組", Order = 4, Description = "修改標準 WBS 模組（基準工時異動版本 +1）")]
    [RequiresPermission("QTN.TEMPLATE.MANAGE", "維護標準工時庫", ActionCategory.Operational)]
    [ProducesResponseType(typeof(WbsTemplateDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WbsTemplateDto>> Update(Guid id, [FromBody] UpsertWbsTemplateRequest request, CancellationToken ct = default) =>
        FromResult(await master.UpdateTemplateAsync(Actor, id, request, ct).ConfigureAwait(false));
}

/// <summary>審批權責矩陣人選（SOP 5.2）：業務經理、PMO 總監、財務長、總經理、法務長。被指派者可檢視全部報價並於輪到時審批。</summary>
[ApiController]
[Authorize]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/qtn/approvers")]
[ControllerTitle("報價審批人設定", Icon = "fa-solid fa-user-shield", Order = 10, Description = "審批權責矩陣人選")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ApproversController(MasterDataService master) : QtnControllerBase
{
    [HttpPost("search")]
    [Function("SearchQtnApprovers", "報價審批人設定", Icon = "fa-solid fa-user-shield", Order = 1, Description = "審批權責矩陣人選", IsMenu = true, Path = "/sgs.modules.qtn/approvers")]
    [RequiresPermission("QTN.APPROVER.MANAGE", "設定報價審批人", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(IReadOnlyList<ApproverRoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApproverRoleDto>>> Search(CancellationToken ct = default) =>
        Ok(await master.ListApproversAsync(ct).ConfigureAwait(false));

    [HttpPut]
    [Function("SetQtnApprovers", "指派報價審批人", Order = 2, Description = "指派審批角色人選")]
    [RequiresPermission("QTN.APPROVER.MANAGE", "設定報價審批人", ActionCategory.Administrative)]
    [ProducesResponseType(typeof(IReadOnlyList<ApproverRoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApproverRoleDto>>> Set([FromBody] SetApproversRequest request, CancellationToken ct = default) =>
        FromResult(await master.SetApproversAsync(Actor, request, ct).ConfigureAwait(false));
}
