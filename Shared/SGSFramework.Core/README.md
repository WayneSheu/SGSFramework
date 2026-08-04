SGSFramework.Core
SGSFramework.Core 是框架的領域核心層 (Domain Layer)。本組件定義了整個系統的領域模型、業務規則與契約介面，不依賴任何外部框架（如 EF Core 或 ASP.NET Core），確保業務邏輯的純粹性與高度可測試性。

🛠️ 技術堆疊
開發框架：.NET 10.0 (C# 14 strict null checks)

設計準則：Domain-Driven Design (DDD), Clean Architecture

相依性：無 (Pure Domain Layer)

🚀 組件架構與規範
本組件位於系統核心，嚴格遵守「內層不依賴外層」原則。

1. 領域實體 (Domain Entities)
所有領域實體必須繼承 BaseEntity，並實作必要的領域介面。

聚合根 (Aggregate Root)：負責維護聚合內的業務一致性。

值物件 (Value Objects)：封裝不可變的業務屬性（如 Money, EmailAddress）。

2. 領域契約介面 (Interfaces)
所有基礎設施（如 SGSFramework.Persistent）必須實作定義於此層的介面：

IRepository<T>：領域儲存庫介面。

IUnitOfWork：交易工作單元介面。

IDomainService：跨實體的領域邏輯服務定義。

3. 領域異常 (Domain Exceptions)
所有業務邏輯違反時，應拋出繼承自 DomainException 的例外，以確保錯誤訊息可被應用層 (Application Layer) 正確處理。

🏗️ 實作指引
定義領域模型範例
C#
public class User : BaseEntity, IAuditableEntity
{
    public string UserName { get; private set; } = string.Empty;
    public Email Email { get; private set; } // Value Object

    // 業務行為方法 (非僅 Getter/Setter)
    public void UpdateProfile(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) 
            throw new DomainException("Username cannot be empty.");
            
        UserName = newName;
    }
}
⚠️ 核心紀律
零外部依賴：SGSFramework.Core 不得引用任何 UI、Infrastructure 或 Persistence 相關的套件（如 Microsoft.EntityFrameworkCore）。

領域封裝：實體的屬性 setter 應設為 private 或 internal，強制開發者透過公開的業務方法 (Business Methods) 修改狀態。

強類型安全：全面啟用 C# 14 Nullable Reference Types，確保領域模型的狀態在任何時候都是可預測的。

👥 團隊維護與支援
主要維護者：Wayne

設計規範參考：企業級 Clean Architecture 標準作業指引

💡 提醒：若您需要擴充全域領域邏輯或定義新的領域事件 (Domain Events)，請先於 Git 進行議題討論 (Issue Tracking)，確保介面的一致性。