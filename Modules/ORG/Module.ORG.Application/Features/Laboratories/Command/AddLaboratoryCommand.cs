using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGS.Modules.ORG.Infrastructure.Repositories;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System.ComponentModel.DataAnnotations;


namespace SGS.Modules.ORG.Application.Features.Laboratories.Command
{

    /// <summary>
    /// 新增實驗室
    /// </summary>`
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
        string ? Location = null,

        [StringLength(200, ErrorMessage = "描述說明長度不能超過 200 個字元。")]
    string? Description = null

    ) : IRequest<Result<LaboratoryDto>>;

    /// <summary>
    /// 
    /// </summary>
    public sealed class AddLaboratoryCommandHandler : IRequestHandler<AddLaboratoryCommand, Result<LaboratoryDto>>
    {
        private readonly IOrganizationService _orgService;

        public AddLaboratoryCommandHandler(IOrganizationService orgService)
        {
            _orgService = orgService ?? throw new ArgumentNullException(nameof(orgService));
        }

        public async Task<Result<LaboratoryDto>> Handle(AddLaboratoryCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));

            try
            {
                int level = 1;
                Guid? tenantLabId = null;

                // 1. 若有 ParentId，向 Service / Repository 取得父節點資訊以推算 Level 與 TenantLabId 繼承
                if (request.ParentId.HasValue && request.ParentId.Value > 0)
                {
                    var parentEntity = await _orgService.GetOrganizationByIdAsync(request.ParentId.Value, cancellationToken).ConfigureAwait(false);
                    if (parentEntity is null)
                    {
                        return Error.NotFound("ORG_PARENT_NOT_FOUND", $"找不到指定的父級組織 (ID: {request.ParentId.Value})。");
                    }

                    level = parentEntity.Level + 1;
                    tenantLabId = parentEntity.TenantLabId; // 繼承父節點租戶 ID
                }
                else
                {
                    // Level = 1 頂層實體實驗室，自動給予全新 UUIDv7 租戶識別碼
                    tenantLabId = Guid.CreateVersion7();
                }

                // 2. 正確帶入計算後的 level 參數實例化 Organization 領域實體
                var entity = new Organization(
                    name: request.Name,
                    code: request.Code,
                    level: level,
                    parentId: request.ParentId,
                    tenantLabId: tenantLabId,
                    location: request.Location,
                    description: request.Description
                );

                // 3. 呼叫 Application Service 執行持久化與樹狀物化路徑 (NodePath) 運算
                var createdEntity = await _orgService.CreateOrganizationAsync(entity, cancellationToken).ConfigureAwait(false);

                var dto = new LaboratoryDto
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

                return dto;
            }
            catch (ArgumentException ex)
            {
                return Error.Validation("ORG_VALIDATION_FAILED", ex.Message);
            }
            catch (Exception ex)
            {
                return Error.Failure("ORG_CREATE_FAILED", $"新增實驗室時發生系統錯誤: {ex.Message}");
            }
        }
    }

}
