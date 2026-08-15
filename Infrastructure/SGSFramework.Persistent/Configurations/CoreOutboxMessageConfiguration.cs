#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Infrastructure.Persistence.Extensions;

namespace SGSFramework.Infrastructure.Persistence.Configurations;

public sealed class CoreOutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        // 將通用 OutboxMessage 映射至 core.OutboxMessages
        builder.ConfigureOutboxTable("core");
    }
}