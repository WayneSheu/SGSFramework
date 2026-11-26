using MediatR;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Abstractions.Entities.Identities;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Command
{
    /// <summary>批次支援的單筆明細</summary>
    /// <param name="EffectiveDate">支援起日（含）；null＝今天（台灣時區）</param>
    /// <param name="ExpiryDate">支援迄日（含），必填</param>
    public sealed record BatchLabSupportItem(
        Guid UserId, int LabId, Guid TenantLabId, string? JobTitle, DateTime? EffectiveDate, DateTime ExpiryDate);

    /// <summary>
    /// 批次新增 / 更新實驗室支援。DryRun＝只檢查不寫入；非 DryRun 為「全有全無」：任一筆不合格即整批不建立。
    /// </summary>
    public sealed record BatchAssignLabSupportCommand(
        IReadOnlyList<BatchLabSupportItem> Items, bool DryRun = false, string? OperatorId = null)
        : IRequest<LabSupportBatchResult>;

    /// <summary>單筆檢查結果；Index 為送出明細的 0 起算序號</summary>
    public sealed record LabSupportBatchItemResult(int Index, bool Succeeded, string? Error, List<string> Warnings);

    /// <summary>批次結果。逐筆結果為空代表整批層級錯誤（例如筆數超過上限）</summary>
    public sealed record LabSupportBatchResult(bool Succeeded, string Message, int Created, List<LabSupportBatchItemResult> Items);

    public sealed class BatchAssignLabSupportCommandHandler : IRequestHandler<BatchAssignLabSupportCommand, LabSupportBatchResult>
    {
        public const int MaxItems = 100;

        private readonly DbContext _context;
        private readonly ORGDbContext _orgContext;
        private readonly TimeProvider _clock;

        public BatchAssignLabSupportCommandHandler(DbContext context, ORGDbContext orgContext, TimeProvider clock)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public async Task<LabSupportBatchResult> Handle(BatchAssignLabSupportCommand request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var items = request.Items ?? [];
            if (items.Count == 0)
            {
                return Fail("請至少選擇一筆。");
            }

            if (items.Count > MaxItems)
            {
                return Fail($"單次最多 {MaxItems} 筆，目前 {items.Count} 筆。");
            }

            var today = LabSupportRules.TodayInTaiwan(_clock);

            // 一次載入所需資料，逐筆於記憶體檢查（避免 N+1）
            var userIds = items.Select(i => i.UserId).Where(id => id != Guid.Empty).Distinct().ToList();
            var mappings = await _context.Set<UserLabMapping>()
                .Where(x => userIds.Contains(x.UserId))
                .ToListAsync(cancellationToken);

            var homeByUser = mappings
                .Where(m => m.IsActive && m.AssignmentType == LabAssignmentType.Primary)
                .GroupBy(m => m.UserId)
                .ToDictionary(g => g.Key, g => g.First().LabId);

            var orgIds = items.Select(i => i.LabId).Concat(homeByUser.Values).Distinct().ToList();
            var organizations = await _orgContext.Organizations
                .AsNoTracking()
                .Include(o => o.Parent)
                .Where(o => orgIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, cancellationToken);

            var results = new List<LabSupportBatchItemResult>(items.Count);
            var seen = new HashSet<(Guid UserId, int LabId)>();

            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var warnings = new List<string>();
                var error = ValidateItem(item, today, mappings, homeByUser, organizations, seen, warnings);
                results.Add(new LabSupportBatchItemResult(index, error is null, error, warnings));
            }

            var failed = results.Count(r => !r.Succeeded);
            if (failed > 0)
            {
                return new LabSupportBatchResult(false, $"有 {failed} 筆不合格，請修正或移除後再試。整批未建立。", 0, results);
            }

            if (request.DryRun)
            {
                return new LabSupportBatchResult(true, $"檢查通過，可建立 {items.Count} 筆。", 0, results);
            }

            using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var item in items)
                {
                    var effective = LabSupportRules.ToUtcStartOfDay(item.EffectiveDate ?? today);
                    var expiry = LabSupportRules.ToUtcEndOfDay(item.ExpiryDate);
                    var existing = mappings.FirstOrDefault(m => m.UserId == item.UserId && m.LabId == item.LabId);

                    if (existing is null)
                    {
                        var created = UserLabMapping.Create(
                            item.UserId, item.LabId, item.TenantLabId, LabAssignmentType.Support,
                            item.JobTitle, effective, expiry, request.OperatorId);
                        await _context.Set<UserLabMapping>().AddAsync(created, cancellationToken);
                    }
                    else
                    {
                        // 已撤銷或既有的支援：以本次內容覆寫並重新啟用
                        existing.UpdateDetails(item.TenantLabId, LabAssignmentType.Support, item.JobTitle, effective, expiry, request.OperatorId);
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new LabSupportBatchResult(true, $"已建立 {items.Count} 筆實驗室支援。", items.Count, results);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new LabSupportBatchResult(false, $"批次建立失敗，整批已回復：{ex.Message}", 0, results);
            }
        }

        private static string? ValidateItem(
            BatchLabSupportItem item,
            DateTime today,
            IReadOnlyList<UserLabMapping> mappings,
            IReadOnlyDictionary<Guid, int> homeByUser,
            IReadOnlyDictionary<int, SGS.Modules.ORG.Infrastructure.Entities.Org.Organization> organizations,
            ISet<(Guid UserId, int LabId)> seen,
            List<string> warnings)
        {
            if (item.UserId == Guid.Empty || item.LabId <= 0 || item.TenantLabId == Guid.Empty)
            {
                return "人員與支援實驗室為必填。";
            }

            var effectiveDate = (item.EffectiveDate ?? today).Date;
            if (item.ExpiryDate.Date < effectiveDate)
            {
                return "生效結束日不得早於起始日。";
            }

            if (!seen.Add((item.UserId, item.LabId)))
            {
                return "批次內重複：同一人員與同一支援實驗室只能出現一次。";
            }

            // 主要實驗室、同實驗室別檢核（與單筆指派同一規則）
            homeByUser.TryGetValue(item.UserId, out var homeLabId);
            var categoryError = LabSupportRules.CheckCategory(homeByUser.ContainsKey(item.UserId) ? homeLabId : null, item.LabId, organizations);
            if (categoryError is { } e)
            {
                return e.Message;
            }

            var existing = mappings.FirstOrDefault(m => m.UserId == item.UserId && m.LabId == item.LabId);
            if (existing is not null)
            {
                if (existing.IsActive && existing.AssignmentType != LabAssignmentType.Support)
                {
                    var label = existing.AssignmentType == LabAssignmentType.Primary ? "主要" : "兼任";
                    return $"該人員於此實驗室已有「{label}」歸屬，無法改為支援。";
                }

                if (existing.IsActive)
                {
                    warnings.Add($"已有支援紀錄（{existing.EffectiveDate:yyyy/MM/dd} ~ {existing.ExpiryDate:yyyy/MM/dd}），將以本次內容覆寫。");
                }
                else
                {
                    warnings.Add("此人員於該實驗室有已撤銷的紀錄，將以本次內容重新啟用。");
                }
            }

            return null;
        }

        private static LabSupportBatchResult Fail(string message) => new(false, message, 0, []);
    }
}
