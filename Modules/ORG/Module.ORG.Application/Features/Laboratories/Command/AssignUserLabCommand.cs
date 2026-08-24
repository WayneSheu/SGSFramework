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
    /// 指派/新增單一使用者實驗室對應關係 Command
    /// </summary>
    public sealed record AssignUserLabCommand(
        Guid UserId,
        int LabId,
        Guid TenantLabId,
        bool IsPrimary,
        string? JobTitle = null,
        DateTime? EffectiveDate = null,
        DateTime? ExpiryDate = null,
        string? OperatorId = null
    ) : IRequest<Result<bool>>;

    /// <summary>
    /// AssignUserLabCommand 處理常式
    /// 遵循 Clean Architecture 與 .NET 10 規範，確保主要實驗室維護與充血模型封裝
    /// </summary>
    public sealed class AssignUserLabCommandHandler : IRequestHandler<AssignUserLabCommand, Result<bool>>
    {
        private readonly DbContext _context;

        public AssignUserLabCommandHandler(DbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Result<bool>> Handle(AssignUserLabCommand request, CancellationToken cancellationToken)
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

            if (request.TenantLabId == Guid.Empty)
            {
                return Result.Failure<bool>(
                    Error.Validation("USERLAB_INVALID_TENANT_LABID", "租戶實驗室識別碼不能為 Empty Guid。"));
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // 2. 查詢該使用者既有的對應記錄
                var existingMappings = await _context.Set<UserLabMapping>()
                    .Where(x => x.UserId == request.UserId)
                    .ToListAsync(cancellationToken);

                var targetMapping = existingMappings.FirstOrDefault(x => x.LabId == request.LabId);

                // 3. 若指定為主要實驗室 (IsPrimary = true)，需將既有主要實驗室降級為次要
                if (request.IsPrimary)
                {
                    var currentPrimary = existingMappings.FirstOrDefault(x => x.IsPrimary && x.LabId != request.LabId);
                    currentPrimary?.DemoteToSecondary(request.OperatorId);
                }

                // 4. 新增或更新對應資料
                if (targetMapping is null)
                {
                    // 使用 Domain 實體工廠建立新關聯
                    var newMapping = UserLabMapping.Create(
                        userId: request.UserId,
                        labId: request.LabId,
                        tenantLabId: request.TenantLabId,
                        isPrimary: request.IsPrimary,
                        jobTitle: request.JobTitle,
                        effectiveDate: request.EffectiveDate,
                        expiryDate: request.ExpiryDate,
                        operatorId: request.OperatorId
                    );

                    await _context.Set<UserLabMapping>().AddAsync(newMapping, cancellationToken);
                }
                else
                {
                    // 呼叫 Domain 充血模型方法更新細節
                    targetMapping.UpdateDetails(
                        tenantLabId: request.TenantLabId,
                        isPrimary: request.IsPrimary,
                        jobTitle: request.JobTitle,
                        effectiveDate: request.EffectiveDate ?? targetMapping.EffectiveDate,
                        expiryDate: request.ExpiryDate,
                        operatorId: request.OperatorId
                    );
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result.Success(true);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<bool>(
                    Error.Unexpected("USERLAB_ASSIGN_DB_ERROR", $"指派使用者實驗室資料庫儲存失敗，違反約束條件：{ex.Message}"));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<bool>(
                    Error.Unexpected("USERLAB_ASSIGN_ERROR", $"執行指派使用者實驗室時發生未預期錯誤：{ex.Message}"));
            }
        }
    }
}
