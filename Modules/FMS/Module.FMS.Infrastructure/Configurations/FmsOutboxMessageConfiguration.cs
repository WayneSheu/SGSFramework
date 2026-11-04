using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Outbox;
using System;
using System.Collections.Generic;
using System.Text;
using SGSFramework.Infrastructure.Persistence.Extensions;

namespace SGS.Modules.FMS.Infrastructure.Configurations
{
    public sealed class FmsOutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            // 將通用 OutboxMessage 映射至 fms.OutboxMessages
            builder.ConfigureOutboxTable("fms");
        }
    }
}
