using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Contracts.Queries;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.DTOs;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Queries;

/// <summary>
/// 取得使用者可存取實驗室清單查詢處理器
/// </summary>
public class GetAccessibleLaboratoriesQueryHandler : IRequestHandler<GetAccessibleLaboratoriesQuery, Result<List<AccessibleLabDto>>>
{
    private readonly IUserLabRepository _userLabRepository;
    private readonly ORGDbContext _orgDbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<GetAccessibleLaboratoriesQueryHandler> _logger;

    public GetAccessibleLaboratoriesQueryHandler(
        IUserLabRepository userLabRepository,
        ORGDbContext orgDbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<GetAccessibleLaboratoriesQueryHandler> logger)
    {
        _userLabRepository = userLabRepository ?? throw new ArgumentNullException(nameof(userLabRepository));
        _orgDbContext = orgDbContext ?? throw new ArgumentNullException(nameof(orgDbContext));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<List<AccessibleLabDto>>> Handle(
        GetAccessibleLaboratoriesQuery request,
        CancellationToken cancellationToken)
    {
        var validationResult = request.Validate();
        if (validationResult.IsFailure)
        {
            return Result.Failure<List<AccessibleLabDto>>(validationResult.Error);
        }

        if (!Guid.TryParse(request.UserId, out var userIdGuid))
        {
            return Result.Failure<List<AccessibleLabDto>>(
                Error.Validation("INVALID_USER_ID", "使用者識別碼格式不正確。"));
        }

        try
        {
            // 1. 檢查使用者身分與系統管理員特權 (SuperAdmin / sysadmin)
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user != null && (await _userManager.IsInRoleAsync(user, "SuperAdmin") || await _userManager.IsInRoleAsync(user, "sysadmin")))
            {
                _logger.LogInformation("使用者 {UserId} ({Email}) 為系統管理員，啟動全域實驗室特權載入機制。", request.UserId, user.Email);

                // 【核心修正】先執行 ToListAsync 將實體拉回記憶體，避開 EF Core 無法轉譯 (lab, index) 的限制
                var rawLabs = await _orgDbContext.Organizations
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.TenantLabId.HasValue)
                    .ToListAsync(cancellationToken);

                // 於記憶體（Client-side）進行帶 index 的 DTO 轉換
                var allLabs = rawLabs.Select((lab, index) => new AccessibleLabDto
                {
                    LabId = lab.TenantLabId!.Value,
                    //LabCode = lab.Code ?? string.Empty,
                    LabName = lab.Name ?? "系統實驗室",
                    Path = lab.NodePath,
                    IsPrimary = index == 0
                }).ToList();

                // 萬一 DB 尚未建置任何 Organization，回傳全域降級節點
                if (!allLabs.Any())
                {
                    allLabs.Add(new AccessibleLabDto
                    {
                        LabId = Guid.Empty,
                        //LabCode = "GLOBAL",
                        LabName = "全域管理員預設實驗室",
                        IsPrimary = true
                    });
                }

                return Result.Success(allLabs);
            }

            // 2. 一般使用者流程
            var mappings = await _userLabRepository.GetAccessibleLabsAsync(userIdGuid, cancellationToken);
            if (mappings == null || !mappings.Any())
            {
                _logger.LogWarning("使用者 {UserId} 未綁定任何實驗室權限。", request.UserId);
                return Result.Success(new List<AccessibleLabDto>());
            }

            var labGuids = mappings
                .Where(m => m.TenantLabId != Guid.Empty)
                .Select(m => m.TenantLabId)
                .Distinct()
                .ToList();

            var labs = await _orgDbContext.Organizations
                .AsNoTracking()
                .Where(x => x.TenantLabId.HasValue
                         && labGuids.Contains(x.TenantLabId.Value)
                         && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            var mappingDict = mappings
                .GroupBy(m => m.TenantLabId)
                .ToDictionary(g => g.Key, g => g.First());

            var result = labs.Select(lab => new AccessibleLabDto
            {
                LabId = lab.TenantLabId!.Value,
                //LabCode = lab.Code ?? string.Empty,
                LabName = lab.Name ?? "未知實驗室",
                Path = lab.NodePath,
                IsPrimary = mappingDict.TryGetValue(lab.TenantLabId.Value, out var m) && m.IsPrimary
            }).ToList();

            // Fallback 降級保護
            if (!result.Any() && mappings.Any())
            {
                result = mappings.Select(m => new AccessibleLabDto
                {
                    LabId = m.TenantLabId,
                    //LabName = m.LabName ?? "預設實驗室",
                    IsPrimary = m.IsPrimary
                }).ToList();
            }

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢使用者實驗室發生異常。UserId: {UserId}", request.UserId);
            return Result.Failure<List<AccessibleLabDto>>(
                Error.Failure("ORG_LABS_FETCH_ERROR", "無法存取實驗室清單。"));
        }
    }
}