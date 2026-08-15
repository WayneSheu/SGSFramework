#nullable enable
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Outbox;

namespace SGSFramework.Infrastructure.Persistence.Extensions;

public static class OutboxConfigurationExtensions
{
    /// <summary>
    /// 針對完整 OutboxMessage 實體結構進行表對映、欄位約束與索引設定，並支援動態 Schema
    /// </summary>
    /// <param name="builder">EF Core EntityTypeBuilder</param>
    /// <param name="schemaName">模組專屬的資料庫 Schema 名稱 (例如 "core", "org")</param>
    /// <returns>設定後的 EntityTypeBuilder</returns>
    /// <exception cref="ArgumentNullException">當 builder 為 null 時拋出 Exception</exception>
    /// <exception cref="ArgumentException">當 schemaName 為空字串或 null 時拋出 Exception</exception>
    public static EntityTypeBuilder<OutboxMessage> ConfigureOutboxTable(
        this EntityTypeBuilder<OutboxMessage> builder,
        string schemaName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

        // 1. 動態指定目標 Schema 與資料表名稱
        builder.ToTable("OutboxMessages", schemaName);

        // 2. 主鍵設定
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        // 3. 追蹤欄位設定
        builder.Property(x => x.CorrelationId)
            .HasMaxLength(100)
            .IsRequired(false)
            .IsUnicode(false);

        builder.Property(x => x.CausationId)
            .HasMaxLength(200)
            .IsRequired(false)
            .IsUnicode(false);

        // 4. 訊息型別與內容約束
        builder.Property(x => x.Type)
            .HasMaxLength(500)
            .IsRequired()
            .IsUnicode(false);

        builder.Property(x => x.Content)
            .IsRequired()
            .IsUnicode(true);

        // 5. 時間戳記與狀態欄位
        builder.Property(x => x.OccurredOnUtc)
            .IsRequired();

        builder.Property(x => x.ProcessedOnUtc)
            .IsRequired(false);

        builder.Property(x => x.ScheduledAtUtc)
            .IsRequired(false);

        builder.Property(x => x.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.IsDead)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.LastError)
            .IsRequired(false)
            .IsUnicode(true);

        // 6. 複合索引優化：Worker Polling 查詢條件 (ProcessedOnUtc, IsDead, ScheduledAtUtc)
        builder.HasIndex(x => new { x.ProcessedOnUtc, x.IsDead, x.ScheduledAtUtc })
            .HasDatabaseName($"IX_{schemaName}_OutboxMessages_FetchPending");

        // 7. 單獨索引：支援分散式追蹤分析
        builder.HasIndex(x => x.CorrelationId)
            .HasDatabaseName($"IX_{schemaName}_OutboxMessages_CorrelationId");

        return builder;
    }
}