// ==========================================
// 檔案路徑: src/SGSFramework/Infrastructure/SGSFramework.Security/Extensions/JwtBearerAuthenticationExtensions.cs
// 架構層級: Infrastructure / Security Layer
// ==========================================

namespace SGSFramework.Security.Extensions;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;
using System.Threading.Tasks;

public static class JwtBearerAuthenticationExtensions
{
    public static IServiceCollection AddConfiguredJwtBearerAuthentication(
        this IServiceCollection services,
        string issuer,
        string audience,
        string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    try
                    {
                        // 處理 Scalar UI 傳送 Authorization Header 缺乏 Bearer 前綴之容錯機制
                        string? authHeader = context.Request.Headers["Authorization"].ToString();
                        if (!string.IsNullOrWhiteSpace(authHeader) && !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Token = authHeader.Trim();
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogError(ex, "[JWT-AUTH] 解析 Authorization 標頭時發生例外");
                    }
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogWarning("[JWT-AUTH-FAIL] JWT 身份驗證失敗: {Message}, Token: {Token}",
                        context.Exception.Message,
                        context.Request.Headers["Authorization"]);
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogWarning("[JWT-AUTH-CHALLENGE] 觸發 401 Unauthorized 挑戰，路徑: {Path}", context.Request.Path);
                    return Task.CompletedTask;
                }
            };
        });

        return services;
    }
}