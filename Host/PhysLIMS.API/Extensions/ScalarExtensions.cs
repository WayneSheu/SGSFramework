using Scalar.AspNetCore;

namespace PhysLIMS.API.Extensions
{
    public static class ScalarExtensions
    {
        public static IEndpointConventionBuilder MapCustomScalarApiReference(this IEndpointRouteBuilder endpoints)
        {
            return endpoints.MapScalarApiReference(options =>
            {
                options
                    .WithTitle("PhysLIMS API Document")
                    .WithTheme(ScalarTheme.Moon)
                    // 停用用戶端程式碼範例生成或依需求調整
                    .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            });
        }
    }
}
