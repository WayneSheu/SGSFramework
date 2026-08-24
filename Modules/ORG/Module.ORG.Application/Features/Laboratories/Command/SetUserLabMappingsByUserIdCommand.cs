using MediatR;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Command
{
    /// <summary>
    /// 批次設定/覆蓋指定使用者的實驗室對應關係 Command
    /// </summary>
    public sealed record SetUserLabMappingsByUserIdCommand(
        Guid UserId,
        List<UserLabAssignmentDto> Assignments,
        string? OperatorId = null
    ) : IRequest<Result<bool>>;

    /// <summary>
    /// SetUserLabMappingsByUserIdCommand 處理常式
    /// 依循 Clean Architecture 與 .NET 10 規範，確保主要實驗室唯一性、顯式 Null 檢查與資料庫 Transaction 交易安全
    /// </summary>
    public sealed class SetUserLabMappingsByUserIdCommandHandler
        : IRequestHandler<SetUserLabMappingsByUserIdCommand, Result<bool>>
    {
        private readonly DbContext _context;

        public SetUserLabMappingsByUserIdCommandHandler(DbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Result<bool>> Handle(
            SetUserLabMappingsByUserIdCommand request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.UserId == Guid.Empty)
            {
                return Result.Failure<bool>(
                    Error.Validation("USERLAB_INVALID_USERID", "使用者識別碼不能為 Empty Guid。"));
            }

            var assignments = request.Assignments ?? new List<UserLabAssignmentDto>();

            // 1. 業務規則驗證：傳入的對應清單中，最多只能有一個設定為 Primary
            var primaryCount = assignments.Count(a => a.IsPrimary);
            if (primaryCount > 1)
            {
                return Result.Failure<bool>(
                    Error.Validation("USERLAB_MULTIPLE_PRIMARY", "同一位使用者最多只能設定一個主要實驗室。"));
            }

            // 2. 業務規則驗證：實驗室識別碼重複性檢查
            var duplicateLabIds = assignments
                .GroupBy(a => a.LabId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateLabIds.Count != 0)
            {
                return Result.Failure<bool>(
                    Error.Validation("USERLAB_DUPLICATE_LABS", $"對應設定中包含重複的實驗室識別碼：{string.Join(", ", duplicateLabIds)}"));
            }

            // 開啟資料庫交易處理 (DbContext 級別 Sync/Set 覆蓋)
            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // 3. 查詢既有的對應記錄
                var existingMappings = await _context.Set<UserLabMapping>()
                    .Where(x => x.UserId == request.UserId)
                    .ToListAsync(cancellationToken);

                var inputLabIds = assignments.Select(a => a.LabId).ToHashSet();
                var now = DateTime.UtcNow;

                // 4. 處理刪除：既有資料不在輸入清單中，且目前為啟用狀態者進行停用/刪除處理
                foreach (var existing in existingMappings)
                {
                    if (!inputLabIds.Contains(existing.LabId))
                    {
                        _context.Set<UserLabMapping>().Remove(existing);
                    }
                }

                // 5. 處理新增與更新 (Sync 邏輯優化)
                foreach (var dto in assignments)
                {
                    var existing = existingMappings.FirstOrDefault(x => x.LabId == dto.LabId);

                    if (existing is null)
                    {
                        // 呼叫 Domain 工廠建立封裝好的 Entity
                        var newMapping = UserLabMapping.Create(
                            userId: request.UserId,
                            labId: dto.LabId,
                            tenantLabId: dto.TenantLabId,
                            isPrimary: dto.IsPrimary,
                            jobTitle: dto.JobTitle,
                            effectiveDate: dto.EffectiveDate,
                            expiryDate: dto.ExpiryDate,
                            operatorId: request.OperatorId
                        );

                        await _context.Set<UserLabMapping>().AddAsync(newMapping, cancellationToken);
                    }
                    else
                    {
                        // 透過 Domain 方法更新狀態，簡化程式碼並利用 Change Tracker 自動追蹤變更
                        existing.UpdateDetails(
                            tenantLabId: dto.TenantLabId,
                            isPrimary: dto.IsPrimary,
                            jobTitle: dto.JobTitle,
                            effectiveDate: dto.EffectiveDate,
                            expiryDate: dto.ExpiryDate,
                            operatorId: request.OperatorId
                        );

                        // 註：EF Core 會自動追蹤已載入實體的 Property 修改，無需再顯式呼叫 _context.Set<UserLabMapping>().Update(existing)
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return Result.Success(true);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<bool>(
                    Error.Failure("USERLAB_SET_DB_ERROR", $"更新使用者實驗室對應資料庫失敗，違反限制約束：{ex.Message}"));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<bool>(
                    Error.Unexpected("USERLAB_SET_ERROR", $"執行批次設定使用者實驗室關聯時發生未預期錯誤：{ex.Message}"));
            }
        }
    }

}
