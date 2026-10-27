using Serilog.Core;
using Serilog.Events;
using SGSFramework.Core.Abstractions.Attributes;
using System.Reflection;

namespace SGSFramework.Core.Mask
{
    /// <summary>
    /// 當 Serilog 遇到以 {@Object} 記錄的物件時，此政策會檢查屬性上是否有標註 [Masked] / [SensitiveData] / [Encrypted]
    /// </summary>
    public class SensitiveDataDestructuringPolicy : IDestructuringPolicy
    {
        private readonly UniversalMaskingOperator _maskingOperator;
        private readonly MaskingService _maskService;

        // 透過建構子注入您的全域脫敏運算子
        public SensitiveDataDestructuringPolicy(UniversalMaskingOperator maskingOperator, MaskingService maskService)
        {
            _maskingOperator = maskingOperator;
            _maskService = maskService;
        }

        public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyFactory, out LogEventPropertyValue? result)
        {
            var type = value.GetType();

            // 排除基本型別，避免無限遞迴
            if (type.IsPrimitive || type == typeof(string) || type.FullName!.StartsWith("System."))
            {
                result = null;
                return false;
            }

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);
            var logProperties = new List<LogEventProperty>();

            foreach (var prop in props)
            {
                object? propValue = prop.GetValue(value);
                if (propValue == null) continue;

                var sensitiveAttr = prop.GetCustomAttribute<SensitiveDataAttribute>();

                // 策略 1：顯式標註 [Masked] / [SensitiveData] 的屬性 -> 依格式強制脫敏
                if (sensitiveAttr != null)
                {
                    var masked = _maskService.MaskByFormat(propValue.ToString() ?? string.Empty, sensitiveAttr.Format);
                    logProperties.Add(new LogEventProperty(prop.Name, new ScalarValue(masked)));
                }
                // 策略 2：標註 [Encrypted] 的落地加密欄位 -> 一律完全遮蔽
                else if (prop.IsDefined(typeof(EncryptedAttribute)))
                {
                    logProperties.Add(new LogEventProperty(prop.Name, new ScalarValue("***MASKED***")));
                }
                // 策略 3：未標註但符合特徵 (手機, Email, 姓名, 金鑰...) -> 自動脫敏
                else if (propValue is string stringValue)
                {
                    var maskResult = _maskingOperator.Mask(stringValue, prop.Name);
                    logProperties.Add(maskResult.Match
                        ? new LogEventProperty(prop.Name, new ScalarValue(maskResult.Result))
                        : new LogEventProperty(prop.Name, propertyFactory.CreatePropertyValue(propValue, true)));
                }
                else
                {
                    // 其他非字串物件，繼續遞迴解構
                    logProperties.Add(new LogEventProperty(prop.Name, propertyFactory.CreatePropertyValue(propValue, true)));
                }
            }

            result = new StructureValue(logProperties, type.Name);
            return true;
        }
    }
}
