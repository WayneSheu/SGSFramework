using System.Reflection;
using System.Runtime.Loader;

namespace SGSFramework.ModulePlugin.Systems.Module.Contexts;

/// <summary>
/// 隔離式組件載入上下文 (Collectible AssemblyLoadContext)
/// 優化項目：
/// 1. 共享組件保護機制：避免 MediatR / DI / Core 契約型別雙重載入引發的 Type Mismatch
/// 2. 無鎖記憶體串流載入：讀取位元組串流，徹底解除實體 DLL 檔案鎖定 (Access Denied)
/// 3. 策略注入：支援動態擴充共享組件白名單
/// </summary>
public sealed class ModuleAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly HashSet<string> _sharedAssemblyNames;
    private readonly Func<AssemblyName, bool>? _sharedCustomPredicate;

    /// <summary>
    /// 初始化隔離式組件載入上下文
    /// </summary>
    /// <param name="pluginPath">外掛主 DLL 實體路徑</param>
    /// <param name="sharedAssemblyNames">強制委派給 Default Context 的共享組件名稱清單</param>
    /// <param name="sharedCustomPredicate">自訂共享組件斷言策略</param>
    public ModuleAssemblyLoadContext(
        string pluginPath,
        IEnumerable<string>? sharedAssemblyNames = null,
        Func<AssemblyName, bool>? sharedCustomPredicate = null)
        : base(isCollectible: true)
    {
        if (string.IsNullOrWhiteSpace(pluginPath))
        {
            throw new ArgumentNullException(nameof(pluginPath), "外掛實體路徑不可為空。");
        }

        _resolver = new AssemblyDependencyResolver(pluginPath);
        _sharedAssemblyNames = new HashSet<string>(sharedAssemblyNames ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        _sharedCustomPredicate = sharedCustomPredicate;
    }

    /// <summary>
    /// 解析並載入目標組件
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        ArgumentNullException.ThrowIfNull(assemblyName);

        // 1. 檢查是否為系統或框架層共享組件，若是則回傳 null 委派給 AssemblyLoadContext.Default 載入
        if (IsSharedAssembly(assemblyName))
        {
            return null;
        }

        // 2. 解析外掛目錄下的組件路徑
        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
        {
            return null;
        }

        // 3. 採用無鎖記憶體串流載入組件
        return LoadAssemblyFromMemory(assemblyPath);
    }

    /// <summary>
    /// 解析並載入 Unmanaged C/C++ 原生動態連結庫
    /// </summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        if (string.IsNullOrWhiteSpace(unmanagedDllName))
        {
            return IntPtr.Zero;
        }

        try
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (!string.IsNullOrEmpty(libraryPath) && File.Exists(libraryPath))
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }
        }
        catch
        {
            // 原生 DLL 解析失敗，降級交由系統預設搜尋管道處理
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 判斷目標組件是否應由主應用程式 (Default Context) 共享載入
    /// </summary>
    private bool IsSharedAssembly(AssemblyName assemblyName)
    {
        string name = assemblyName.Name ?? string.Empty;

        // 1. 全域框架、核心契約庫與模組 Shared/Application 層預設一律共享 (由 Default Context 載入)
        if (name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("MediatR", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SGS.Core", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SGSFramework.", StringComparison.OrdinalIgnoreCase) || // <--- 納入 SGSFramework 核心與持久化共用組件
            name.EndsWith(".Application", StringComparison.OrdinalIgnoreCase) ||    // <---保護 Application 層
            name.EndsWith(".Abstractions", StringComparison.OrdinalIgnoreCase) ||   // <---保護 抽象介面層
            name.EndsWith(".Contracts", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. 檢查自訂名單
        if (_sharedAssemblyNames.Contains(name))
        {
            return true;
        }

        // 3. 執行外部策略斷言
        return _sharedCustomPredicate?.Invoke(assemblyName) ?? false;
    }

    /// <summary>
    /// 將檔案讀入記憶體位元組陣列並轉為 Stream 載入，避免檔案上鎖
    /// </summary>
    private Assembly LoadAssemblyFromMemory(string assemblyPath)
    {
        try
        {
            byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);

            string pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            byte[]? pdbBytes = File.Exists(pdbPath) ? File.ReadAllBytes(pdbPath) : null;

            using var asmStream = new MemoryStream(assemblyBytes);
            using var pdbStream = pdbBytes != null ? new MemoryStream(pdbBytes) : null;

            return LoadFromStream(asmStream, pdbStream);
        }
        catch (Exception ex)
        {
            // 記憶體載入失敗時，降級嘗試傳統實體路徑載入
            try
            {
                return LoadFromAssemblyPath(assemblyPath);
            }
            catch (Exception innerEx)
            {
                throw new InvalidOperationException($"無法透過記憶體或實體路徑載入模組組件：{assemblyPath}", new AggregateException(ex, innerEx));
            }
        }
    }
}