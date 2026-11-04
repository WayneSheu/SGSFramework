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
    }
}
