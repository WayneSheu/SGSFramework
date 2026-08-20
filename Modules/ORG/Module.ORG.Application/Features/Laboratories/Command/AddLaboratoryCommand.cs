using MediatR;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Generators;
using SGSFramework.Core.Results;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Command;

/// <summary>
/// 新增實驗室/組織節點指令
/// </summary>
public sealed record AddLaboratoryCommand(
    [Required(ErrorMessage = "組織代碼為必填項目。")]
    [StringLength(20, ErrorMessage = "組織代碼長度不能超過 20 個字元。")]
    string Code,

    [Required(ErrorMessage = "組織名稱為必填項目。")]
    [StringLength(50, ErrorMessage = "組織名稱長度不能超過 50 個字元。")]
    string Name,

    int? ParentId = null,

    [StringLength(100, ErrorMessage = "位置長度不能超過 100 個字元。")]
    string? Location = null,

    [StringLength(200, ErrorMessage = "描述說明長度不能超過 200 個字元。")]
    string? Description = null
) : IRequest<Result<LaboratoryDto>>;

/// <summary>
/// 新增實驗室 Command Handler (精準控制 Level 1 類別層與 Level 2+ 區域實驗室 TenantLabId 邏輯)
/// </summary>
public sealed class AddLaboratoryCommandHandler : IRequestHandler<AddLaboratoryCommand, Result<LaboratoryDto>>
{
    private readonly IOrganizationService _orgService;
    private readonly IGuidGenerator _guidGenerator;

    public AddLaboratoryCommandHandler(
        IOrganizationService orgService,
        IGuidGenerator guidGenerator)
    {
        _orgService = orgService ?? throw new ArgumentNullException(nameof(orgService));
        _guidGenerator = guidGenerator ?? throw new ArgumentNullException(nameof(guidGenerator));
    }

    public async Task<Result<LaboratoryDto>> Handle(AddLaboratoryCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        try
        {
            string? parentNodePath = null;
            Guid? tenantLabId = null;

            if (request.ParentId.HasValue && request.ParentId.Value > 0)
            {
                var parentEntity = await _orgService.GetOrganizationByIdAsync(request.ParentId.Value, cancellationToken).ConfigureAwait(false);
                if (parentEntity is null)
                {
                    return Error.NotFound("ORG_PARENT_NOT_FOUND", $"找不到指定的父級組織 (ID: {request.ParentId.Value})。");
                }

                parentNodePath = parentEntity.NodePath;
                int targetLevel = parentEntity.Level + 1;

                tenantLabId = (targetLevel == 2)
                    ? _guidGenerator.CreateVersion7()
                    : parentEntity.TenantLabId;
            }

            // 使用 Context 進行解耦建立，未來新增屬性無需修改 Create 方法簽名
            // 使用物件初始化運算子語法建立 Context，具備高可讀性與 required 編譯期驗證
            var createContext = new OrganizationCreateContext
            {
                Name = request.Name,
                Code = request.Code,
                ParentId = request.ParentId,
                ParentNodePath = parentNodePath,
                TenantLabId = tenantLabId,
                Location = request.Location,
                Description = request.Description
            };

            var entity = Organization.Create(createContext);

            var createdEntity = await _orgService.CreateOrganizationAsync(entity, cancellationToken).ConfigureAwait(false);

            return new LaboratoryDto
            {
                Id = createdEntity.Id,
                ParentId = createdEntity.ParentId,
                TenantLabId = createdEntity.TenantLabId,
                Code = createdEntity.Code,
                Name = createdEntity.Name,
                Location = createdEntity.Location,
                Description = createdEntity.Description,
                NodePath = createdEntity.NodePath,
                Level = createdEntity.Level
            };
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("ORG_VALIDATION_FAILED", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Failure("ORG_OPERATIONAL_FAILED", ex.Message);
        }
        catch (Exception ex)
        {
            return Error.Failure("ORG_CREATE_FAILED", $"新增實驗室時發生系統錯誤: {ex.Message}");
        }
    }
}