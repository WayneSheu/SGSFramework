using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.AuditLogs;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.AuditLog.Channels;
using SGSFramework.AuditLog.DTOs;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace SGSFramework.AuditLog.Services.Worker
{

    /// <summary>
    /// 泛型 AuditWorker，TContext 為各模組的 DbContext
    /// </summary>
    public class AuditWorker<TContext> : BackgroundService
    where TContext : DbContext, IAuditDbContext
    {
        private readonly AuditChannel _channel;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AuditWorker<TContext>> _logger;
        private readonly IAuditStorageStrategy<TContext> _storageStrategy;
        private readonly string _moduleName;
        private readonly string _fallbackPath;
        private readonly string _auditErrorPath;

        private const int MaxRetries = 3;
        private const int BatchSize = 1000;

        public AuditWorker(
            AuditChannel channel,
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
            _auditErrorPath = Path.Combine(AppContext.BaseDirectory, "Audit_Errors", _moduleName);

            Directory.CreateDirectory(_fallbackPath);
            Directory.CreateDirectory(_auditErrorPath);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[{Module}] AuditWorker 啟動...", _moduleName);

            await RecoverFallbackLogsAsync(stoppingToken);
            var buffer = new List<SGSFramework.Core.Abstractions.Entities.AuditLogs.AuditLogEntity>(BatchSize);

            while (await _channel.Reader.WaitToReadAsync(stoppingToken))
            {
                while (buffer.Count < BatchSize && _channel.Reader.TryRead(out var entry))
                {
                    buffer.Add(MapToEntity(entry));
                }

                if (buffer.Count > 0)
                {
                    await SaveBatchWithReliabilityAsync(buffer, stoppingToken);
                    buffer.Clear();
                }
            }

            _logger.LogInformation("[{Module}] Reader 結束（Channel 已關閉或 Token 已取消）", _moduleName);
        }

        private async Task SaveBatchWithReliabilityAsync(List<SGSFramework.Core.Abstractions.Entities.AuditLogs.AuditLogEntity> batch, CancellationToken ct)
        {
            if (batch == null || batch.Count == 0) return;

            int retryCount = 0;
            while (retryCount < MaxRetries)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

                    await _storageStrategy.SaveBatchAsync(dbContext, batch, ct);
                    _logger.LogInformation("[{Module}] SaveBatchAsync 成功寫入 {Count} 筆資料。", _moduleName, batch.Count);
                    return;
                }
                catch (Exception ex)
                {
                    retryCount++;
                    _logger.LogWarning(ex, "[{Module}] 寫入 DB 失敗，嘗試第 {Count}/{Max} 次...", _moduleName, retryCount, MaxRetries);

                    if (retryCount >= MaxRetries)
                    {
                        _logger.LogError(ex, "[{Module}] 重試次數已達上限，轉為寫入本地降級備份檔。", _moduleName);
                        await SaveToFallbackDiskAsync(batch, ex);
                        return;
                    }

                    await Task.Delay(1000 * retryCount, ct);
                }
            }
        }

        private async Task RecoverFallbackLogsAsync(CancellationToken ct)
        {
            var files = Directory.GetFiles(_fallbackPath, "*.json");
            if (files.Length == 0) return;

            _logger.LogInformation("[{Module}] 發現 {Length} 個備份檔案，開始嘗試復原...", _moduleName, files.Length);

            foreach (var file in files)
            {
                try
                {
                    string json = await File.ReadAllTextAsync(file, ct);
                    var batch = JsonSerializer.Deserialize<List<SGSFramework.Core.Abstractions.Entities.AuditLogs.AuditLogEntity>>(json);

                    if (batch != null && batch.Count > 0)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

                        await _storageStrategy.RecoverLogsAsync(dbContext, batch, ct);
                        _logger.LogInformation("[{Module}] 檔案 {FileName} 復原成功。", _moduleName, Path.GetFileName(file));
                        File.Delete(file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{Module}] 復原檔案 {FileName} 失敗，將留待下一次重試。", _moduleName, Path.GetFileName(file));
                }
            }
        }

        private async Task SaveToFallbackDiskAsync(List<SGSFramework.Core.Abstractions.Entities.AuditLogs.AuditLogEntity> batch, Exception ex)
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

                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(fullPath, json);
                _logger.LogInformation("[{Module}] 已將 {Count} 筆資料安全備份至 {FileName}", _moduleName, batch.Count, fileName);
            }
            catch (Exception diskEx)
            {
                _logger.LogCritical(diskEx, "[{Module}] 嚴重錯誤：無法寫入 DB 且本地備份失敗，發生資料遺失風險！", _moduleName);
            }
        }

        private static SGSFramework.Core.Abstractions.Entities.AuditLogs.AuditLogEntity MapToEntity(AuditEntry dto)
        {
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = false
            };

            // 確保 TraceId 為 32 位元 HEX 格式
            string traceId = !string.IsNullOrWhiteSpace(dto.TraceId)
                ? dto.TraceId
                : (dto.Id != Guid.Empty ? dto.Id.ToString("N") : Guid.NewGuid().ToString("N"));

            return new SGSFramework.Core.Abstractions.Entities.AuditLogs.AuditLogEntity
            {
                Schema = dto.Schema,
                TableName = dto.TableName ?? string.Empty,
                Action = dto.Action ?? string.Empty,
                Timestamp = dto.Timestamp == default ? DateTime.UtcNow : dto.Timestamp,
                CreatedAt = DateTime.UtcNow,
                UserId = dto.UserId,
                TraceId = traceId,
                RemoteIp = dto.RemoteIp,
                KeyValues = dto.KeyValues?.Count > 0 ? JsonSerializer.Serialize(dto.KeyValues, options) : null,
                OldValues = dto.OldValues?.Count > 0 ? JsonSerializer.Serialize(dto.OldValues, options) : null,
                NewValues = dto.NewValues?.Count > 0 ? JsonSerializer.Serialize(dto.NewValues, options) : null,
                ChangedColumns = dto.ChangedColumns?.Count > 0 ? JsonSerializer.Serialize(dto.ChangedColumns) : null,
                PreviousHash = string.Empty,
                StoredHash = string.Empty,
                IsRepaired = false
            };
        }
    }

}
