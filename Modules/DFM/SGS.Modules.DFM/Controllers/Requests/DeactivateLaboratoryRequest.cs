using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Controllers.Requests
{
    /// <summary>
    /// 停用實驗室請求模型
    /// </summary>
    public record DeactivateLaboratoryRequest(string Reason);
}
