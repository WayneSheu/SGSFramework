using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Mask
{
    /// <summary>
    /// 定義全域日誌安全脫敏與解構配置的策略合約
    /// </summary>
    public interface ISerilogMaskingStrategy
    {
        /// <summary>
        /// 執行 LoggerConfiguration 的安全遮罩與解構政策注入
        /// </summary>
        LoggerConfiguration Configure(LoggerConfiguration loggerConfiguration, IServiceProvider serviceProvider);
    }
}
