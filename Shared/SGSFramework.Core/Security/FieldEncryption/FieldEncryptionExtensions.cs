using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.Core.Abstractions.Attributes;
using System.Reflection;

namespace SGSFramework.Core.Security.FieldEncryption
{
    public static class FieldEncryptionExtensions
    {
        // 共用單一 Converter 實例；Lambda 僅於讀寫時呼叫 FieldEncryptor，設計期 (Migration) 不會觸發加解密
        private static readonly ValueConverter<string, string> EncryptedConverter =
            new(v => FieldEncryptor.Protect(v), v => FieldEncryptor.Unprotect(v));

        /// <summary>
        /// 掃描模型中所有標註 [Encrypted] 的字串屬性並掛上加解密 ValueConverter。
        /// 請於 OnModelCreating 最後呼叫 (可重複呼叫)。
        /// </summary>
        public static ModelBuilder ApplyEncryptedProperties(this ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType != typeof(string)) continue;

                    var member = (MemberInfo?)property.PropertyInfo ?? property.FieldInfo;
                    if (member?.IsDefined(typeof(EncryptedAttribute), inherit: true) == true)
                    {
                        property.SetValueConverter(EncryptedConverter);
                    }
                }
            }

            return modelBuilder;
        }

        /// <summary>
        /// Fluent API 版本：用於無法加上 Attribute 的框架屬性 (例如 IdentityUser.PhoneNumber)。
        /// </summary>
        public static PropertyBuilder<string?> IsEncrypted(this PropertyBuilder<string?> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Metadata.SetValueConverter(EncryptedConverter);
            return builder;
        }

        /// <summary>
        /// 判斷屬性是否為落地加密欄位 (供稽核等機制避免把明文另存他處)
        /// </summary>
        public static bool IsEncryptedProperty(this IReadOnlyProperty property)
        {
            ArgumentNullException.ThrowIfNull(property);
            return ReferenceEquals(property.GetValueConverter(), EncryptedConverter)
                || property.PropertyInfo?.IsDefined(typeof(EncryptedAttribute), inherit: true) == true;
        }

        /// <summary>
        /// 綁定 DI 中的 Data Protection 金鑰環至 <see cref="FieldEncryptor"/>。
        /// 必須於 builder.Build() 之後、任何資料庫存取 (Seeder / Migration) 之前呼叫。
        /// </summary>
        public static WebApplication UseFieldEncryption(this WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);
            FieldEncryptor.Initialize(app.Services.GetRequiredService<IDataProtectionProvider>());
            return app;
        }
    }
}
