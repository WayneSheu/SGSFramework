using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Migrations
{
    //public sealed class MigrationService : IMigrationService
    //{
    //    private readonly DbContext _context;
    //    private readonly ILogger<MigrationService> _logger;

    //    public MigrationService(DbContext context, ILogger<MigrationService> logger)
    //    {
    //        _context = context ?? throw new ArgumentNullException(nameof(context));
    //        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    //    }

    //    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    //    {
    //        string currentStep = "檢查待處理的遷移項目";
    //        try
    //        {
    //            _logger.LogInformation("開始執行 EF Core 10 資料庫遷移。目標版本: {Target}", targetMigration ?? "最新版本");

    //            // 1. 檢查連線能力與取得待處理遷移
    //            var pendingMigrations = await _context.Database.GetPendingMigrationsAsync(cancellationToken);
    //            if (!pendingMigrations.Any())
    //            {
    //                _logger.LogInformation("資料庫已是最新狀態，無需套用遷移。");
    //                return;
    //            }

    //            // 2. 執行遷移
    //            currentStep = "執行資料庫遷移套用";
    //            if (string.IsNullOrWhiteSpace(targetMigration))
    //            {
    //                await _context.Database.MigrateAsync(cancellationToken);
    //            }
    //            else
    //            {
    //                // EF Core 10+ 針對特定目標遷移的處理邏輯
    //                var migrator = _context.Database.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
    //                await migrator.MigrateAsync(targetMigration, cancellationToken);
    //            }

    //            _logger.LogInformation("EF Core 10 資料庫遷移成功套用。");
    //        }
    //        catch (OperationCanceledException ex)
    //        {
    //            _logger.LogWarning(ex, "資料庫遷移操作已被取消。");
    //            throw new MigrationException("資料庫遷移操作超時或被取消。", currentStep, targetMigration, ex);
    //        }
    //        catch (Exception ex) when (ex is not MigrationException)
    //        {
    //            _logger.LogError(ex, "在步驟 '{Step}' 執行 EF Core 10 遷移時發生未預期的例外狀況。", currentStep);

    //            // 封裝底層 EF Core 例外（如 DbUpdateException, SocketException 等）
    //            throw new MigrationException(
    //                $"資料庫遷移失敗。錯誤發生於步驟: {currentStep}。詳情: {ex.Message}",
    //                currentStep,
    //                targetMigration,
    //                ex
    //            );
    //        }
    //    }
    //}
}
