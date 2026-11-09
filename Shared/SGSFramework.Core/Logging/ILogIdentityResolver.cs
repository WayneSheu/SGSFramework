#nullable enable
namespace SGSFramework.Core.Logging
{
    /// <summary>
    /// 日誌寫入時的名稱解析：獨立日誌庫無法直接查主庫的 AspNetUsers / org.Organization，
    /// 改由 Host 提供此服務把 UserId / TenantId 換成顯示名稱。解析失敗必須回傳空集合而非擲例外。
    /// </summary>
    public interface ILogIdentityResolver
    {
        Task<(IReadOnlyDictionary<string, string> Users, IReadOnlyDictionary<string, string> Tenants)> ResolveAsync(
            IReadOnlyCollection<Guid> userIds,
            IReadOnlyCollection<string> tenantIds,
            CancellationToken cancellationToken);

        /// <summary>
        /// 依人員姓名 / 帳號關鍵字（包含比對 FullName 或 UserName）找出使用者的登入帳號（UserName）。
        /// 資料日誌的操作者欄位記錄的是登入帳號（Identity.Name），以姓名查詢時需先換成帳號再比對。
        /// 無符合者回傳空集合；最多回傳 <paramref name="maxCount"/> 筆。
        /// </summary>
        Task<IReadOnlyList<string>> FindLoginNamesAsync(string keyword, int maxCount, CancellationToken cancellationToken);

        /// <summary>登入帳號（UserName）轉人員姓名（FullName，無姓名時退回帳號）；查無者不在結果內。</summary>
        Task<IReadOnlyDictionary<string, string>> ResolveDisplayNamesAsync(
            IReadOnlyCollection<string> loginNames, CancellationToken cancellationToken);
    }
}
