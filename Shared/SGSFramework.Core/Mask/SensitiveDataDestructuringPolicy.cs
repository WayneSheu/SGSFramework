using Serilog.Core;
using Serilog.Events;
using SGSFramework.Core.Abstractions.Attributes;
using System.Reflection;

namespace SGSFramework.Core.Mask
{
    /// <summary>
    /// 當 Serilog 遇到以 {@Object} 記錄的物件時，此政策會檢查屬性上是否有標註 [Sensitive]
    /// </summary>
    public class SensitiveDataDestructuringPolicy : IDestructuringPolicy
    {
        private readonly UniversalMaskingOperator _maskingOperator;

        // 透過建構子注入您的全域脫敏運算子
        public SensitiveDataDestructuringPolicy(UniversalMaskingOperator maskingOperator)
        {
            _maskingOperator = maskingOperator;
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

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var logProperties = new List<LogEventProperty>();

            foreach (var prop in props)
            {
                object? propValue = prop.GetValue(value);
                if (propValue == null) continue;

                string stringValue = propValue.ToString() ?? "";
                var sensitiveAttr = prop.GetCustomAttribute<SensitiveDataAttribute>();

                // 策略 1：顯式標註 [Sensitive] 的屬性 -> 強制脫敏
                if (sensitiveAttr != null)
                {
                    logProperties.Add(new LogEventProperty(prop.Name, new ScalarValue(sensitiveAttr.Format)));
                }
                // 策略 2：未標註但符合特徵 (手機, Email, 姓名...) -> 自動脫敏
                else if (propValue is string)
                {
                    var maskResult = _maskingOperator.Mask(stringValue, prop.Name);
                    if (maskResult.Match)
                    {
                        logProperties.Add(new LogEventProperty(prop.Name, new ScalarValue(maskResult.Result)));
                    }
                    else
                    {
                        logProperties.Add(new LogEventProperty(prop.Name, propertyFactory.CreatePropertyValue(propValue, true)));
                    }
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
