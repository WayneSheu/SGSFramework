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
    /// 設定/切換使用者主要實驗室 Command
    /// </summary>
    public sealed record SetPrimaryLabCommand(
        Guid UserId,
        int LabId,
        string? OperatorId = null
    ) : IRequest<Result<bool>>;

    /// <summary>
    /// SetPrimaryLabCommand 處理常式
    /// 遵循 Clean Architecture 與 .NET 10 規範，維護主要實驗室切換機制與充血模型封裝
    /// </summary>
    public sealed class SetPrimaryLabCommandHandler : IRequestHandler<SetPrimaryLabCommand, Result<bool>>
    {
        private readonly DbContext _context;

        public SetPrimaryLabCommandHandler(DbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Result<bool>> Handle(SetPrimaryLabCommand request, CancellationToken cancellationToken)
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
                // 2. 查詢該使用者所有已指派的實驗室對應記錄
                var userLabMappings = await _context.Set<UserLabMapping>()
                    .Where(x => x.UserId == request.UserId && x.IsActive)
                    .ToListAsync(cancellationToken);

                // 3. 檢查目標實驗室是否在該使用者的可存取權限清單中
                var targetLabMapping = userLabMappings.FirstOrDefault(x => x.LabId == request.LabId);
                if (targetLabMapping is null)
                {
                    return Result.Failure<bool>(
                        Error.NotFound(
                            "USERLAB_NOT_FOUND",
                            $"目標實驗室 (ID: {request.LabId}) 未指派給該使用者，無法提升為主要實驗室。"));
                }

                // 4. 若目標已經是主要實驗室，直接回傳成功 (冪等性處理)
                if (targetLabMapping.IsPrimary)
                {
                    return Result.Success(true);
                }

                // 5. 將既有主要實驗室降級為次要 (Demote)
                var currentPrimary = userLabMappings.FirstOrDefault(x => x.IsPrimary);
                currentPrimary?.DemoteToSecondary(request.OperatorId);

                // 6. 將目標實驗室升級為主要 (Promote)
                targetLabMapping.PromoteToPrimary(request.OperatorId);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result.Success(true);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<bool>(
                    Error.Unexpected(
                        "USERLAB_SET_PRIMARY_DB_ERROR",
                        $"設定主要實驗室時發生資料庫寫入錯誤，違反約束條件：{ex.Message}"));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<bool>(
                    Error.Unexpected(
                        "USERLAB_SET_PRIMARY_ERROR",
                        $"執行設定主要實驗室時發生未預期錯誤：{ex.Message}"));
            }
        }
    }
}
