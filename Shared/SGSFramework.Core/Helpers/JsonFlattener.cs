using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace SGSFramework.Core.Helpers
{

    /// <summary>
    /// 高效 JSON 扁平化工具 (JsonFlattener)
    /// 不使用反射(Reflection)，而是直接操作 System.Text.Json.JsonDocument，這在效能上非常高效且記憶體佔用低。
    /// </summary>
    public static class JsonFlattener
    {
        /// <summary>
        /// 將任意層級的 JSON 扁平化為 Dictionary
        /// 輸入: { "User": { "Name": "A", "Roles": ["Admin", "Dev"] } }
        /// 輸出: 
        /// "User.Name": "A"
        /// "User.Roles[0]": "Admin"
        /// "User.Roles[1]": "Dev"
        /// </summary>
        public static Dictionary<string, string> Flatten(string json)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    FlattenElement(doc.RootElement, "", result);
                }
            }
            catch
            {
                // 容錯處理：如果 JSON 格式錯誤，直接回傳原始字串作為 Key
                result.Add("RawData", json);
            }

            return result;
        }

        private static void FlattenElement(JsonElement element, string prefix, Dictionary<string, string> result)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        // 遞迴處理物件屬性，組合 Key (例如: Address.City)
                        string key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                        FlattenElement(property.Value, key, result);
                    }
                    break;

                case JsonValueKind.Array:
                    int index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        // 處理陣列，使用索引作為 Key (例如: Tags[0])
                        string key = $"{prefix}[{index}]";
                        FlattenElement(item, key, result);
                        index++;
                    }
                    break;

                case JsonValueKind.Null:
                    result[prefix] = "null";
                    break;

                default:
                    // 基礎型別 (String, Number, True/False) 直接轉字串
                    result[prefix] = element.ToString();
                    break;
            }
        }
    }
}
