using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Helpers
{
    /// <summary>
    /// 提供從 appsettings.json 或環境變數中取得資料庫連線字串的輔助方法
    /// </summary>
    public static class DbContextConfigHelper
    {
        public static string GetConnectionString(string sectionPath = "PersistentOptions:DatabaseSettings:ConnectionString")
        {
            var apiPath = Environment.GetEnvironmentVariable("SES_API_PATH")
                          ?? Path.Combine(Directory.GetCurrentDirectory(), "../../../../API/SES.API");

            var config = new ConfigurationBuilder()
                .SetBasePath(apiPath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            return config.GetValue<string>(sectionPath)
                   ?? throw new InvalidOperationException("無法取得連線字串");
        }
    }
}
