using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Identiies
{
    /// <summary>
    /// 當前請求之用戶安全性、組織環境與網路來源上下文快照
    /// </summary>
    /// <param name="UserId">用戶唯一識別碼</param>
    /// <param name="Username">用戶帳號名稱</param>
    /// <param name="DeviceId">客戶端裝置識別碼</param>
    /// <param name="LaboratoryId">所屬實驗室/組織識別碼 (PhysLIMS 2.0 隔離邊界)</param>
    /// <param name="ClientIp">真實客戶端來源 IP 位址 (已解析 Proxy 代理)</param>
    public sealed record UserContextSnapshot(
        string UserId,
        string Username,
        string DeviceId,
        string LaboratoryId,
        string ClientIp
    );
}
