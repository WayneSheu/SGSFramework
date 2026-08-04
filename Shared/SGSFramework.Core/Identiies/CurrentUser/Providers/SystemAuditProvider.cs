using SGSFramework.Core.HttpAuditProviders;

namespace SGSFramework.Core.Identiies.CurrentUser.Providers
{
    /// <summary>
    /// 當程式碼執行在背景（沒有 HttpContext）時，由系統指派。
    /// </summary>
    /// <summary>
    /// 當程式碼執行在背景（沒有 HttpContext）時，由系統指派。
    /// </summary>
    public class SystemAuditProvider : IAuditProvider
    {
        public string UserId => "Background_Service";
        public string? UserName => "Background Service";
        public string? TraceId => Guid.NewGuid().ToString();
        public string? RemoteIp => null;
        public string? DeviceId => "System_Internal_Channel";
        public string? LaboratoryId => "System_Global_Node";
    }
}
