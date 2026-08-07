using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Base;
using SGSFramework.Identity.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DbContexts
{
    /// <summary>
    /// 泛型化 Identity DbContext 基底類別
    /// </summary>
    public abstract class GenericIdentityDbContext<TUser, TRole, TKey> : IdentityDbContext<TUser, TRole, TKey>
        where TUser : IdentityUser<TKey>, IBaseUser
        where TRole : IdentityRole<TKey>
        where TKey : IEquatable<TKey>
    {

        protected GenericIdentityDbContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // 針對實作 IBaseUser 的實體套用共用設定
            builder.Entity<TUser>(entity =>
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
