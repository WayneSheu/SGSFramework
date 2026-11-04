using Microsoft.Extensions.Options;

namespace SGS.Modules.FMS.Tests;

internal sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; } = value;
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

/// <summary>測試用暫存目錄，Dispose 時刪除</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sgs-fm-tests-" + Guid.NewGuid().ToString("N"));

    public TempDirectory() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}
