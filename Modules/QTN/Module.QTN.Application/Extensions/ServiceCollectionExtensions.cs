using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGS.Modules.QTN.Application.Services;

namespace SGS.Modules.QTN.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>註冊 QTN Application 層：主檔、商機、報價（估時／定價／審批／版本）、附件、PDF、CR 評估、結案復盤、儀表板。</summary>
    public static IServiceCollection AddModuleQtnApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<QtnAccess>();
        services.AddScoped<QtnMailNotifier>();
        services.AddScoped<MasterDataService>();
        services.AddScoped<OpportunityService>();
        services.AddScoped<QuoteService>();
        services.AddScoped<QuoteAttachmentService>();
        services.AddScoped<QuoteExportService>();
        services.AddScoped<ChangeAssessmentService>();
        services.AddScoped<VarianceReviewService>();
        services.AddScoped<QuoteDashboardService>();
        return services;
    }
}
