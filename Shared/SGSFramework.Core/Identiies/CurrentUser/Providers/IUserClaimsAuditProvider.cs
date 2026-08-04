namespace SGSFramework.Core.Identiies.CurrentUser.Providers
{
    public interface IUserClaimsAuditProvider
    {
        AuditIdentity GetCurrentIdentity();
    }

    public record AuditIdentity(
     string UserId,
     string TraceId,
     string TenantId,
     string? DepartmentId = null, // 部門資訊
     string? RemoteIp = null,      // 客戶端 IP
     string? MachineName = null,   // 執行伺服器名稱
     string? UserAgent = null      // 瀏覽器/載具資訊 (選配)
 );
}
