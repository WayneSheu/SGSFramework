using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.ApiDoc
{
    /// <summary>
    /// 定義與 API 文件規格相關的領域服務介面
    /// </summary>
    public interface IApiDocService
    {
        Task<string> GetOpenApiSpecAsync(CancellationToken cancellationToken = default);
    }
}
