namespace SGSFramework.Core.Abstractions.Attributes
{
    /// <summary>
    /// 標註需「落地加密」的字串欄位 (ISO 27001 A.8.24 / 個資法)。
    /// 由 <c>ModelBuilder.ApplyEncryptedProperties()</c> 自動掛上 EF Core ValueConverter，
    /// 寫入資料庫前以 ASP.NET Core Data Protection 加密、讀取時解密，對業務程式碼透明。
    /// </summary>
    /// <remarks>
    /// 限制：密文為非決定性 (同明文每次結果不同)，因此標註欄位無法於資料庫端進行
    /// 等值查詢、LIKE 搜尋、排序或唯一索引；需搜尋的欄位 (例如姓名、員工編號) 請勿標註。
    /// 日誌輸出時一律視為機密並以預設遮罩處理。
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class EncryptedAttribute : Attribute
    {
    }
}
