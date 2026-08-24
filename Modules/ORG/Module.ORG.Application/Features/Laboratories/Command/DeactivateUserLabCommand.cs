using MediatR;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Command
{

    /// <summary>
    /// 停用指定使用者與實驗室對應關係 Command
    /// </summary>
    public sealed record DeactivateUserLabCommand(
        Guid UserId,
        int LabId,
        string? OperatorId = null
    ) : IRequest<Result<bool>>;

    /// <summary>
    /// DeactivateUserLabCommand 處理常式
    /// 遵循 Clean Architecture 與 .NET 10 規範，維護主要實驗室安全停用機制與充血模型封裝
    /// </summary>
    public sealed class DeactivateUserLabCommandHandler : IRequestHandler<DeactivateUserLabCommand, Result<bool>>
    {
        private readonly DbContext _context;

        public DeactivateUserLabCommandHandler(DbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Result<bool>> Handle(DeactivateUserLabCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // 1. 基礎參數驗證
            if (request.UserId == Guid.Empty)
            {
                return Result.Failure<bool>(
                    Error.Validation("USERLAB_INVALID_USERID", "使用者識別碼不能為 Empty Guid。"));
            }

            if (request.LabId <= 0)
            {
                return Result.Failure<bool>(
                    Error.Validation("USERLAB_INVALID_LABID", "實驗室識別碼必須大於 0。"));
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // 2. 查詢該使用者指定的實驗室對應記錄
                var targetMapping = await _context.Set<UserLabMapping>()
                    .FirstOrDefaultAsync(
                        x => x.UserId == request.UserId && x.LabId == request.LabId,
                        cancellationToken);

                if (targetMapping is null)
                {
                    return Result.Failure<bool>(
                        Error.NotFound(
                            "USERLAB_NOT_FOUND",
                            $"找不到使用者 (ID: {request.UserId}) 與實驗室 (ID: {request.LabId}) 的對應記錄。"));
                }

                // 3. 業務安全保護：若欲停用的實驗室為「主要實驗室 (IsPrimary)」，禁止直接停用，需先指定新的主要實驗室
                if (targetMapping.IsPrimary && targetMapping.IsActive)
                {
                    return Result.Failure<bool>(
                        Error.Failure(
                            "USERLAB_DEACTIVATE_PRIMARY_FORBIDDEN",
                            "無法直接停用主要實驗室。請先將其他實驗室設為主要實驗室後再執行停用。"));
                }

                // 4. 若已經是停用狀態，直接回傳成功 (冪等性處理)
                if (!targetMapping.IsActive)
                {
                    return Result.Success(true);
                }

                // 5. 呼叫 Domain 充血模型方法執行停用
                targetMapping.Deactivate(request.OperatorId);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result.Success(true);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<bool>(
                    Error.Unexpected(
                        "USERLAB_DEACTIVATE_DB_ERROR",
                        $"停用使用者實驗室對應時發生資料庫寫入錯誤：{ex.Message}"));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<bool>(
                    Error.Unexpected(
                        "USERLAB_DEACTIVATE_ERROR",
                        $"執行停用使用者實驗室對應時發生未預期錯誤：{ex.Message}"));
            }
        }
    }
}
