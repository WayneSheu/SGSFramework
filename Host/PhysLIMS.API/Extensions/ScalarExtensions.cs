// Path: src/SGSFramework/Host/PhysLIMS.API/Extensions/ScalarExtensions.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Scalar.AspNetCore;

namespace PhysLIMS.API.Extensions;

public static class ScalarExtensions
{
    public static IEndpointConventionBuilder MapCustomScalarApiReference(this IEndpointRouteBuilder endpoints)
    {
        // 統一使用 "/scalar" 作為路由字首
        return endpoints.MapScalarApiReference("/scalar", options =>
        {
            options
                .WithTitle("PhysLIMS 2.0 API Documentation")
                .WithTheme(ScalarTheme.Solarized)
                .WithLayout(ScalarLayout.Modern)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);

            // [修正] 改用 AddDocument 正確對應多版本 OpenAPI 文件端點，避免誤用 AddServer 造成路徑 404
            options.AddDocument("v1", "v1 API", "/openapi/v1.json")
                   .AddDocument("v2", "v2 API", "/openapi/v2.json");

            // 整合 JWT 身分驗證配置
            options.Authentication = new ScalarAuthenticationOptions
            {
                PreferredSecurityScheme = JwtBearerDefaults.AuthenticationScheme
            };
        });
    }
}