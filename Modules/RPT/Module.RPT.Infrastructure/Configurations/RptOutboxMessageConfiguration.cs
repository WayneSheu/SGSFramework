using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Outbox;
using System;
using System.Collections.Generic;
using System.Text;
using SGSFramework.Infrastructure.Persistence.Extensions;

namespace SGS.Modules.RPT.Infrastructure.Configurations
{
    public sealed class RptOutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            // 將通用 OutboxMessage 映射至 rpt.OutboxMessages
            builder.ConfigureOutboxTable("rpt");
        }
    }
}
