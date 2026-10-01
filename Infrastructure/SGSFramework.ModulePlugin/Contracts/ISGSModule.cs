using Microsoft.Extensions.DependencyInjection;

namespace SGSFramework.ModulePlugin.Contracts;

/// <summary>
/// 全端 DI 靜態服務註冊與身份合約介面
/// </summary>
public interface ISGSModule
{
    string Id { get; }
    string Name { get; }
    string Version { get; }

    /// <summary>
    /// 後端 API 或前端 WASM DI 服務註冊進入點
    /// </summary>
    void ConfigureServices(IServiceCollection services);
}