namespace SGSFramework.Core.Identiies.CurrentUser
{
    public interface ICurrentUserService
    {
        // 由 Web 專案實作，從 JWT 或 Session 取得 UserID
        string? GetCurrentUserId();
    }
}
