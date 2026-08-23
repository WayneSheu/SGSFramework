using SGSFramework.Core.Abstractions.Entities.Modules;

namespace SGSFramework.ModulePlugin.Abstractions
{
    /// <summary>
    /// 模組元資料倉儲介面
    /// </summary>
    public interface IModuleRepository
    {
        /// <summary>
        /// 取得所有模組紀錄
        /// </summary>
        Task<IEnumerable<ModuleMetadata>> GetAllModulesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 依模組名稱取得指定模組紀錄
        /// </summary>
        Task<ModuleMetadata?> GetModuleByNameAsync(string moduleName, CancellationToken cancellationToken = default);

        /// <summary>
        /// 新增或更新模組紀錄
        /// </summary>
        Task UpsertAsync(ModuleMetadata moduleInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// 切換模組啟用/停用狀態
        /// </summary>
        Task ToggleModuleStatusAsync(string moduleName, bool isActive, CancellationToken cancellationToken = default);

        /// <summary>
        /// 移除指定模組紀錄
        /// </summary>
        Task RemoveModuleAsync(string moduleName, CancellationToken cancellationToken = default);
    }

}