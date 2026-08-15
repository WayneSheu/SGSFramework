using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Outbox;
using System;
using System.Collections.Generic;
using System.Text;
using SGSFramework.Infrastructure.Persistence.Extensions;

namespace SGS.Modules.ORG.Infrastructure.Configurations
{
    public sealed class OrgOutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            // 將通用 OutboxMessage 映射至 org.OutboxMessages
            builder.ConfigureOutboxTable("org");
        }
    }
}
