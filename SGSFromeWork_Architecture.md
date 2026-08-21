# SES (Smart Empowerment Solution) 插件架構說明

本文件記錄了 SES 系統基於 **AssemblyLoadContext** 的動態外掛（Plugin）載入與熱插拔生命週期設計。

---

## 1. 系統架構概觀

系統採用 Plugin-based 模組化設計，主要由以下元件組成：
* **Host System**: 負責整體運作與共用 DI 容器的生命週期管理。
* **PluginLoader**: 封裝 `AssemblyLoadContext`，負責動態載入、解除載入與記憶體回收監控。
* **IPlugin Interface**: 定義外掛與宿主的標準通訊合約。

---

## 2. 核心介面定義 (IPlugin.cs)

```csharp
namespace SES.Domain.Interfaces;

public interface IPlugin
{
    Guid Id { get; }
    string Name { get; }
    Task InitializeAsync(IServiceProvider serviceProvider);
    Task ExecuteAsync();
    Task ShutdownAsync();
}

## 3. 記憶體生命週期與防護機制
為防止動態載入 DLL 造成記憶體洩漏（Memory Leak），PluginLoader 實作了基於 WeakReference 的回收確認機制：

呼叫 AssemblyLoadContext.Unload() 標記解除載入。

透過迴圈主動觸發 GC.Collect()。

檢查 WeakReference.IsAlive 狀態，確保外掛資源已被徹底釋放。

4. 相關架構圖
詳細的生命週期與資料流向圖請參閱同目錄下的 Excalidraw 檔案：
![Plugin Lifecycle](./plugin_lifecycle.excalidraw.svg)

5. PluginLoader 記憶體生命週期詳解
為確保 AssemblyLoadContext (ALC) 能夠正確回收，必須遵循嚴格的引用規則。以下是技術細節：

強引用隔離 (Strong Reference Isolation)：

宿主 (Host) 絕不能持有 Plugin 中任何類型的強引用。

所有 Plugin 內部的服務實作必須透過 IPlugin.InitializeAsync(IServiceProvider) 傳入的容器進行解析，而非直接 New 出來。

卸載驗證機制 (Unload Verification)：

當 ALC.Unload() 被呼叫後，GC 回收並非同步完成。

實作中透過 WeakReference 持有 ALC 的參照。若經多次 GC.Collect() 後 IsAlive 仍為 true，代表存在記憶體洩漏，系統會觸發 LogWarning 並記錄洩漏指標，便於診斷無法釋放的物件（如未釋放的 Event Handler 或靜態成員）。

6. DI 容器整合與插件生命週期管理
在 SES 系統中，DI 容器的設計需支援「動態註冊」與「隔離解除註冊」：

Plugin-Specific Service Scope：

每個 Plugin 載入時，應建立一個獨立的 IServiceScope，確保該 Plugin 獨有的服務不會與 Host 或其他 Plugin 產生衝突。

註冊流程：

Plugin 層：透過 IPlugin 的 InitializeAsync 傳入 Host 的 DI 容器。

Host 層：在 AssemblyLoadContext 卸載時，自動清理該 Scope 下所有已註冊的 Singleton 與 Scoped 服務。

架構建議：

C#
// 範例：Plugin 內部註冊服務的規範
public async Task InitializeAsync(IServiceProvider serviceProvider)
{
    // 插件應僅依賴介面 (Interface)，不得依賴 Host 的實作細節
    var myService = serviceProvider.GetRequiredService<IMyPluginService>();
    await myService.RegisterCapabilitiesAsync();
}

7. Bitmask 權限與插件整合規範
針對權限點擴展至 64 位元以上的需求，系統採取 BitArray 或 BigInteger 實作的 PermissionSet 結構，並與插件生命週期深度整合。

7.1 權限模型設計 (Permission Model)
動態註冊：每個插件在載入時，必須透過 IPlugin.InitializeAsync 回報其所需的權限位元遮罩（Bitmask）。

權限容器：Host 系統維護一個 GlobalPermissionRegistry，將插件 ID 與對應的 BitArray 權限集合進行映射。

大於 64-bit 的處理：由於權限點位數量極大，系統禁止直接使用位元運算子（如 &, |）於 long，必須使用以下封裝類別：

C#
// 企業級權限標識結構範例
public record PermissionBit(int BitIndex);

public class PermissionSet
{
    private readonly System.Collections.BitArray _mask = new(256); // 支援 256+ 位元

    public void Grant(PermissionBit bit) => _mask.Set(bit.BitIndex, true);
    public bool HasPermission(PermissionBit bit) => _mask.Get(bit.BitIndex);
}
7.2 插件的權限邊界 (Plugin Permission Boundary)
存取控制 (Access Control)：當 Host 系統呼叫插件功能時，會先檢查該插件的 PermissionSet 是否包含對應的 PermissionBit。

運行時驗證：若插件試圖執行超過其註冊範圍的業務邏輯，Host 的 Interceptor (攔截器) 將拋出 UnauthorizedAccessException，確保單一插件無法越權操作其他核心功能。

7.3 架構視覺化重點 (Excalidraw 建議)
在您的 plugin_lifecycle.excalidraw 中，請加入一個「權限審核層 (Permission Gateway)」：

Request Layer: 插件發起請求。

Validation Layer: Host 查詢該插件的 PermissionSet (BitArray)。

Execution Layer: 只有通過驗證才授權執行。