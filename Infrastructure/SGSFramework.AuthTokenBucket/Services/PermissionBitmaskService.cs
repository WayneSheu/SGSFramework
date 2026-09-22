// ==========================================
// 檔案路徑: Application/SGSFramework.AuthTokenBucket/Services/PermissionBitmaskService.cs
// 架構層級: Application Layer (Service Implementation)
// 設計模式: Feature-level Bitmask Calculation & Decoding
// ==========================================

#nullable enable

namespace SGSFramework.AuthTokenBucket.Services;

using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs.PermissionTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class PermissionBitmaskService : IPermissionBitmaskService
{
    private readonly IPermissionManagementService _permissionService;
    private readonly ILogger<PermissionBitmaskService> _logger;

    public PermissionBitmaskService(
        IPermissionManagementService permissionService,
        ILogger<PermissionBitmaskService> logger)
    {
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, long>> CalculateModuleBitmasksAsync(
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var targetPermissions = permissions as HashSet<string> ?? new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);

        if (targetPermissions.Count == 0)
        {
            return result;
        }

        try
        {
            var permissionTree = await _permissionService.GetPermissionTreeAsync(cancellationToken).ConfigureAwait(false);
            if (permissionTree == null || permissionTree.Count == 0)
            {
                _logger.LogWarning("[PermissionBitmaskService] 未能取得系統權限樹狀 Metadata，無法計算位元遮罩。");
                return result;
            }

            foreach (var module in permissionTree)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (module.Functions == null || module.Functions.Count == 0) continue;

                foreach (var fn in module.Functions)
                {
                    if (fn.ReadPermission != null &&
                        !string.IsNullOrEmpty(fn.ReadPermission.PermissionKey) &&
                        targetPermissions.Contains(fn.ReadPermission.PermissionKey))
                    {
                        ApplyBitmask(result, fn.ReadPermission.PermissionKey, fn.ReadPermission.BitPosition);
                    }

                    if (fn.ActionPermissions != null && fn.ActionPermissions.Count > 0)
                    {
                        foreach (var action in fn.ActionPermissions)
                        {
                            if (!string.IsNullOrEmpty(action.PermissionKey) && targetPermissions.Contains(action.PermissionKey))
                            {
                                ApplyBitmask(result, action.PermissionKey, action.BitPosition);
                            }
                        }
                    }
                }
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PermissionBitmaskService] Bitmask 計算作業已取消。");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionBitmaskService] 計算權限 Bitmask 時發生系統異常。");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> DecodeBitmaskToPermissionsAsync(
        Dictionary<string, long> bitmaskMap,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bitmaskMap);

        var result = new List<string>();
        if (bitmaskMap.Count == 0)
        {
            return result;
        }

        try
        {
            var permissionTree = await _permissionService.GetPermissionTreeAsync(cancellationToken).ConfigureAwait(false);
            if (permissionTree == null || permissionTree.Count == 0)
            {
                _logger.LogWarning("[PermissionBitmaskService] 未能取得系統權限樹狀 Metadata，無法解碼位元遮罩。");
                return result;
            }

            foreach (var module in permissionTree)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (module.Functions == null || module.Functions.Count == 0) continue;

                foreach (var fn in module.Functions)
                {
                    // 1. 檢查 ReadPermission
                    if (fn.ReadPermission != null && !string.IsNullOrEmpty(fn.ReadPermission.PermissionKey))
                    {
                        CheckAndAddPermission(fn.ReadPermission.PermissionKey, fn.ReadPermission.BitPosition, bitmaskMap, result);
                    }

                    // 2. 檢查 ActionPermissions
                    if (fn.ActionPermissions != null && fn.ActionPermissions.Count > 0)
                    {
                        foreach (var action in fn.ActionPermissions)
                        {
                            if (!string.IsNullOrEmpty(action.PermissionKey))
                            {
                                CheckAndAddPermission(action.PermissionKey, action.BitPosition, bitmaskMap, result);
                            }
                        }
                    }
                }
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PermissionBitmaskService] Bitmask 解碼作業已取消。");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionBitmaskService] 解碼權限 Bitmask 時發生系統異常。");
            throw;
        }
    }

    private static void ApplyBitmask(Dictionary<string, long> result, string rawPermissionKey, long bitPosition)
    {
        if (string.IsNullOrWhiteSpace(rawPermissionKey)) return;

        string groupKey = ExtractFeaturePrefix(rawPermissionKey);
        int normalizedBit = (int)(bitPosition % 64);
        long currentMask = (1L << normalizedBit);

        if (result.TryGetValue(groupKey, out long existingMask))
        {
            result[groupKey] = existingMask | currentMask;
        }
        else
        {
            result[groupKey] = currentMask;
        }
    }

    private static void CheckAndAddPermission(
        string permissionKey,
        long bitPosition,
        Dictionary<string, long> bitmaskMap,
        List<string> resultList)
    {
        string groupKey = ExtractFeaturePrefix(permissionKey);
        if (bitmaskMap.TryGetValue(groupKey, out long storedBitmask))
        {
            int normalizedBit = (int)(bitPosition % 64);
            long targetMask = (1L << normalizedBit);

            if ((storedBitmask & targetMask) == targetMask)
            {
                resultList.Add(permissionKey);
            }
        }
    }

    private static string ExtractFeaturePrefix(string permissionKey)
    {
        string[] parts = permissionKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{parts[0]}.{parts[1]}";
        }
        return permissionKey;
    }
}