using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGS.Modules.ORG.Application.Abstractions;


namespace SGS.Modules.ORG.Application.Features.Laboratories.Command
{

    /// <summary>
    /// 刪除實驗室/組織節點指令
    /// </summary>
    /// <param name="Id">要刪除的實驗室/組織識別碼</param>
    public sealed record DeleteLaboratoryCommand(int Id) : IRequest<Result<bool>>;


    /// <summary>
    /// 刪除實驗室/組織節點處理器
    /// </summary>
    public sealed class DeleteLaboratoryCommandHandler : IRequestHandler<DeleteLaboratoryCommand, Result<bool>>
    {
        private readonly IOrganizationService _organizationService;
        private readonly ILogger<DeleteLaboratoryCommandHandler> _logger;

        public DeleteLaboratoryCommandHandler(
            IOrganizationService organizationService,
            ILogger<DeleteLaboratoryCommandHandler> logger)
        {
            _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<bool>> Handle(DeleteLaboratoryCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));

            try
            {
                // 1. 業務邏輯驗證：確認節點是否存在 (必須傳遞 cancellationToken)
                var orgLab = await _organizationService
                    .GetOrganizationByIdAsync(request.Id, cancellationToken)
                    .ConfigureAwait(false);

                if (orgLab is null)
                {
                    _logger.LogWarning("嘗試刪除不存在的實驗室節點，ID: {Id}", request.Id);
                    return Error.NotFound("Laboratory.NotFound", $"找不到 ID 為 {request.Id} 的實驗室。");
                }

                // 2. 執行刪除作業 (連同子樹軟刪除與物化路徑維護)
                await _organizationService
                    .DeleteOrganizationAsync(request.Id, cancellationToken)
                    .ConfigureAwait(false);

                _logger.LogInformation("成功刪除 ID 為 {Id} 的實驗室及其相關子節點。", request.Id);

                // 3. 善用 Result<T> 隱式轉換糖回傳成功狀態
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刪除 ID 為 {Id} 的實驗室時發生未預期錯誤。", request.Id);

                // 4. 嚴禁在 Handler 中重新拋出通用 Exception，必須包裹為 Result.Failure 回傳
                return Error.Unauthorized("Laboratory.DeleteFailed", $"刪除實驗室失敗: {ex.Message}");
            }
        }


    }
}
