using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace PhysLIMS.API.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// 根據執行環境 (IIS 或 Kestrel) 安全配置身分驗證，並加入 JWT 容錯清洗機制。
    /// </summary>
    public static IServiceCollection AddCustomAuthentication(this IServiceCollection services, bool isIisHosted)
    {
        // 啟用 PII 顯示，方便排查 IDX14102 等 JWT 解析失敗時的原始字串內容
        Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;

        if (isIisHosted)
        {
            // =========================================================================
            // 【IIS 託管模式】
            // =========================================================================
            var authBuilder = services.AddAuthentication(options =>
            {
                // 為了讓 Blazor WASM 正常運作，預設驗證與挑戰仍必須是 Bearer，不可是 IISDefaults
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
             
            });

            // 採用防禦性擴充，避免重複註冊 Bearer Scheme
            services.Configure<AuthenticationOptions>(options =>
            {
                if (!options.Schemes.Any(s => s.Name == JwtBearerDefaults.AuthenticationScheme))
                {
                    authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        options.Authority = null;
                        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                        {
                            ValidateIssuer = false,
                            ValidateAudience = false,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true
                        };
                    });
                }
            });

            services.Configure<IISServerOptions>(options =>
            {
                options.AutomaticAuthentication = true;
            });
        }
        else
        {
            // =========================================================================
            // 【Kestrel 獨立執行模式】
            // =========================================================================
            var authBuilder = services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            });

            services.Configure<AuthenticationOptions>(options =>
            {
                if (!options.Schemes.Any(s => s.Name == JwtBearerDefaults.AuthenticationScheme))
                {
                    authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        // Kestrel 下的 JWT 設定
                    });
                }
            });

            var isNegotiateRegistered = services.Any(sd =>
                sd.ImplementationType == typeof(NegotiateHandler) ||
                sd.ServiceType == typeof(NegotiateHandler));

            if (!isNegotiateRegistered)
            {
                authBuilder.AddNegotiate();
            }
        }

        // 註冊 JWT Token 防呆攔截機制，修正使用者或前端 UI 造成的格式錯誤
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var originalOnMessageReceived = options.Events?.OnMessageReceived;

            if (options.Events == null)
            {
                options.Events = new JwtBearerEvents();
            }

            options.Events.OnMessageReceived = async context =>
            {
                try
                {
                    if (originalOnMessageReceived != null)
                    {
                        await originalOnMessageReceived(context).ConfigureAwait(false);
                    }

                    string? token = context.Token;

                    if (string.IsNullOrWhiteSpace(token))
                    {
                        var authHeader = context.Request.Headers.Authorization.ToString();
                        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            token = authHeader.Substring("Bearer ".Length).Trim();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            token = token.Substring("Bearer ".Length).Trim();
                        }

                        token = token.Trim('"');
                        context.Token = token;
                    }
                }
                catch (Exception ex)
                {
                    var logger = context.HttpContext.RequestServices.GetService<ILogger<JwtBearerEvents>>();
                    logger?.LogWarning(ex, "JWT Token 預處理清洗時發生例外錯誤。");
                }
            };
        });

        return services;
    }
}