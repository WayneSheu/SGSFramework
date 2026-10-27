using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.Tracing;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;

namespace SGSFramework.ApiInfrastructure.Controllers.V1;

/// <summary>
/// 系統帳號權杖端點（OAuth 2.0 Client Credentials Grant）：服務以 client_id + client_secret 換取短效 JWT。
/// 與管理 API 分屬不同功能，避免停用管理頁功能時連帶中斷服務間通訊。
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("api/v{version:apiVersion}/system-accounts")]
[ControllerTitle("系統帳號權杖", Icon = "fa-solid fa-ticket", Order = 25, Description = "系統帳號以 Client Credentials 換發短效 JWT，並提供權杖自我檢查")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class SystemAccountTokenController : ApiControllerBase
{
    private readonly ISystemAccountService _systemAccountService;

    public SystemAccountTokenController(ISystemAccountService systemAccountService)
    {
        ArgumentNullException.ThrowIfNull(systemAccountService);
        _systemAccountService = systemAccountService;
    }

    /// <summary>
    /// 換發系統帳號權杖
    /// </summary>
    [HttpPost("token")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded", MediaTypeNames.Application.Json)]
    [Function("IssueSystemAccountToken", "換發系統帳號權杖", Icon = "fa-solid fa-ticket", Order = 1, Description = "以 client_id + client_secret 換發短效 JWT（OAuth 2.0 Client Credentials）")]
    [EndpointSummary("換發系統帳號權杖")]
    [EndpointDescription("grant_type=client_credentials；client_id / client_secret 可放在表單、JSON 或 HTTP Basic 標頭；scope 省略時核發全部已授予的操作範圍。")]
    [ProducesResponseType(typeof(SystemTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> IssueToken(CancellationToken cancellationToken = default)
    {
        var request = await ReadTokenRequestAsync(cancellationToken);
        if (request is null)
        {
            return OAuthError(StatusCodes.Status400BadRequest, "invalid_request", "請以 application/x-www-form-urlencoded 或 JSON 傳送權杖請求。");
        }

        var result = await _systemAccountService.IssueTokenAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        // RFC 6749 §5.1：權杖回應不得被快取
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";

        if (result.Succeeded)
        {
            return Ok(result.Token);
        }

        return OAuthError(
            result.Error == "invalid_client" ? StatusCodes.Status401Unauthorized : StatusCodes.Status400BadRequest,
            result.Error!,
            result.ErrorDescription);
    }

    /// <summary>
    /// 系統帳號權杖自我檢查
    /// </summary>
    [HttpGet("whoami")]
    [Authorize]
    [RequiresSystemScope]
    [Function("SystemAccountWhoAmI", "系統帳號自我檢查", Icon = "fa-solid fa-user-check", Order = 2, Description = "以系統帳號權杖呼叫，回傳帳號與操作範圍，供部署後驗證連線")]
    [EndpointSummary("系統帳號自我檢查")]
    [EndpointDescription("僅限系統帳號權杖呼叫；回傳權杖所代表的帳號、操作範圍與到期時間。")]
    [ProducesResponseType(typeof(SystemAccountWhoAmIDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public IActionResult WhoAmI()
    {
        var expiresClaim = User.FindFirst("exp")?.Value;
        DateTimeOffset? expiresAt = long.TryParse(expiresClaim, out var exp) ? DateTimeOffset.FromUnixTimeSeconds(exp) : null;

        return Ok(new SystemAccountWhoAmIDto
        {
            AccountId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            ClientId = User.FindFirst(SystemAccountClaims.ClientId)?.Value,
            DisplayName = User.FindFirst(SystemAccountClaims.DisplayName)?.Value,
            Scopes = User.GetScopes().OrderBy(s => s, StringComparer.Ordinal).ToList(),
            ExpiresAt = expiresAt,
            CorrelationId = CorrelationContext.Current
        });
    }

    /// <summary>
    /// 讀取權杖請求：表單（RFC 6749 標準）或 JSON；用戶端憑證亦可放在 HTTP Basic 標頭（RFC 6749 §2.3.1）
    /// </summary>
    private async Task<SystemTokenRequest?> ReadTokenRequestAsync(CancellationToken cancellationToken)
    {
        SystemTokenRequest? request;

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            request = new SystemTokenRequest
            {
                GrantType = form["grant_type"].ToString(),
                ClientId = form["client_id"].ToString(),
                ClientSecret = form["client_secret"].ToString(),
                Scope = form["scope"].ToString()
            };
        }
        else if (Request.HasJsonContentType())
        {
            try
            {
                request = await Request.ReadFromJsonAsync<SystemTokenRequest>(cancellationToken);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }
        else
        {
            return null;
        }

        if (request is not null &&
            AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header) &&
            string.Equals(header.Scheme, "Basic", StringComparison.OrdinalIgnoreCase) &&
            TryDecodeBasic(header.Parameter, out var basicId, out var basicSecret))
        {
            request.ClientId = basicId;
            request.ClientSecret = basicSecret;
        }

        return request;
    }

    private static bool TryDecodeBasic(string? parameter, out string clientId, out string clientSecret)
    {
        clientId = clientSecret = string.Empty;
        if (string.IsNullOrWhiteSpace(parameter))
        {
            return false;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(parameter));
            var separator = decoded.IndexOf(':');
            if (separator <= 0)
            {
                return false;
            }

            clientId = Uri.UnescapeDataString(decoded[..separator]);
            clientSecret = Uri.UnescapeDataString(decoded[(separator + 1)..]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>OAuth 2.0 錯誤回應（RFC 6749 §5.2）</summary>
    private ObjectResult OAuthError(int status, string error, string? description)
    {
        if (status == StatusCodes.Status401Unauthorized)
        {
            Response.Headers.WWWAuthenticate = "Basic realm=\"system-accounts\"";
        }

        return new ObjectResult(new Dictionary<string, string?>
        {
            ["error"] = error,
            ["error_description"] = description
        })
        {
            StatusCode = status
        };
    }
}
