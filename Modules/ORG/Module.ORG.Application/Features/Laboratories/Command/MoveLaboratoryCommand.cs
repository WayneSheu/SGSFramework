using MediatR;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using SGSFramework.Core.Errors;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Command
{
    /// <summary>
    /// 搬移組織/實驗室樹狀節點（及其完整子樹）指令
    /// </summary>
    /// <param name="NodeId">要搬移的目標節點識別碼</param>
    /// <param name="NewParentId">新的父節點識別碼（若搬移至頂層則傳入 null）</param>
    public sealed record MoveLaboratoryCommand(
        [Required(ErrorMessage = "必須指定要搬移的目標節點 ID。")]
        int NodeId,

        int? NewParentId = null
    ) : IRequest<Result<LaboratoryDto>>;

    /// <summary>
    /// 搬移組織/實驗室樹狀節點指令處理器
    /// </summary>
    public sealed class MoveLaboratoryCommandHandler : IRequestHandler<MoveLaboratoryCommand, Result<LaboratoryDto>>
    {
        private readonly IOrganizationService _organizationService;
        private readonly ILogger<MoveLaboratoryCommandHandler> _logger;

        public MoveLaboratoryCommandHandler(
            IOrganizationService organizationService,
            ILogger<MoveLaboratoryCommandHandler> logger)
        {
            _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<LaboratoryDto>> Handle(MoveLaboratoryCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));

            try
            {
                // 1. 驗證被搬移節點是否存在
                var targetNode = await _organizationService
                    .GetOrganizationByIdAsync(request.NodeId, cancellationToken)
                    .ConfigureAwait(false);

                if (targetNode is null)
                {
                    _logger.LogWarning("嘗試搬移不存在的實驗室節點，NodeId: {NodeId}", request.NodeId);
                    return Error.NotFound("Laboratory.NodeNotFound", $"找不到 ID 為 {request.NodeId} 的實驗室節點。");
                }

                // 2. 驗證新父節點是否存在（若 NewParentId 有值）
                if (request.NewParentId.HasValue)
                {
                    if (request.NodeId == request.NewParentId.Value)
                    {
                        return Error.Validation("Laboratory.InvalidMove", "無法將節點搬移至自身下方。");
                    }

                    var newParentNode = await _organizationService
                        .GetOrganizationByIdAsync(request.NewParentId.Value, cancellationToken)
                        .ConfigureAwait(false);

                    if (newParentNode is null)
                    {
                        _logger.LogWarning("嘗試搬移至不存在的父節點，NewParentId: {NewParentId}", request.NewParentId.Value);
                        return Error.NotFound("Laboratory.ParentNotFound", $"找不到 ID 為 {request.NewParentId.Value} 的目標父節點。");
                    }

                    // 防範循環引用：目標父節點的 NodePath 不得包含當前被搬移節點的路徑
                    if (!string.IsNullOrEmpty(targetNode.NodePath) &&
                        !string.IsNullOrEmpty(newParentNode.NodePath) &&
                        newParentNode.NodePath.StartsWith($"{targetNode.NodePath}/"))
                    {
                        return Error.Validation("Laboratory.CircularDependency", "無法將父節點移動至其子孫節點下方，這會造成循環引用。");
                    }
                }

                // 3. 呼叫 Application Service 執行子樹搬移（包含 NodePath 重新計算與 DB 異動）
                await _organizationService
                    .MoveOrganizationAsync(request.NodeId, request.NewParentId ?? 0, cancellationToken)
                    .ConfigureAwait(false);

                // 4. 重新取得搬移後的最新節點資訊
                var updatedEntity = await _organizationService
                    .GetOrganizationByIdAsync(request.NodeId, cancellationToken)
                    .ConfigureAwait(false);

                if (updatedEntity is null)
                {
                    return Error.Failure("Laboratory.MoveFailed", $"搬移節點 {request.NodeId} 後無法讀取最新狀態。");
                }

                var dto = new LaboratoryDto
                {
                    Id = updatedEntity.Id,
                    ParentId = updatedEntity.ParentId,
                    TenantLabId = updatedEntity.TenantLabId,
                    Code = updatedEntity.Code,
                    Name = updatedEntity.Name,
                    Location = updatedEntity.Location,
                    Description = updatedEntity.Description,
                    NodePath = updatedEntity.NodePath,
                    Level = updatedEntity.Level
                };

                _logger.LogInformation("成功將節點 ID {NodeId} 搬移至新父節點 ID {NewParentId}。", request.NodeId, request.NewParentId);

                return dto;
            }
            catch (InvalidOperationException ex)
            {
                return Error.Validation("Laboratory.MoveValidationFailed", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搬移節點 ID {NodeId} 至 {NewParentId} 時發生未預期系統錯誤。", request.NodeId, request.NewParentId);
                return Error.Unexpected("Laboratory.MoveFailed", $"搬移節點時發生系統錯誤: {ex.Message}");
            }
        }
    }

}
