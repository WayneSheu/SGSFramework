// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.AuditLog/Channels/AuditChannel.cs
// 架構層級: Infrastructure Layer
// ==========================================

namespace SGSFramework.AuditLog.Channels;

using SGSFramework.AuditLog.Abstractions;
using SGSFramework.AuditLog.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public sealed class AuditChannel<TContext> : IAuditChannel<TContext>
{
    private readonly Channel<AuditEntry> _channel;

    public AuditChannel()
    {
        var options = new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,  // 專屬指定 TContext 的 Worker 單一消費者[cite: 23]
            SingleWriter = false  // 支援多個 HTTP Request 同時寫入[cite: 23]
        };

        _channel = Channel.CreateBounded<AuditEntry>(options);
    }

    /// <summary>
    /// 異步寫入單筆稽核紀錄
    /// </summary>
    public ValueTask AddAuditLogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return _channel.Writer.WriteAsync(entry, ct);
    }

    /// <summary>
    /// 批次寫入稽核紀錄
    /// </summary>
    public async ValueTask AddBatchAuditLogAsync(IEnumerable<AuditEntry> entries, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        foreach (var entry in entries)
        {
            if (entry is not null)
            {
                await _channel.Writer.WriteAsync(entry, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 同步非阻塞嘗試寫入 (適合於非 async 上下文中使用)[cite: 23]
    /// </summary>
    public bool TryAddAuditLog(AuditEntry entry)
    {
        if (entry is null) return false;
        return _channel.Writer.TryWrite(entry); //[cite: 23]
    }

    /// <summary>
    /// 供背景服務讀取佇列所有訊息
    /// </summary>
    public IAsyncEnumerable<AuditEntry> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}