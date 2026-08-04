using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Identity.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DbContexts
{
    /// <summary>
    /// 擴充的 Identity 核心 DbContext
    /// </summary>
    public class ExtendedIdentityDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public ExtendedIdentityDbContext(DbContextOptions<ExtendedIdentityDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 自訂 Fluent API 設定
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.FullName)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .IsRequired();
            });
        }
    }
}
