using Microsoft.AspNetCore.Authentication;

namespace PhysLIMS.API.Models
{
    /// <summary>
    /// 虛擬 Windows 驗證處理器的配置選項
    /// </summary>
    public sealed class FakeWindowsAuthOptions : AuthenticationSchemeOptions
    {
        public string DefaultDomain { get; set; } = "CORP";
        public string DefaultUserName { get; set; } = "wayne";
        public string DefaultRole { get; set; } = "Domain Users";
    }
}
