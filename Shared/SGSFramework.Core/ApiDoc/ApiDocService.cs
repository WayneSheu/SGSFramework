using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.ApiDoc
{
    public class ApiDocService : IApiDocService
    {
        public async Task<string> GetOpenApiSpecAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // 模擬非同步讀取 OpenAPI JSON/YAML 規格檔案
                await Task.Delay(10, cancellationToken);
                return """
            {
              "openapi": "3.0.0",
              "info": {
                "title": "對比示範 API",
                "version": "1.0.0"
              },
              "paths": {}
            }
            """;
            }
            catch (OperationCanceledException)
            {
                // 處理取消請求
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("無法正確檢索 OpenAPI 規格說明。", ex);
            }
        }
    }
}
