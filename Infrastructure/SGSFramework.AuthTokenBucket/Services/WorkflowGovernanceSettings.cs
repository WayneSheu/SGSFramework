using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.SystemSettings;
using SGSFramework.Core.Abstractions.Governance;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>
/// 專案 / 合約審批治理設定讀取：優先採用 core.SystemSettings（系統配置頁），
/// 未設定時回到組態 PmsGovernance:ContractSeparationRequired / PmsGovernance:CrDualSignoffRequired（預設 true）。
/// 單筆主鍵查詢、不快取，儲存後立即生效（含多節點部署）。
/// </summary>
public sealed class WorkflowGovernanceSettings(
    DbContext dbContext,
    IConfiguration configuration,
    ILogger<WorkflowGovernanceSettings> logger) : IWorkflowGovernanceSettings
{
    public bool DefaultContractSeparationRequired => configuration.GetValue("PmsGovernance:ContractSeparationRequired", true);

    public bool DefaultCrDualSignoffRequired => configuration.GetValue("PmsGovernance:CrDualSignoffRequired", true);

    public async Task<bool> IsContractSeparationRequiredAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken).ConfigureAwait(false))?.ContractSeparationRequired ?? DefaultContractSeparationRequired;

    public async Task<bool> IsCrDualSignoffRequiredAsync(CancellationToken cancellationToken = default) =>
        (await LoadAsync(cancellationToken).ConfigureAwait(false))?.CrDualSignoffRequired ?? DefaultCrDualSignoffRequired;

    private async Task<WorkflowGovernanceSettingValue?> LoadAsync(CancellationToken cancellationToken)
    {
        var json = await dbContext.Set<SystemSetting>().AsNoTracking()
            .Where(s => s.Key == WorkflowGovernanceSettingKeys.Key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<WorkflowGovernanceSettingValue>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "專案 / 合約審批設定格式異常，改用預設值。");
            return null;
        }
    }
}
