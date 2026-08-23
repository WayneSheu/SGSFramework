using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace SGSFramework.Persistent.Extensions
{
    /// <summary>
    /// 自行編輯的IDbContextOptionsExtension，負責把schema字符串塞進DbContextOptions的增強槽
    /// Provides an Entity Framework Core options extension for configuring toolkit-specific settings, such as the
    /// default database schema.
    /// </summary>
    /// <remarks>This extension enables customization of EF Core DbContext behavior by allowing
    /// toolkit-related options to be specified. It is typically used internally by the toolkit to register services and
    /// manage schema configuration. The extension is automatically recognized by EF Core when added to the DbContext
    /// options builder.</remarks>
    public class ToolkitOptionsExtension : IDbContextOptionsExtension
    {
        public string? Schema { get; init; }

        // EF Core 要求實作，用來描述這個擴充（出現在 DbContext 的 debug 資訊）
        public DbContextOptionsExtensionInfo Info => new ExtensionInfo(this);

        // EF Core 呼叫此方法將擴充的服務注入 DI
        public void ApplyServices(IServiceCollection services) { }

        // EF Core 呼叫此方法做 options 驗證（可留空）
        public void Validate(IDbContextOptions options) { }

        private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            public ExtensionInfo(IDbContextOptionsExtension extension)
                : base(extension) { }

            // 是否影響 DbContext 的 pool key（不同 schema 應視為不同 pool）
            public override bool IsDatabaseProvider => false;

            public override string LogFragment =>
                $"Schema={((ToolkitOptionsExtension)Extension).Schema}";

            public override int GetServiceProviderHashCode() =>
                ((ToolkitOptionsExtension)Extension).Schema?.GetHashCode() ?? 0;

            public override bool ShouldUseSameServiceProvider(
                DbContextOptionsExtensionInfo other) =>
                other is ExtensionInfo o &&
                o.GetServiceProviderHashCode() == GetServiceProviderHashCode();

            public override void PopulateDebugInfo(
                IDictionary<string, string> debugInfo) =>
                debugInfo["Toolkit:Schema"] =
                    ((ToolkitOptionsExtension)Extension).Schema ?? "core";
        }
    }
}
