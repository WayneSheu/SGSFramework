using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.Core.Abstractions.Governance
{
    /// <summary>
    /// 專案管理（PMS）與合約管理的審批治理動態設定：儲存於 core.SystemSettings（分類鍵 <see cref="WorkflowGovernanceSettingKeys.Key"/>），
    /// 由「系統配置」頁維護；未自訂時採用 appsettings 的 PmsGovernance 區段（預設皆為啟用）。
    /// </summary>
    public interface IWorkflowGovernanceSettings
    {
        /// <summary>
        /// 合約核決是否落實職責分離：啟用時，送審者不得核決自己送審的合約，且同一人不得決定同一送審週期的兩個步驟。
        /// 停用時仍須依序完成 PM 初審 → 法務 → 高階主管核決，但允許同一人處理。
        /// </summary>
        Task<bool> IsContractSeparationRequiredAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// CR 變更單是否須 PM 與 Tech Lead 雙簽：停用時內部簽核只需 PM 或 Tech Lead 任一人簽核；
        /// 客戶 CR 仍須客戶簽核（在內部簽核之後）。
        /// </summary>
        Task<bool> IsCrDualSignoffRequiredAsync(CancellationToken cancellationToken = default);

        /// <summary>appsettings 的預設值：合約職責分離</summary>
        bool DefaultContractSeparationRequired { get; }

        /// <summary>appsettings 的預設值：CR 雙簽</summary>
        bool DefaultCrDualSignoffRequired { get; }
    }

    /// <summary>core.SystemSettings 中專案 / 合約審批設定的分類鍵</summary>
    public static class WorkflowGovernanceSettingKeys
    {
        public const string Key = "PmsGovernance";
    }

    /// <summary>core.SystemSettings 中 PmsGovernance 設定的 JSON 內容</summary>
    public sealed record WorkflowGovernanceSettingValue(bool ContractSeparationRequired = true, bool CrDualSignoffRequired = true);
}
