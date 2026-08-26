namespace SGS.Modules.ORG.Contracts.Queries;

using MediatR;
using SGSFramework.Core.DTOs;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;

public sealed record GetAccessibleLaboratoriesQuery(string UserId) : IRequest<Result<List<AccessibleLabDto>>>
{
    /// <summary>
    /// 驗證 Query 參數合法性
    /// 導入 MediatR Pipeline Behavior + FluentValidation 後 此Validate() 可移除
    /// </summary>
    public Result Validate()
    {
        if (string.IsNullOrWhiteSpace(UserId))
        {
            return Result.Failure(
                Error.Validation("ORG_USER_ID_REQUIRED", "使用者識別碼不可為空。"));
        }

        if (!Guid.TryParse(UserId, out _))
        {
            return Result.Failure(
                Error.Validation("ORG_INVALID_USER_ID_FORMAT", "使用者識別碼格式不正確 (需為 Guid)。"));
        }

        return Result.Success();
    }


};

