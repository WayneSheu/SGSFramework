// ==========================================
// 檔案路徑: src/SGSFramework/Host/PhysLIMS.Controller/Filters/OpenApiSecurityRequirementTransformer.cs
// 架構層級: Presentation / Controller Layer
// ==========================================

namespace SGSFramework.ApiInfrastructure.Filters;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 為 OpenAPI 操作端點注入全域 Bearer Token 安全性需求，確保 Scalar UI 自動夾帶 Authorization Header。
/// 相容於 .NET 10 / OpenAPI.NET 物件模型結構。
/// </summary>
public sealed class OpenApiSecurityRequirementTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeName = "Bearer";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            // 1. 初始化 Components 物件
            document.Components ??= new OpenApiComponents();

            // 2. 解決 CS0266 錯誤：改用介面型別 IOpenApiSecurityScheme 進行泛型實體化
            if (document.Components.SecuritySchemes == null)
            {
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>();
            }

            // 3. 檢查並補全 Bearer SecurityScheme 定義
            if (!document.Components.SecuritySchemes.ContainsKey(SchemeName))
            {
                document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "請輸入 JWT Bearer Token 以取得存取權限"
                };
            }

            // 4. 建立 OpenApiSecuritySchemeReference 參照
            var schemeReference = new OpenApiSecuritySchemeReference(SchemeName, document);

            var securityRequirement = new OpenApiSecurityRequirement
            {
                [schemeReference] = new List<string>()
            };

            // 5. 綁定至 OpenApiDocument.Security 集合
            document.Security ??= new List<OpenApiSecurityRequirement>();
            document.Security.Add(securityRequirement);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("注入全域 OpenAPI Security Requirements 時發生錯誤。", ex);
        }

        return Task.CompletedTask;
    }
}