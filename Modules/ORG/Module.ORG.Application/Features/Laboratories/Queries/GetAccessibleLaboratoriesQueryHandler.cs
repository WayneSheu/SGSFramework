using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Contracts.Queries;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
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
public sealed class GetAccessibleLaboratoriesQueryHandler(
    ORGDbContext orgDbContext,
    UserManager<ApplicationUser> userManager,
    ILogger<GetAccessibleLaboratoriesQueryHandler> logger)
    : IRequestHandler<GetAccessibleLaboratoriesQuery, Result<List<AccessibleLabDto>>>
{
    private readonly ORGDbContext _orgDbContext = orgDbContext ?? throw new ArgumentNullException(nameof(orgDbContext));
    private readonly UserManager<ApplicationUser> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    private readonly ILogger<GetAccessibleLaboratoriesQueryHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<List<AccessibleLabDto>>> Handle(
        GetAccessibleLaboratoriesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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
            // 1. 系統管理員特權流程
            var user = await _userManager.FindByIdAsync(request.UserId).ConfigureAwait(false);
            if (user != null && (await _userManager.IsInRoleAsync(user, "SuperAdmin").ConfigureAwait(false) ||
                                 await _userManager.IsInRoleAsync(user, "sysadmin").ConfigureAwait(false)))
            {
                _logger.LogInformation("使用者 {UserId} ({Email}) 為系統管理員，啟動全域實驗室特權載入機制。", request.UserId, user.Email);

                var superAdminLabs = await LoadAllActiveLabsForAdminAsync(cancellationToken).ConfigureAwait(false);
                return Result.Success(superAdminLabs);
            }

            // 2. 一般使用者流程：完整補齊 LabId, ParentLabId, ParentTenantLabId 投影轉譯
            var userLabs = await _orgDbContext.UserAccessibleLabReadModels
                .AsNoTracking()
                .Where(x => x.UserId == userIdGuid)
                .Select(x => new AccessibleLabDto
                {
                    LabId = x.LabId,
                    TenantLabId = x.TenantLabId,
                    LabCode = x.LabCode,
                    LabName = x.LabName,
                    Path = x.Path,
                    HierarchyLevel = x.HierarchyLevel,
                    IsPrimary = x.IsPrimary,
                    ParentLabId = x.ParentLabId,
                    ParentTenantLabId = x.ParentTenantLabId,
                    ParentLabCode = x.ParentLabCode,
                    ParentLabName = x.ParentLabName
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!userLabs.Any())
            {
                _logger.LogWarning("使用者 {UserId} 未綁定任何實驗室權限或目標實驗室已停用。", request.UserId);
            }

            return Result.Success(userLabs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查詢使用者實驗室發生異常。UserId: {UserId}", request.UserId);
            return Result.Failure<List<AccessibleLabDto>>(
                Error.Failure("ORG_LABS_FETCH_ERROR", "無法存取實驗室清單。"));
        }
    }

    private async Task<List<AccessibleLabDto>> LoadAllActiveLabsForAdminAsync(CancellationToken cancellationToken)
    {
        var activeOrgs = await _orgDbContext.Organizations
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rootLookup = activeOrgs
            .Where(x => x.Level == 1)
            .ToDictionary(x => ExtractLevel1Segment(x.NodePath), StringComparer.OrdinalIgnoreCase);

        var labs = activeOrgs
            .Where(x => x.TenantLabId.HasValue)
            .Select((lab, index) =>
            {
                string rootSegment = ExtractLevel1Segment(lab.NodePath);
                bool hasParent = rootLookup.TryGetValue(rootSegment, out var parentNode);

                return new AccessibleLabDto
                {
                    LabId = lab.Id,
                    TenantLabId = lab.TenantLabId!.Value,
                    LabCode = lab.Code ?? string.Empty,
                    LabName = lab.Name ?? string.Empty,
                    Path = lab.NodePath ?? string.Empty,
                    HierarchyLevel = lab.Level,
                    IsPrimary = index == 0,
                    ParentLabId = lab.Level == 1 ? lab.Id : (hasParent ? parentNode!.Id : lab.Id),
                    ParentTenantLabId = lab.Level == 1 ? lab.TenantLabId : (hasParent ? parentNode!.TenantLabId : lab.TenantLabId),
                    ParentLabCode = lab.Level == 1 ? lab.Code ?? string.Empty : (hasParent ? parentNode!.Code ?? "ROOT" : "ROOT"),
                    ParentLabName = lab.Level == 1 ? lab.Name ?? string.Empty : (hasParent ? parentNode!.Name ?? "根組織" : "根組織")
                };
            })
            .ToList();

        if (!labs.Any())
        {
            labs.Add(new AccessibleLabDto
            {
                LabId = 0,
                TenantLabId = Guid.Empty,
                LabCode = "GLOBAL",
                LabName = "全域管理員預設實驗室",
                Path = "/",
                HierarchyLevel = 1,
                IsPrimary = true,
                ParentLabId = 0,
                ParentTenantLabId = Guid.Empty,
                ParentLabCode = "GLOBAL",
                ParentLabName = "全域管理員預設實驗室"
            });
        }

        return labs;
    }

    private static string ExtractLevel1Segment(string? nodePath)
    {
        if (string.IsNullOrWhiteSpace(nodePath)) return string.Empty;
        var parts = nodePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? $"/{parts[0]}/" : nodePath;
    }
}