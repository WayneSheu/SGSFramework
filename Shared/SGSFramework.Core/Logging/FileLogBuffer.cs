#nullable enable
using System.Text.Json;
using Serilog.Events;
using Serilog.Parsing;

namespace SGSFramework.Core.Logging
{
    /// <summary>
    /// 日誌檔案緩衝：外掛未載入或日誌資料庫不可用時，保存完整結構化欄位，待恢復後回補。
    /// 目錄：{BaseDirectory}/Log_Buffer/{kind}/*.json
    /// </summary>
    public static class FileLogBuffer
    {
        public const string SystemKind = "system";
        public const string SecurityKind = "security";

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

        public static string GetDirectory(string kind)
            => Path.Combine(AppContext.BaseDirectory, "Log_Buffer", kind);

        /// <summary>將一批事件寫成單一緩衝檔。任何失敗僅寫 SelfLog，不再回寫 Serilog 以避免回圈。</summary>
        public static async Task WriteAsync(string kind, IEnumerable<LogEvent> events, CancellationToken ct = default)
        {
            try
            {
                var dtos = events.Select(BufferedLogEvent.From).ToList();
                if (dtos.Count == 0) return;

                var dir = GetDirectory(kind);
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json");
                var tmp = path + ".tmp";
                await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(dtos, JsonOptions), ct).ConfigureAwait(false);
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                Serilog.Debugging.SelfLog.WriteLine($"[LogBuffer] 寫入緩衝檔失敗 ({kind}): {ex.Message}");
            }
        }

        /// <summary>依時間順序列出待回補的緩衝檔。</summary>
        public static IReadOnlyList<string> ListFiles(string kind)
        {
            var dir = GetDirectory(kind);
            return Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal).ToList()
                : Array.Empty<string>();
        }

        public static async Task<IReadOnlyList<LogEvent>> ReadAsync(string path, CancellationToken ct = default)
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var dtos = JsonSerializer.Deserialize<List<BufferedLogEvent>>(json, JsonOptions) ?? new();
            return dtos.Select(d => d.ToLogEvent()).ToList();
        }

        /// <summary>
        /// 回補所有緩衝檔：每個檔案寫入成功才刪除；遇到失敗即停止（保留檔案待下次）。
        /// </summary>
        public static async Task<int> ReplayAsync(
            string kind, Func<IReadOnlyList<LogEvent>, CancellationToken, Task> writer, CancellationToken ct = default)
        {
            var replayed = 0;
            foreach (var file in ListFiles(kind))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var events = await ReadAsync(file, ct).ConfigureAwait(false);
                    if (events.Count > 0) await writer(events, ct).ConfigureAwait(false);
                    File.Delete(file);
                    replayed += events.Count;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Serilog.Debugging.SelfLog.WriteLine($"[LogBuffer] 回補失敗 {file}: {ex.Message}");
                    break;
                }
            }
            return replayed;
        }

        /// <summary>可序列化的 LogEvent 快照（屬性以字串保存，與現有處理器 Trim('"') 讀法相容）。</summary>
        internal sealed class BufferedLogEvent
        {
            public DateTimeOffset Timestamp { get; set; }
            public string Level { get; set; } = nameof(LogEventLevel.Information);
            public string Message { get; set; } = string.Empty;
            public string? Exception { get; set; }
            public Dictionary<string, string?> Properties { get; set; } = new();

            public static BufferedLogEvent From(LogEvent e) => new()
            {
                Timestamp = e.Timestamp,
                Level = e.Level.ToString(),
                Message = e.RenderMessage(),
                Exception = e.Exception?.ToString(),
                Properties = e.Properties.ToDictionary(
                    p => p.Key,
                    p => p.Value is ScalarValue { Value: string s } ? s : p.Value.ToString())
            };

            public LogEvent ToLogEvent()
            {
                var props = Properties
                    .Select(p => new LogEventProperty(p.Key, new ScalarValue(p.Value)))
                    .ToList();
                // 以純文字 token 建立範本，避免訊息內的大括號被重新解析
                var template = new MessageTemplate(new MessageTemplateToken[] { new TextToken(Message) });
                var level = Enum.TryParse<LogEventLevel>(Level, out var l) ? l : LogEventLevel.Information;
                return new LogEvent(Timestamp, level, null, template, props);
            }
        }
    }
}
