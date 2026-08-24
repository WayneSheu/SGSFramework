using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Messagings;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Queries 
{

    /// <summary>
    /// 取得特定使用者可存取實驗室清單之查詢指令 (Positional Sealed Record)
    /// </summary>
    public sealed record GetAccessibleLaboratoriesQuery(string UserId) : IQuery<Result<List<AccessibleLaboratoryDto>>>
    {
        /// <summary>
        /// 驗證 Query 參數合法性
        /// </summary>
        public Result Validate()
        {
            if (string.IsNullOrWhiteSpace(UserId))
            {
                return Result.Failure(
                    Error.Validation("ORG_USER_ID_REQUIRED", "使用者識別碼不可為空。"));
            }

            if (!Guid.TryParse(UserId, out _))
            {
                return Result.Failure(
                    Error.Validation("ORG_INVALID_USER_ID_FORMAT", "使用者識別碼格式不正確 (需為 Guid)。"));
            }

            return Result.Success();
        }
    }


    /// <summary>
    /// 取得使用者可存取實驗室清單查詢處理器
    /// </summary>
    public class GetAccessibleLaboratoriesQueryHandler : IRequestHandler<GetAccessibleLaboratoriesQuery, Result<List<AccessibleLaboratoryDto>>>
    {
        private readonly IUserLabRepository _userLabRepository; // 來自主專案 (Host)
        private readonly ORGDbContext _orgDbContext;             // 來自 ORG 模組
        private readonly ILogger<GetAccessibleLaboratoriesQueryHandler> _logger;

        public GetAccessibleLaboratoriesQueryHandler(
            IUserLabRepository userLabRepository,
            ORGDbContext orgDbContext,
            ILogger<GetAccessibleLaboratoriesQueryHandler> logger)
        {
            _userLabRepository = userLabRepository ?? throw new ArgumentNullException(nameof(userLabRepository));
            _orgDbContext = orgDbContext ?? throw new ArgumentNullException(nameof(orgDbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<List<AccessibleLaboratoryDto>>> Handle(
            GetAccessibleLaboratoriesQuery request,
            CancellationToken cancellationToken)
        {
            var validationResult = request.Validate();
            if (validationResult.IsFailure)
            {
                return Result.Failure<List<AccessibleLaboratoryDto>>(validationResult.Error);
            }

            var userIdGuid = Guid.Parse(request.UserId);

            try
            {
                // 1. 呼叫主專案 Repository 取得用戶直屬的 Mapping 記錄
                var mappings = await _userLabRepository.GetAccessibleLabsAsync(userIdGuid, cancellationToken);
                if (!mappings.Any())
                {
                    return Result.Success(new List<AccessibleLaboratoryDto>());
                }
                // 取得所有 LabId 以便後續查詢 ORG 模組的 Laboratory 實體
                var labIds = mappings.Select(m => m.TenantLabId).ToList();

                // 2. 聯集 ORG 模組的 Laboratory 實體以補齊 Name、Code 及樹狀繼承節點
                var labs = await _orgDbContext.Organizations
                    .AsNoTracking()
                    .Where(x => labIds.Any().ToString().Contains(x.TenantLabId.Value.ToString()) && !x.IsDeleted)
                    .ToListAsync(cancellationToken);
                
                var mappingDict = mappings.ToDictionary(m => m.TenantLabId);

                var result = labs.Select(lab =>
                {
                    // 安全取得映射資料，若為繼承節點則 null
                    var mapping = mappingDict.GetValueOrDefault(lab.TenantLabId.Value);
                    var isDirectlyAssigned = mapping is not null;

                    return new AccessibleLaboratoryDto
                    {
                        Id = lab.Id,
                        ParentId = lab.ParentId,
                        TenantLabId = lab.TenantLabId,
                        EffectiveTenantLabId = lab.TenantLabId ?? lab.EffectiveTenantLabId,
                        Code = lab.Code,
                        Name = lab.Name,
                        NodePath = lab.NodePath,
                        Level = lab.Level,
                        IsDirectlyAssigned = isDirectlyAssigned,
                        IsPrimary = mapping?.IsPrimary ?? false,
                        JobTitle = mapping?.JobTitle,
                        IsRegional = lab.Level == 2 && lab.TenantLabId.HasValue// 這裡假設 IsRegional 是 Laboratory 實體的一個屬性
                    };
                }).ToList();

                return Result.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching accessible laboratories in ORG module. UserId: {UserId}", request.UserId);
                return Result.Failure<List<AccessibleLaboratoryDto>>(
                    Error.Failure("ORG_ACCESSIBLE_LABS_FETCH_ERROR", "無法順利取得可存取實驗室清單。"));
            }
        }
    }

}
