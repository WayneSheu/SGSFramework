using SGSFramework.Core.Abstractions.Entities.Modules;

namespace SGSFramework.ModulePlugin.Abstractions
{
    public interface IModuleRepository
    {
        Task<ModuleMetadata?> GetModuleByNameAsync(string moduleName, CancellationToken cancellationToken = default);
        Task<IEnumerable<ModuleMetadata>> GetAllModulesAsync(CancellationToken cancellationToken = default);
        Task UpsertAsync(ModuleMetadata module, CancellationToken cancellationToken = default);
        Task SetModuleStatusAsync(string moduleName, bool isActive, CancellationToken cancellationToken = default);
        Task ToggleModuleStatusAsync(string moduleName, bool isActive, CancellationToken cancellationToken = default);
        Task RemoveModuleAsync(string moduleName, CancellationToken cancellationToken = default);
        Task DeleteControllersByModuleNameAsync(string moduleName, CancellationToken cancellationToken = default);
        Task DeleteModuleMetadataAsync(ModuleMetadata module, CancellationToken cancellationToken = default);
    }
}