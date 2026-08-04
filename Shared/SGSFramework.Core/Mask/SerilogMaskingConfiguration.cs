
#nullable enable
using Serilog;
using Serilog.Enrichers.Sensitive;

namespace SGSFramework.Core.Mask
{
    /// <summary>
    /// 提供靜態、執行緒安全的 Serilog 全域脫敏與強型別物件解構管線配置
    /// </summary>
    public static class SerilogMaskingConfiguration
    {
        /// <summary>
        /// 鏈式配置全域日誌脫敏與物件解構政策，避免與 DI 容器產生 Scoped 生命週期衝突
        /// </summary>
        /// <param name="loggerConfiguration">Serilog 核心組態實例</param>
        /// <returns>配置完成的 LoggerConfiguration</returns>
        public static LoggerConfiguration ConfigurationSerilogMasking(this LoggerConfiguration loggerConfiguration)
        {
            ArgumentNullException.ThrowIfNull(loggerConfiguration);

            try
            {
                // 1. 直接實例化脫敏核心元件，維持其在日誌管線中的單例與高效能特性
                var maskService = new MaskingService();
                var maskingOperator = new UniversalMaskingOperator(maskService);
                var destructuringPolicy = new SensitiveDataDestructuringPolicy(maskingOperator);

                // 2. 配置 SensitiveDataEnricherOptions
                var options = new SensitiveDataEnricherOptions
                {
                    Mode = MaskingMode.Globally,
                    MaskValue = "***MASKED***",
                    MaskingOperators = new List<IMaskingOperator> { maskingOperator },
                    MaskProperties = new List<MaskProperty>
                    {
                        new() { Name = "Name" },
                        new() { Name = "EmployeeName" },
                        new() { Name = "Password" },
                        new() { Name = "Token" },
                        new() { Name = "BankAccount" },
                        new() { Name = "CreditCard" }
                    },
                    ExcludeProperties = new List<string> { "Id", "EventId", "Timestamp" }
                };

                // 3. 核心管線串接：先注入自定義物件解構政策，再掛載全域脫敏 Enricher
                return loggerConfiguration
                    .Destructure.With(destructuringPolicy)
                    .Enrich.WithSensitiveDataMasking(options);
            }
            catch (Exception ex)
            {
                Serilog.Debugging.SelfLog.WriteLine($"[SerilogMaskingConfiguration Critical Error]: {ex.Message}");
                throw new InvalidOperationException("無法正確初始化 Serilog 脫敏與解構配置管線。", ex);
            }
        }
    }
}