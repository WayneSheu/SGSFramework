using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PhysLIMS.API.Extensions;

/// <summary>
/// 「用戶操作手冊專用」OpenAPI 文件（/openapi/user-manual.json）：供操作手冊管理（MAN）的 AI 生成讀取欄位驗證規則與錯誤說明。
/// 與完整的 v1 文件隔離：排除管理、系統、驗證與手冊生成器本身的端點，避免內部資料結構進入 AI 上下文與對外手冊。
/// </summary>
public static partial class UserManualOpenApiExtensions
{
    public const string DocumentName = "user-manual";

    /// <summary>
    /// 路徑第三段（/api/v{n}/{segment}/…）命中下列任一即排除：管理、治理、安全、基礎設施與手冊生成器本身。
    /// 此清單是「最低限度」的保護，新增管理性質的模組時請一併補上；終端用戶的業務模組（pms、qtn、reports…）則保留。
    /// </summary>
    private static readonly HashSet<string> ExcludedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "internal", "man", "auth", "system", "system-accounts", "system-settings",
        "access-reviews", "data-protections", "database-maintenance", "database-schemas", "function-managements",
        "identity-security", "lab-audits", "lab-function-scopes", "ledger", "log-rotation", "menu-items",
        "permissions", "role-governance", "roles", "scheduler", "spa-modules", "telemetry", "users"
    };

    [GeneratedRegex(@"^/api/v\d+(?:\.\d+)?/(?<segment>[^/]+)")]
    private static partial Regex ApiPathRegex();

    /// <summary>判斷路徑是否應自用戶手冊文件排除（純函式，便於測試）。</summary>
    public static bool IsExcludedPath(string path)
    {
        var match = ApiPathRegex().Match(path ?? string.Empty);
        if (!match.Success) return false;

        var segment = match.Groups["segment"].Value;
        return ExcludedSegments.Contains(segment) || segment.StartsWith("system-", StringComparison.OrdinalIgnoreCase);
    }

    public static IServiceCollection AddUserManualOpenApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOpenApi(DocumentName, options =>
        {
            // 端點的 ApiExplorer 群組名稱是 API 版本（v1），不是文件名稱；須與 v1 文件相同，明確收錄全部後再自行排除敏感路徑
            options.ShouldInclude = _ => true;

            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "PhysLIMS 用戶功能操作 API 規格",
                    Version = "v1",
                    Description = "僅包含終端用戶日常操作之功能端點與資料驗證規則（已排除管理、系統與驗證端點）"
                };

                if (document.Paths is { } paths)
                {
                    foreach (var key in paths.Keys.Where(IsExcludedPath).ToList())
                    {
                        paths.Remove(key);
                    }
                }

                // 資料模型僅保留名稱不含管理／內部／手冊生成器字樣者；殘留的 $ref 對只讀 JSON 的消費者無害
                if (document.Components?.Schemas is { } schemas)
                {
                    foreach (var key in schemas.Keys
                                 .Where(k => k.Contains("Admin", StringComparison.OrdinalIgnoreCase)
                                             || k.Contains("Internal", StringComparison.OrdinalIgnoreCase)
                                             || k.Contains("Generation", StringComparison.OrdinalIgnoreCase)
                                             || k.Contains("Login", StringComparison.OrdinalIgnoreCase)
                                             || k.Contains("Token", StringComparison.OrdinalIgnoreCase))
                                 .ToList())
                    {
                        schemas.Remove(key);
                    }
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
