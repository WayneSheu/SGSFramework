using FluentAssertions;
using Serilog.Events;
using Serilog.Parsing;
using SGSFramework.Core.Logging;
using Xunit;

namespace SGSFramework.Core.Tests;

/// <summary>日誌檔案緩衝：落檔保留完整結構欄位、依序回補、失敗保留檔案。</summary>
public sealed class FileLogBufferTests : IDisposable
{
    private readonly string _kind = $"test-{Guid.NewGuid():N}";

    public void Dispose()
    {
        var dir = FileLogBuffer.GetDirectory(_kind);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    private static LogEvent Create(string message, LogEventLevel level = LogEventLevel.Information, params (string Key, string Value)[] props)
    {
        var template = new MessageTemplate(new MessageTemplateToken[] { new TextToken(message) });
        var properties = props.Select(p => new LogEventProperty(p.Key, new ScalarValue(p.Value)));
        return new LogEvent(DateTimeOffset.Parse("2026-11-04T11:46:44+08:00"), level, null, template, properties);
    }

    [Fact]
    public async Task Write_then_replay_preserves_fields_and_deletes_file()
    {
        await FileLogBuffer.WriteAsync(_kind, new[]
        {
            Create("使用者登入成功 {not-a-placeholder}", LogEventLevel.Warning,
                ("CorrelationId", "abc123"), ("UserId", "u-1"), ("EventCategory", "Auth.Login"))
        });

        FileLogBuffer.ListFiles(_kind).Should().HaveCount(1);

        var received = new List<LogEvent>();
        var replayed = await FileLogBuffer.ReplayAsync(_kind, (events, _) =>
        {
            received.AddRange(events);
            return Task.CompletedTask;
        });

        replayed.Should().Be(1);
        FileLogBuffer.ListFiles(_kind).Should().BeEmpty();

        var e = received.Single();
        e.Level.Should().Be(LogEventLevel.Warning);
        e.Timestamp.Should().Be(DateTimeOffset.Parse("2026-11-04T11:46:44+08:00"));
        // 大括號不得被當成範本重新解析
        e.RenderMessage().Should().Be("使用者登入成功 {not-a-placeholder}");
        // 屬性以 ScalarValue 還原：處理器以 ToString().Trim('"') 讀取
        e.Properties["CorrelationId"].ToString().Trim('"').Should().Be("abc123");
        e.Properties["EventCategory"].ToString().Trim('"').Should().Be("Auth.Login");
    }

    [Fact]
    public async Task Replay_keeps_file_and_stops_when_writer_fails()
    {
        await FileLogBuffer.WriteAsync(_kind, new[] { Create("first") });
        await Task.Delay(5); // 檔名含毫秒時間戳，確保排序
        await FileLogBuffer.WriteAsync(_kind, new[] { Create("second") });

        var calls = 0;
        var replayed = await FileLogBuffer.ReplayAsync(_kind, (_, _) =>
        {
            calls++;
            throw new InvalidOperationException("DB down");
        });

        replayed.Should().Be(0);
        calls.Should().Be(1, "遇到失敗即停止，避免打亂順序");
        FileLogBuffer.ListFiles(_kind).Should().HaveCount(2);
    }

    [Fact]
    public async Task Replay_processes_files_in_chronological_order()
    {
        await FileLogBuffer.WriteAsync(_kind, new[] { Create("first") });
        await Task.Delay(5);
        await FileLogBuffer.WriteAsync(_kind, new[] { Create("second") });

        var order = new List<string>();
        await FileLogBuffer.ReplayAsync(_kind, (events, _) =>
        {
            order.AddRange(events.Select(e => e.RenderMessage()));
            return Task.CompletedTask;
        });

        order.Should().Equal("first", "second");
    }

    [Fact]
    public async Task Write_with_no_events_creates_no_file()
    {
        await FileLogBuffer.WriteAsync(_kind, Array.Empty<LogEvent>());

        FileLogBuffer.ListFiles(_kind).Should().BeEmpty();
    }
}
