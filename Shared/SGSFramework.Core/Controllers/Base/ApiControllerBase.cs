
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.ComponentModel;
using SGSFramework.Core.Identiies;
using SGSFramework.Core.Results;
using SGSFramework.Core.Errors;

namespace SGSFramework.Core.Controllers.Base
{

    /// <summary>
    /// 企業級 API 控制器基底抽象類別
    /// 內建基於結果模式 (Result Pattern) 的回應流轉機制與強型別用戶資安上下文感知 (Claims Context Awareness)
    /// </summary>
    /// <summary>
    /// 企業級 API 控制器基底抽象類別
    /// 內建基於結果模式 (Result Pattern) 的回應流轉機制與高效能用戶資安上下文快照快取
    /// </summary>
    [ApiController]
    [Route("v1/api/[controller]")] // 統一在此處定義版本與動態 Token， 請求路徑自動綁定控制器名稱，例如：api/[controller]/[action]
    public abstract class ApiControllerBase : ControllerBase
    {
        private UserContextSnapshot? _userInfo;

        /// <summary>
        /// 延遲載入並快取當前請求的強型別環境快照
        /// 確保單次請求內不論讀取幾次，皆僅解析一次 Claims 與 Headers，大幅提升高併發吞吐量
        /// </summary>
        protected UserContextSnapshot UserInfo => _userInfo ??= new UserContextSnapshot(
            UserId: User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "ANONYMOUS",
            Username: User.Identity?.Name ?? "System",
            DeviceId: User.FindFirst("device_id")?.Value ?? "UNKNOWN_DEVICE",
            LaboratoryId: User.FindFirst("laboratory_id")?.Value ?? User.FindFirst("clabory_id")?.Value ?? "UNKNOWN_LAB",
            ClientIp: ResolveClientIp()
        );

        // 💡 衍生屬性直接穿透至快取好的 UserInfo，維持外部呼叫端代碼的相容性與極致效能
        protected string CurrentUserId => UserInfo.UserId;
        protected string CurrentUsername => UserInfo.Username;
        protected string CurrentDeviceId => UserInfo.DeviceId;
        protected string CurrentLaboratoryId => UserInfo.LaboratoryId;
        protected string CurrentClientIp => UserInfo.ClientIp;

        /// <summary>
        /// 處理無回傳值的泛型領域結果轉換
        /// </summary>
        protected ActionResult HandleResult(Result result)
        {
            ArgumentNullException.ThrowIfNull(result);
            return result.IsSuccess ? Ok() : Problem(result.Error);
        }

        /// <summary>
        /// 處理包含資料載荷 (Payload) 的泛型領域結果轉換
        /// </summary>
        protected ActionResult HandleResult<T>(Result<T> result)
        {
            ArgumentNullException.ThrowIfNull(result);
            return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
        }

        /// <summary>
        /// 將領域錯誤 (Domain Error) 標準化映射為符合 RFC 7231 / RFC 7235 規範的 ProblemDetails 結構
        /// </summary>
        private ActionResult Problem(Error error)
        {
            int statusCode = error.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status500InternalServerError
            };

            //使用 Property Pattern { Count: > 0 } 安全相容 IReadOnlyCollection
            if (error is ValidationError validationError && validationError.Errors is { Count: > 0 })
            {
                var validationDetails = new ValidationProblemDetails
                {
                    Status = statusCode,
                    Title = GetTitle(error.Type),
                    Type = GetTypeUri(error.Type),
                    Detail = error.Message,
                    Extensions = { { "errorCode", error.Code } }
                };

                //對齊架構中 Error 實體的核心欄位 (.Code 與 .Message)
                foreach (var err in validationError.Errors)
                {
                    string key = err.Code ?? "ValidationProperties";
                    string errorMessage = err.Message ?? "未預期的驗證錯誤。";

                    if (!validationDetails.Errors.TryGetValue(key, out var messages))
                    {
                        validationDetails.Errors.Add(key, [errorMessage]);
                    }
                    else
                    {
                        validationDetails.Errors[key] = [.. messages, errorMessage];
                    }
                }

                return new BadRequestObjectResult(validationDetails);
            }

            return ObjectResultProblem(error, statusCode);
        }

        private ActionResult ObjectResultProblem(Error error, int statusCode)
        {
            return new ObjectResult(new ProblemDetails
            {
                Status = statusCode,
                Title = GetTitle(error.Type),
                Type = GetTypeUri(error.Type),
                Detail = error.Message,
                Extensions = { { "errorCode", error.Code } } // 🔒 保持一致的結構輸出
            })
            {
                StatusCode = statusCode
            };
        }

        /// <summary>
        /// 網路層防禦：精準提取真實客戶端 IP
        /// </summary>
        private string ResolveClientIp()
        {
            try
            {
                if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedHeader))
                {
                    var ipList = forwardedHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (ipList.Length > 0)
                        return ipList[0].Trim(); // 取得最前端真實 Client IP
                }
                return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            }
            catch
            {
                return "0.0.0.0"; // 防禦性地邊界退避，避免日誌系統因網路底層 Exception 而崩潰
            }
        }

        private static string GetTitle(ErrorType errorType) =>
            errorType switch
            {
                ErrorType.Validation => "Validation Error",
                ErrorType.NotFound => "Not Found",
                ErrorType.Conflict => "Conflict",
                ErrorType.Unauthorized => "Unauthorized",
                _ => "Server Error"
            };

        private static string GetTypeUri(ErrorType errorType) =>
            errorType switch
            {
                ErrorType.Validation => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                ErrorType.NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                ErrorType.Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                ErrorType.Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
                _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            };
    }
}
