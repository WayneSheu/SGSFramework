// Path: src/SGSFramework/Shared/SGSFramework.Core/Configurations/OptionsRegistrationExtensions.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace SGSFramework.Core.Configurations
{
    /// <summary>
    /// 系統配置管理：統一的強型別 Options 註冊與驗證擴充方法。
    /// <para>
    /// 執行期 (DI 注入) 請使用 <see cref="AddValidatedOptions{TOptions}"/>：綁定 → DataAnnotations 驗證 → 啟動時驗證 (Fail-Fast)。
    /// 建置期 (builder.Build() 之前需要讀值) 請使用 <see cref="GetValidatedOptions{TOptions}"/>，避免以魔術字串直接讀取 IConfiguration。
    /// </para>
    /// </summary>
    public static class OptionsRegistrationExtensions
    {
        /// <summary>
        /// 綁定指定組態區段至 <typeparamref name="TOptions"/>，並啟用 DataAnnotations 與啟動時驗證。
        /// </summary>
        /// <param name="services">DI 服務集合</param>
        /// <param name="sectionPath">組態區段路徑 (例如 "PersistentSettings:ConnectionStrings")</param>
        /// <returns>OptionsBuilder，可再串接 .Validate(...) 追加自訂規則</returns>
        public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(
            this IServiceCollection services,
            string sectionPath)
            where TOptions : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

            return services.AddOptions<TOptions>()
                .BindConfiguration(sectionPath)
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }

        /// <summary>
        /// 於建置期直接取得並驗證強型別組態 (DI 容器尚未建立時使用)。
        /// 區段不存在時回傳預設值實例後再進行驗證，驗證失敗則拋出 <see cref="OptionsValidationException"/>。
        /// </summary>
        public static TOptions GetValidatedOptions<TOptions>(
            this IConfiguration configuration,
            string sectionPath)
            where TOptions : class, new()
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentException.ThrowIfNullOrWhiteSpace(sectionPath);

            var options = configuration.GetSection(sectionPath).Get<TOptions>() ?? new TOptions();

            var results = new List<ValidationResult>();
            if (!Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true))
            {
                var failures = results.Select(r =>
                    $"[{sectionPath}] {string.Join(",", r.MemberNames)}: {r.ErrorMessage}");

                throw new OptionsValidationException(sectionPath, typeof(TOptions), failures);
            }

            return options;
        }
    }
}
