#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Alerts;
using SGSFramework.SystemLog.Alerts;
using SGSFramework.SystemLog.DTOs;
using System.Net.Mail;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>告警規則管理：查詢、新增、修改、刪除與規則測試；異動後立即熱更新生效規則</summary>
    public interface IAlertRuleService
    {
        Task<IReadOnlyList<AlertRuleDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<AlertRuleDto> CreateAsync(AlertRuleSaveRequest request, CancellationToken cancellationToken = default);
        Task<AlertRuleDto?> UpdateAsync(Guid id, AlertRuleSaveRequest request, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        AlertRuleTestResult Test(AlertRuleTestRequest request);
    }

    public sealed class AlertRuleService : IAlertRuleService
    {
        private const int MaxCooldownSeconds = 86_400;

        private readonly DbContext _context;
        private readonly IAlertRuleProvider _provider;
        private readonly ILogger<AlertRuleService> _logger;

        public AlertRuleService(DbContext context, IAlertRuleProvider provider, ILogger<AlertRuleService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<AlertRuleDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var rules = await _context.Set<AlertRule>()
                .AsNoTracking()
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Name)
                .ToListAsync(cancellationToken);

            return rules.Select(ToDto).ToList();
        }

        public async Task<AlertRuleDto> CreateAsync(AlertRuleSaveRequest request, CancellationToken cancellationToken = default)
        {
            Validate(request);
            await EnsureNameUniqueAsync(request.Name, null, cancellationToken);

            var rule = new AlertRule();
            Apply(rule, request);

            _context.Set<AlertRule>().Add(rule);
            await _context.SaveChangesAsync(cancellationToken);
            await ReloadSafelyAsync(cancellationToken);

            _logger.LogInformation("告警規則已新增：{RuleName} ({RuleId})", rule.Name, rule.Id);
            return ToDto(rule);
        }

        public async Task<AlertRuleDto?> UpdateAsync(Guid id, AlertRuleSaveRequest request, CancellationToken cancellationToken = default)
        {
            Validate(request);

            var rule = await _context.Set<AlertRule>().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rule is null) return null;

            await EnsureNameUniqueAsync(request.Name, id, cancellationToken);
            Apply(rule, request);

            await _context.SaveChangesAsync(cancellationToken);
            await ReloadSafelyAsync(cancellationToken);

            _logger.LogInformation("告警規則已修改：{RuleName} ({RuleId})", rule.Name, rule.Id);
            return ToDto(rule);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var rule = await _context.Set<AlertRule>().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (rule is null) return false;

            _context.Set<AlertRule>().Remove(rule);
            await _context.SaveChangesAsync(cancellationToken);
            await ReloadSafelyAsync(cancellationToken);

            _logger.LogInformation("告警規則已刪除：{RuleName} ({RuleId})", rule.Name, id);
            return true;
        }

        public AlertRuleTestResult Test(AlertRuleTestRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!AlertRuleEngine.TryParseLevel(request.Level, out var level))
                throw new ArgumentException("日誌層級無效。", nameof(request));

            var isSecurity = string.Equals(request.LogType, AlertRuleLogTypes.Security, StringComparison.OrdinalIgnoreCase);
            if (!isSecurity && !string.Equals(request.LogType, AlertRuleLogTypes.System, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("日誌類型僅能是 System 或 Security。", nameof(request));

            var facts = new AlertLogFacts(isSecurity, level, request.Message ?? string.Empty, request.Module, request.Operation, request.ExceptionType);
            var snapshot = _provider.Current;

            // 與實際流程相同：先通過預先過濾，再依優先序比對
            var candidate = (isSecurity ? snapshot.MinSecurity : snapshot.MinSystem) is { } minimum && level >= minimum;
            var rule = candidate ? AlertRuleEngine.Match(facts, snapshot) : null;

            if (rule is null)
                return new AlertRuleTestResult(false, "沒有任何啟用中的規則符合，不會告警。", null, null, null, null);

            return rule.Action == AlertRuleActions.Alert
                ? new AlertRuleTestResult(true, $"符合規則「{rule.Name}」，會發送告警。", rule.Id, rule.Name, rule.Recipients, rule.CooldownSeconds)
                : new AlertRuleTestResult(false, $"符合抑制規則「{rule.Name}」，不會告警。", rule.Id, rule.Name, null, null);
        }

        private static void Validate(AlertRuleSaveRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new ArgumentException("規則名稱不可為空。");
            if (request.Name.Trim().Length > 100)
                throw new ArgumentException("規則名稱不可超過 100 個字元。");
            if (!AlertRuleLogTypes.All.Contains(request.LogType))
                throw new ArgumentException($"日誌類型無效，可用值：{string.Join(" / ", AlertRuleLogTypes.All)}。");
            if (!AlertRuleEngine.TryParseLevel(request.MinLevel, out _))
                throw new ArgumentException("最低層級無效，可用值：Verbose / Debug / Information / Warning / Error / Fatal。");
            if (!AlertRuleActions.All.Contains(request.Action))
                throw new ArgumentException($"動作無效，可用值：{string.Join(" / ", AlertRuleActions.All)}。");
            if (request.Priority is < 0 or > 100_000)
                throw new ArgumentException("優先序需介於 0 ~ 100000。");
            if (request.CooldownSeconds is < 0 or > MaxCooldownSeconds)
                throw new ArgumentException($"冷卻秒數需介於 0 ~ {MaxCooldownSeconds}。");

            if (!string.IsNullOrWhiteSpace(request.Recipients))
            {
                foreach (var address in request.Recipients.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!MailAddress.TryCreate(address, out _))
                        throw new ArgumentException($"收件人 Email 格式不正確：{address}");
                }
            }

            // 一條不限定任何條件的「Alert + Verbose」規則會讓所有日誌都告警，須明確避免誤設
            if (request.Action == AlertRuleActions.Alert &&
                request.LogType == AlertRuleLogTypes.Any &&
                string.Equals(request.MinLevel, "Verbose", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(request.MessageContains) &&
                string.IsNullOrWhiteSpace(request.ModuleContains) &&
                string.IsNullOrWhiteSpace(request.OperationContains) &&
                string.IsNullOrWhiteSpace(request.ExceptionContains))
            {
                throw new ArgumentException("此規則會讓所有日誌都觸發告警，請至少設定一項條件或提高最低層級。");
            }
        }

        private async Task EnsureNameUniqueAsync(string name, Guid? excludeId, CancellationToken cancellationToken)
        {
            var trimmed = name.Trim();
            var exists = await _context.Set<AlertRule>()
                .AnyAsync(r => r.Name == trimmed && (excludeId == null || r.Id != excludeId), cancellationToken);

            if (exists) throw new InvalidOperationException($"規則名稱「{trimmed}」已存在。");
        }

        private static void Apply(AlertRule rule, AlertRuleSaveRequest request)
        {
            rule.Name = request.Name.Trim();
            rule.Description = Clean(request.Description);
            rule.IsEnabled = request.IsEnabled;
            rule.Priority = request.Priority;
            rule.LogType = request.LogType;
            rule.MinLevel = Enum.Parse<Serilog.Events.LogEventLevel>(request.MinLevel, ignoreCase: true).ToString();
            rule.MessageContains = Clean(request.MessageContains);
            rule.ModuleContains = Clean(request.ModuleContains);
            rule.OperationContains = Clean(request.OperationContains);
            rule.ExceptionContains = Clean(request.ExceptionContains);
            rule.Action = request.Action;
            rule.CooldownSeconds = request.CooldownSeconds;
            rule.Recipients = Clean(request.Recipients);
        }

        // 規則已存檔，重載失敗（例如瞬斷）不應讓使用者看到儲存失敗；背景服務下一輪會補上
        private async Task ReloadSafelyAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _provider.ReloadAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "告警規則已儲存，但立即重載失敗，將於下次定期重載時生效。");
            }
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static AlertRuleDto ToDto(AlertRule r) => new(
            r.Id, r.Name, r.Description, r.IsEnabled, r.Priority, r.LogType, r.MinLevel,
            r.MessageContains, r.ModuleContains, r.OperationContains, r.ExceptionContains,
            r.Action, r.CooldownSeconds, r.Recipients, r.CreatedBy, r.CreatedAtUtc, r.UpdatedBy, r.UpdatedAtUtc);
    }
}
