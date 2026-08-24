using MediatR;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;
using SGS.Modules.ORG.Infrastructure.Repositories;
using SGSFramework.Core.Helpers;
using SGSFramework.Core.Errors;
using SGS.Modules.ORG.Application.Abstractions;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Queries
{ 
    /// <summary>
    /// 依據實驗室 Id 取得該節點及其完整下階層樹狀結構之查詢指令
    /// </summary>
    /// <param name="Id">根節點實驗室識別碼</param>
    public sealed record GetLaboratoryTreeQuery(int Id) : IRequest<Result<LaboratoryDto>>;

    /// <summary>
    /// 依據實驗室 Id 取得該節點及其完整下階層樹狀結構之查詢處理器
    /// </summary>
    public sealed class GetLaboratoryTreeQueryHandler : IRequestHandler<GetLaboratoryTreeQuery, Result<LaboratoryDto>>
    {
        private readonly ILaboratoryRepository _repository;
        private readonly ILogger<GetLaboratoryTreeQueryHandler> _logger;

        public GetLaboratoryTreeQueryHandler(
            ILaboratoryRepository repository,
            ILogger<GetLaboratoryTreeQueryHandler> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
         
        public async Task<Result<LaboratoryDto>> Handle(GetLaboratoryTreeQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));

            try
            {
                var entities = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

                if (entities is null || !entities.Any())
                {
                    return Error.NotFound("ORG_LAB_TREE_NOT_FOUND", $"找不到 ID 為 {request.Id} 的實驗室或系統內無任何組織資料。");
                }

                // 1. 投射為 DTO 平舖清單
                var dtos = entities.Select(lab => new LaboratoryDto
                {
                    Id = lab.Id,
                    ParentId = lab.ParentId,
                    TenantLabId = lab.TenantLabId,
                    Code = lab.Code,
                    Name = lab.Name,
                    Location = lab.Location,
                    Description = lab.Description,
                    NodePath = lab.NodePath,
                    Level = lab.Level
                }).ToList();

                // 2. 呼叫特化型 TreeBuilderHelper，完成 EffectiveTenantLabId 的向上/向下繼承並組裝樹狀結構 (O(N))
                var fullTree = dtos.BuildTreeWithContext<int, LaboratoryDto, Guid?>(
                       contextSelector: (node, parentTenantId) =>
                       {
                           node.EffectiveTenantLabId = node.TenantLabId ?? parentTenantId;
                           return node.EffectiveTenantLabId;
                       },
                       rootParentId: null
                   );

                // 3. 尋找指定的目標根節點 (或使用內部遞迴尋找指定子樹)
                var targetNode = FindNodeInTree(fullTree, request.Id);

                if (targetNode is null)
                {
                    return Error.NotFound("ORG_LAB_NODE_NOT_FOUND", $"找不到 ID 為 {request.Id} 的實驗室節點。");
                }

                _logger.LogInformation("成功建構 ID 為 {Id} 的實驗室樹狀結構，包含 {ChildCount} 個子節點。", request.Id, targetNode.Children.Count);

                return targetNode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "建構 ID 為 {Id} 的實驗室樹狀結構時發生錯誤。", request.Id);

                return Error.Failure(
                    "ORG_LAB_BUILD_TREE_FAILED",
                    $"Failed to build laboratory tree for ID {request.Id}: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// 於樹狀結構中遞迴尋找指定的目標節點
        /// </summary>
        private static LaboratoryDto? FindNodeInTree(IEnumerable<LaboratoryDto> nodes, int targetId)
        {
            foreach (var node in nodes)
            {
                if (node.Id == targetId)
                {
                    return node;
                }

                var found = FindNodeInTree(node.Children, targetId);
                if (found is not null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
