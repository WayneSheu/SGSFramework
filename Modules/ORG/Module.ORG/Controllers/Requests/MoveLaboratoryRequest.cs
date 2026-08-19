using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Controllers.Requests
{
    /// <summary>
    /// 搬移樹狀節點 API Request Body 簽署
    /// </summary>
    /// <param name="NewParentId">新的父節點識別碼（若搬移至頂層根節點請設為 null）</param>
    public sealed record MoveLaboratoryRequest(int? NewParentId);
}
