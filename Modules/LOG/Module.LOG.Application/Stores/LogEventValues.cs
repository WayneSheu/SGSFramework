using Serilog.Events;

namespace SGS.Modules.LOG.Application.Stores;

internal static class LogEventValues
{
    /// <summary>讀取屬性文字值（去除 Serilog 字串引號）；不存在回 null。</summary>
    public static string? Text(LogEvent e, string key)
        => e.Properties.TryGetValue(key, out var v)
            ? (v is ScalarValue { Value: string s } ? s : v.ToString().Trim('"'))
            : null;

    public static object Db(string? value) => string.IsNullOrEmpty(value) ? DBNull.Value : value;
}
