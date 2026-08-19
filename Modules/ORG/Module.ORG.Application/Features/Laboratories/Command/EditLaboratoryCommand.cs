using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System.ComponentModel.DataAnnotations;


namespace SGS.Modules.ORG.Application.Features.Laboratories.Command
{
    /// <summary>
    /// 編輯實驗室/組織名稱與描述指令
    /// </summary>
    public sealed record EditLaboratoryCommand(

        int Id,

        [Required(ErrorMessage = "實驗室名稱為必填欄位。")]
        [StringLength(50, ErrorMessage = "實驗室名稱長度不能超過 50 個字元。")]
        string Name,

        [StringLength(200, ErrorMessage = "描述說明長度不能超過 200 個字元。")]
        string? Description = null,

        [StringLength(100, ErrorMessage = "位置長度不能超過 100 個字元。")]
        string? Location = null

    ) : IRequest<Result<LaboratoryDto>>;




    /// <summary>
    /// 編輯實驗室/組織指令處理器
    /// </summary>
    public sealed class EditLaboratoryCommandHandler : IRequestHandler<EditLaboratoryCommand, Result<LaboratoryDto>>
    {
        private readonly IOrganizationService _organizationService;
        private readonly ILogger<EditLaboratoryCommandHandler> _logger;

        public EditLaboratoryCommandHandler(
            IOrganizationService organizationService,
            ILogger<EditLaboratoryCommandHandler> logger)
        {
            _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<LaboratoryDto>> Handle(EditLaboratoryCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));

            try
            {
                // 1. 業務邏輯驗證：確認目標實體是否存在
                var entity = await _organizationService
                    .GetOrganizationByIdAsync(request.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (entity is null)
                {
                    _logger.LogWarning("嘗試編輯不存在的實驗室，ID: {Id}", request.Id);
                    return Error.NotFound("Laboratory.NotFound", $"找不到 ID 為 {request.Id} 的實驗室。");
                }

                // 2. 進行部分欄位更新 (避免使用物件初始化語法破壞封裝與 Domain 狀態)
                // 若 Organization 有提供專屬領域更新方法，應優先使用該方法
                var patchedOrg = await _organizationService
                    .PatchOrganizationTreeAsync(entity, cancellationToken)
                    .ConfigureAwait(false);

                if (patchedOrg is null)
                {
                    return Error.Failure("Laboratory.UpdateFailed", $"編輯實驗室 {request.Name} 失敗。");
                }

                // 3. 投射為 DTO
                var dto = new LaboratoryDto
                {
                    Id = patchedOrg.Id,
                    ParentId = patchedOrg.ParentId,
                    TenantLabId = patchedOrg.TenantLabId,
                    Code = patchedOrg.Code,
                    Name = patchedOrg.Name,
                    Location = patchedOrg.Location,
                    Description = patchedOrg.Description,
                    NodePath = patchedOrg.NodePath,
                    Level = patchedOrg.Level
                };

                _logger.LogInformation("成功編輯 ID 為 {Id} 的實驗室名稱為 {Name}。", patchedOrg.Id, patchedOrg.Name);

                // 4. 利用 Result<T> 隱式轉換糖直接回傳 DTO
                return dto;
            }
            catch (ArgumentException ex)
            {
                return Error.Validation("Laboratory.ValidationFailed", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "編輯 ID 為 {Id} 的實驗室時發生未預期系統錯誤。", request.Id);

                // 5. 將例外統一封裝為 Result.Failure 回傳，遵循專案 Result Pattern 標準
                return Error.Failure("Laboratory.EditFailed", $"編輯實驗室 {request.Name} 時發生系統錯誤: {ex.Message}");
            }
        }
    }

}
