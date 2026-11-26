// 檔案路徑：src/SGSFramework.Core/Application/Features/Laboratories/Command/AssignUserLabCommand.cs

using MediatR;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Command
{
    /// <summary>
    /// 指派/新增單一使用者實驗室對應關係 Command
    /// </summary>
    public sealed record AssignUserLabCommand(
        Guid UserId,
        int LabId,
        Guid TenantLabId,
        LabAssignmentType AssignmentType,
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
        private readonly ORGDbContext _orgContext;

        // 台灣時區定義（相容跨平台 Windows / Linux）
        private static readonly TimeZoneInfo TaiwanTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Taipei Standard Time" : "Asia/Taipei");

        public AssignUserLabCommandHandler(DbContext context, ORGDbContext orgContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
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

            // 支援類別：人員主要實驗室與支援實驗室須屬同一實驗室別（區域實驗室的上層）
            if (request.AssignmentType == LabAssignmentType.Support)
            {
                var categoryCheck = await ValidateSupportCategoryAsync(request, cancellationToken);
                if (categoryCheck is not null) return categoryCheck;
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // 2. 查詢該使用者既有的對應記錄
                var existingMappings = await _context.Set<UserLabMapping>()
                    .Where(x => x.UserId == request.UserId)
                    .ToListAsync(cancellationToken);

                var targetMapping = existingMappings.FirstOrDefault(x => x.LabId == request.LabId);

                // 3. 處理台灣時區轉換與 Safe Nullable 日期解析
                // 使用 DateTime.SpecifyKind 將類型明確為 Unspecified，防止 JSON 反序列化產生的 Local/Utc 狀態引發 TimeZoneInfo 轉譯例外
                DateTime resolvedEffectiveDate = request.EffectiveDate.HasValue
                    ? ConvertToTaiwanUtc(request.EffectiveDate.Value)
                    : ConvertToTaiwanUtc(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TaiwanTimeZone));

                DateTime? resolvedExpiryDate = request.ExpiryDate.HasValue
                    ? ConvertToTaiwanUtc(request.ExpiryDate.Value)
                    : null;

                // 4. 判斷是否需要自動設為主要實驗室
                bool isSupport = request.AssignmentType == LabAssignmentType.Support;

                // 支援期間的「迄日（含）」涵蓋整天：失效時間設為迄日當天結束（其他類別維持原行為）
                if (isSupport && resolvedExpiryDate.HasValue)
                {
                    resolvedExpiryDate = resolvedExpiryDate.Value.AddDays(1).AddTicks(-1);
                }

                if (isSupport && !resolvedExpiryDate.HasValue)
                {
                    return Result.Failure<bool>(
                        Error.Validation("USERLAB_SUPPORT_EXPIRY_REQUIRED", "支援類別必須設定生效結束日。"));
                }

                if (isSupport && targetMapping is { IsActive: true, IsPrimary: true })
                {
                    return Result.Failure<bool>(
                        Error.Validation("USERLAB_SUPPORT_CANNOT_BE_PRIMARY", "該實驗室為使用者目前的主要實驗室，不可改為支援。請先指定其他主要實驗室。"));
                }

                bool hasAnyActivePrimary = existingMappings.Any(x => x.IsActive && x.IsPrimary);
                bool shouldBePrimary = !isSupport
                    && (request.AssignmentType == LabAssignmentType.Primary || !hasAnyActivePrimary);
                var resolvedType = isSupport
                    ? LabAssignmentType.Support
                    : shouldBePrimary ? LabAssignmentType.Primary : LabAssignmentType.Secondary;

                // 5. 若指定或自動判定為主要實驗室，需將既有其他主要實驗室降級為次要
                if (shouldBePrimary)
                {
                    var currentPrimary = existingMappings.FirstOrDefault(x => x.IsPrimary && x.LabId != request.LabId);
                    currentPrimary?.DemoteToSecondary(request.OperatorId);
                }

                // 6. 新增或更新對應資料
                if (targetMapping is null)
                {
                    var newMapping = UserLabMapping.Create(
                        userId: request.UserId,
                        labId: request.LabId,
                        tenantLabId: request.TenantLabId,
                        assignmentType: resolvedType,
                        jobTitle: request.JobTitle,
                        effectiveDate: resolvedEffectiveDate,
                        expiryDate: resolvedExpiryDate,
                        operatorId: request.OperatorId
                    );

                    await _context.Set<UserLabMapping>().AddAsync(newMapping, cancellationToken);
                }
                else
                {
                    targetMapping.UpdateDetails(
                        tenantLabId: request.TenantLabId,
                        assignmentType: resolvedType,
                        jobTitle: request.JobTitle,
                        effectiveDate: resolvedEffectiveDate,
                        expiryDate: resolvedExpiryDate,
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

        /// <summary>
        /// 支援實驗室檢核：人員須有啟用中的主要實驗室，且其所屬實驗室別需與支援實驗室所屬的實驗室別相同。
        /// 此規則於後端強制執行（不論由「實驗室支援」或「使用者管理」指派），前端的人員過濾僅為輔助。
        /// </summary>
        private async Task<Result<bool>?> ValidateSupportCategoryAsync(AssignUserLabCommand request, CancellationToken cancellationToken)
        {
            var homeLabId = await _context.Set<UserLabMapping>()
                .AsNoTracking()
                .Where(x => x.UserId == request.UserId && x.IsActive && x.AssignmentType == LabAssignmentType.Primary)
                .Select(x => (int?)x.LabId)
                .FirstOrDefaultAsync(cancellationToken);

            var ids = new List<int> { request.LabId };
            if (homeLabId.HasValue) ids.Add(homeLabId.Value);

            var organizations = await _orgContext.Organizations
                .AsNoTracking()
                .Include(o => o.Parent)
                .Where(o => ids.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, cancellationToken);

            // 與批次指派共用同一份規則 (LabSupportRules)
            return LabSupportRules.CheckCategory(homeLabId, request.LabId, organizations) is { } failure
                ? Result.Failure<bool>(Error.Validation(failure.Code, failure.Message))
                : null;
        }

        /// <summary>
        /// 將傳入的 DateTime 轉為 DateTimeKind.Unspecified 並去除非必要的時分秒後，安全轉譯為台灣時區對應的 UTC 時間
        /// </summary>
        private static DateTime ConvertToTaiwanUtc(DateTime inputDate)
        {
            var unspecifiedDt = DateTime.SpecifyKind(inputDate.Date, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecifiedDt, TaiwanTimeZone);
        }
    }
}