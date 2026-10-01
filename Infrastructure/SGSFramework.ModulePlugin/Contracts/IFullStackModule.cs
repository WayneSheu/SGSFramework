// Contracts/IFullStackModule.cs
using SGSFramework.ModulePlugin.Abstractions;

namespace SGSFramework.ModulePlugin.Contracts;

/// <summary>
/// 全端模組介面，繼承 ISGSModule 與 IModule，實現從「靜態服務註冊」到「動態運行時初始化與健康監測」的完整生命週期。
/// </summary>
public interface IFullStackModule : ISGSModule, IModule
{
    // 繼承 ISGSModule 的 Id, Name, Version, ConfigureServices
    // 繼承 IModule 的 InitializeAsync, GetHealthStatus
}