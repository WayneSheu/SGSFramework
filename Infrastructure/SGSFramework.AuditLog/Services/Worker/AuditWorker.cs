// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.AuditLog/Services/Worker/AuditWorker.cs
// 架構層級: Infrastructure Layer (.NET 10 / Clean Architecture)
// ==========================================

namespace SGSFramework.AuditLog.Services.Worker;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGSFramework.AuditLog.Abstractions;
using SGSFramework.AuditLog.DTOs;
using SGSFramework.Core.Abstractions.AuditLogs;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 企業級稽核日誌背景非同步處理服務
/// 職責：由 System.Threading.Channels 佇列消費 AuditEntry，並進行批次寫入、定時 Flush、異常重試與降級備份。
/// </summary>
/// <typeparam name="TContext">遵循 IAuditDbContext 介面之資料庫內容物件</typeparam>
public class AuditWorker<TContext> : BackgroundService
    where TContext : DbContext, IAuditDbContext
{
    private readonly IAuditChannel<TContext> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditWorker<TContext>> _logger;
    private readonly IAuditStorageStrategy<TContext> _storageStrategy;
    private readonly string _moduleName;
    private readonly string _fallbackPath;

    private const int MaxRetries = 3;
    private const int BatchSize = 10;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = false
    };

    public AuditWorker(
        IAuditChannel<TContext> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditWorker<TContext>> logger,
        IAuditStorageStrategy<TContext> storageStrategy)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageStrategy = storageStrategy ?? throw new ArgumentNullException(nameof(storageStrategy));

        _moduleName = typeof(TContext).Name;
        _fallbackPath = Path.Combine(AppContext.BaseDirectory, "Audit_Failures", _moduleName);

        Directory.CreateDirectory(_fallbackPath);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[{Module}] AuditWorker 處理程序啟動...", _moduleName);

        // 啟動時自動嘗試復原降級磁碟檔
        await RecoverFallbackLogsAsync(stoppingToken).ConfigureAwait(false);

        var buffer = new List<AuditLogEntity>(BatchSize);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeoutCts.CancelAfter(FlushInterval);

                try
                {
                    // 改用 IAuditChannel 的 ReadAllAsync (IAsyncEnumerable) 搭配 TaskCanceledException/OperationCanceledException 處理 Flush 機制
                    await foreach (var entry in _channel.ReadAllAsync(timeoutCts.Token).ConfigureAwait(false))
                    {
                        buffer.Add(MapToEntity(entry));
                        if (buffer.Count >= BatchSize)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // 定時 FlushInterval 抵達，紀錄 Trace 日誌，繼續執行下方的批次 Flush 操作
                    _logger.LogTrace("[{Module}] 抵達 FlushInterval ({Interval}s) 門檻，準備 Flush 緩衝區資料 (Count: {Count})。",
                        _moduleName, FlushInterval.TotalSeconds, buffer.Count);
                }

                // 緩衝區只要有資料即進行 DB 寫入
                if (buffer.Count > 0)
                {
                    await SaveBatchWithReliabilityAsync(buffer, stoppingToken).ConfigureAwait(false);
                    buffer.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("[{Module}] 收到關閉訊號，中斷 Channel 讀取迴圈...", _moduleName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Module}] 讀取 AuditChannel 時發生非預期例外。", _moduleName);
        }
        finally
        {
            // 服務終止前，強制 Flush 緩衝區剩餘資料
            if (buffer.Count > 0)
            {
                _logger.LogInformation("[{Module}] 服務終止前 Flush 剩餘 {Count} 筆稽核紀錄...", _moduleName, buffer.Count);
                await SaveBatchWithReliabilityAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                buffer.Clear();
            }

            _logger.LogInformation("[{Module}] AuditWorker 已完全終止。", _moduleName);
        }
    }

    private async Task SaveBatchWithReliabilityAsync(List<AuditLogEntity> batch, CancellationToken ct)
    {
        if (batch == null || batch.Count == 0) return;

        int retryCount = 0;
        while (retryCount < MaxRetries)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

                await _storageStrategy.SaveBatchAsync(dbContext, batch, ct).ConfigureAwait(false);
                _logger.LogInformation("[{Module}] SaveBatchAsync 成功寫入 {Count} 筆稽核資料至 DB。", _moduleName, batch.Count);
                return;
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogError(ex, "[{Module}] 寫入 DB 失敗 (第 {Count}/{Max} 次)，原因: {Message}", _moduleName, retryCount, MaxRetries, ex.Message);

                if (retryCount >= MaxRetries)
                {
                    _logger.LogCritical(ex, "[{Module}] 重試次數已達上限，轉為寫入本地降級備份檔。", _moduleName);
                    await SaveToFallbackDiskAsync(batch, ex).ConfigureAwait(false);
                    return;
                }

                await Task.Delay(1000 * retryCount, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task RecoverFallbackLogsAsync(CancellationToken ct)
    {
        try
        {
            var files = Directory.GetFiles(_fallbackPath, "*.json");
            if (files.Length == 0) return;

            _logger.LogInformation("[{Module}] 發現 {Length} 個降級備份檔案，開始嘗試復原...", _moduleName, files.Length);

            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    string json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                    using var document = JsonDocument.Parse(json);

                    if (document.RootElement.TryGetProperty("Logs", out var logsElement))
                    {
                        var batch = JsonSerializer.Deserialize<List<AuditLogEntity>>(logsElement.GetRawText(), JsonOptions);

                        if (batch != null && batch.Count > 0)
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

                            await _storageStrategy.RecoverLogsAsync(dbContext, batch, ct).ConfigureAwait(false);
                            _logger.LogInformation("[{Module}] 檔案 {FileName} 復原成功。", _moduleName, Path.GetFileName(file));
                            File.Delete(file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Module}] 復原檔案 {FileName} 失敗。", _moduleName, Path.GetFileName(file));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Module}] 存取降級備份目錄時發生非預期例外。", _moduleName);
        }
    }

    private async Task SaveToFallbackDiskAsync(List<AuditLogEntity> batch, Exception ex)
    {
        try
        {
            string fileName = $"audit_fail_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json";
            string fullPath = Path.Combine(_fallbackPath, fileName);

            var payload = new
            {
                Module = _moduleName,
                Timestamp = DateTime.UtcNow,
                Exception = ex.ToString(),
                Logs = batch
            };

            string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) });
            await File.WriteAllTextAsync(fullPath, json).ConfigureAwait(false);
            _logger.LogInformation("[{Module}] 已將 {Count} 筆資料安全備份至 {FileName}", _moduleName, batch.Count, fileName);
        }
        catch (Exception diskEx)
        {
            _logger.LogCritical(diskEx, "[{Module}] 嚴重錯誤：無法寫入 DB 且本地備份失敗！", _moduleName);
        }
    }

    private static AuditLogEntity MapToEntity(AuditEntry dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        string safeTraceId = string.IsNullOrWhiteSpace(dto.TraceId)
            ? Guid.NewGuid().ToString("N")
            : (dto.TraceId.Length > 64 ? dto.TraceId[..64] : dto.TraceId);

        string safeSchema = dto.Schema?.Length > 64 ? dto.Schema[..64] : (dto.Schema ?? "core");
        string safeTableName = (dto.TableName ?? "Unknown").Length > 128 ? dto.TableName![..128] : (dto.TableName ?? "Unknown");
        string safeAction = (dto.Action ?? "Modified").Length > 50 ? dto.Action![..50] : (dto.Action ?? "Modified");

        string genesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

        return new AuditLogEntity
        {
            Schema = safeSchema,
            TableName = safeTableName,
            Action = safeAction,
            Timestamp = dto.Timestamp == default ? DateTimeOffset.UtcNow : dto.Timestamp,
            CreatedAt = DateTimeOffset.UtcNow,
            UserId = dto.UserId?.Length > 128 ? dto.UserId[..128] : dto.UserId,
            TraceId = safeTraceId,
            RemoteIp = dto.RemoteIp?.Length > 64 ? dto.RemoteIp[..64] : dto.RemoteIp,
            KeyValues = dto.KeyValues?.Count > 0 ? JsonSerializer.Serialize(dto.KeyValues, JsonOptions) : null,
            OldValues = dto.OldValues?.Count > 0 ? JsonSerializer.Serialize(dto.OldValues, JsonOptions) : null,
            NewValues = dto.NewValues?.Count > 0 ? JsonSerializer.Serialize(dto.NewValues, JsonOptions) : null,
            ChangedColumns = dto.ChangedColumns?.Count > 0 ? JsonSerializer.Serialize(dto.ChangedColumns, JsonOptions) : null,
            PreviousHash = genesisHash,
            StoredHash = genesisHash,
            IsRepaired = false
        };
    }
}