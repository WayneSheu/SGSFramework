using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGS.Modules.LOG.Infrastructure.Extensions;
using SGSFramework.Core.Logging;

namespace SGS.Modules.LOG.Infrastructure.Services;

/// <summary>
/// 日誌庫年度換庫（冷熱分層，半自動）。日誌為 SQL Ledger 帳本表、資料庫禁止 DELETE，舊資料只能整庫換新：
/// <list type="number">
/// <item><b>準備階段</b>（<see cref="PrepareAsync"/>，可由排程於年初自動執行，熱庫全程可寫）：完整備份熱庫並驗證，
/// 以 NORECOVERY 還原為冷庫 <c>{熱庫}_{年度}</c>，冷庫停在「還原中」（不會出現在查詢來源、不可讀寫）。</item>
/// <item><b>核准階段</b>（<see cref="FinalizeAsync"/>，管理者核准後執行，不可逆）：先以 RESTRICTED_USER 凍結熱庫寫入
/// （寫入失敗的日誌自動落檔至 Log_Buffer / Audit_Failures，之後回補），做差異備份並還原收尾冷庫，核對筆數、驗證 Ledger、
/// 冷庫轉 READ_ONLY，才刪除舊熱庫、建立全新熱庫（帳號授權 + Migration），並重新載入冷熱分層目錄。</item>
/// </list>
/// 只用換庫專用帳號 (RotationConnection，CREATE ANY DATABASE + 熱庫 db_owner)；所有資料庫名稱先經白名單驗證再以 QUOTENAME / 括號組合，
/// 備份路徑與其他值一律參數化。核准階段每個步驟都可重複執行（冪等）：中途失敗時再次核准會從目前狀態接續。
/// </summary>
public sealed class LogRotationService(
    IConfiguration configuration,
    ILogTierCatalog catalog,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<LogRotationService> logger)
{
    private const string LockResource = "PhysLIMS.LogRotation";
    private static readonly string[] LedgerTables = ["SystemLogs", "SecurityLog", "AuditLogs"];

    private sealed record Ctx(string HotDb, string MasterConnection, string? BackupDirectory, int CommandTimeout);

    // ───────────────────────────── 狀態 ─────────────────────────────

    public async Task<LogRotationStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var hot = catalog.Hot.Database;
        var now = timeProvider.GetLocalNow();
        var granularity = ConfiguredGranularity();

        // 既有期間：已登記 / 偵測到的冷庫 + 伺服器上名稱符合的資料庫（含還原中的待核准者）
        var existing = new List<LogRotationPeriod>();
        foreach (var cold in catalog.Colds)
        {
            if (LogRotationNaming.TryParseColdPeriod(hot, cold.Database, out var coldPeriod)) existing.Add(coldPeriod);
        }

        string? problem = null;
        Ctx? ctx = null;
        try
        {
            ctx = BuildContext();
        }
        catch (LogRotationException ex)
        {
            problem = ex.Message;
        }

        var hotExists = false;
        var hotOnline = false;
        var busy = false;
        LogRotationStagedInfo? staged = null;

        if (ctx is not null)
        {
            try
            {
                await using var connection = await OpenAsync(ctx, cancellationToken).ConfigureAwait(false);

                var state = await DatabaseStateAsync(connection, ctx, hot, cancellationToken).ConfigureAwait(false);
                hotExists = state is not null;
                hotOnline = string.Equals(state, "ONLINE", StringComparison.OrdinalIgnoreCase);

                var stagedNames = await StagedDatabasesAsync(connection, ctx, cancellationToken).ConfigureAwait(false);
                if (stagedNames.Count > 0 && LogRotationNaming.TryParseColdPeriod(hot, stagedNames[0], out var stagedPeriod))
                {
                    staged = new LogRotationStagedInfo(stagedNames[0], stagedPeriod.Suffix, stagedPeriod.DisplayName);
                }

                existing.AddRange(await ExistingPeriodsAsync(connection, ctx, cancellationToken).ConfigureAwait(false));

                if (await TryLockAsync(connection, ctx, cancellationToken).ConfigureAwait(false))
                {
                    await ReleaseLockAsync(connection, ctx).ConfigureAwait(false);
                }
                else
                {
                    busy = true;
                }
            }
            catch (LogRotationException ex)
            {
                problem = ex.Message;
            }
            catch (SqlException ex)
            {
                problem = $"無法以換庫帳號連線資料庫（SQL 錯誤 {ex.Number}）：{ex.Message}";
            }
        }

        var phase = staged is not null ? LogRotationPhase.Staged
            : ctx is not null && problem is null && !hotExists ? LogRotationPhase.HotMissing
            : LogRotationPhase.Idle;

        var suggestion = LogRotationNaming.Suggest(now, granularity, existing);
        string? note = null;
        if (suggestion.WasSkipped)
        {
            var conflict = existing.FirstOrDefault(e => e.Overlaps(suggestion.Initial));
            note = $"{suggestion.Initial.DisplayName}已有對應的冷庫{(conflict == default ? string.Empty : $"（{conflict.DisplayName}）")}，建議改封存 {suggestion.Period.DisplayName}。";
        }

        return new LogRotationStatus(
            Configured: ctx is not null,
            Problem: problem,
            HotDatabase: hot,
            HotExists: hotExists,
            HotOnline: hotOnline,
            Granularity: granularity.ToString(),
            SuggestedPeriod: suggestion.Period.Suffix,
            SuggestedPeriodDisplay: suggestion.Period.DisplayName,
            SuggestedColdDatabase: SafeColdName(hot, suggestion.Period),
            SuggestedNote: note,
            Phase: phase,
            Busy: busy,
            Staged: staged,
            Colds: catalog.Colds.Select(c => new LogRotationColdInfo(c.Key, c.Database, c.DisplayName)).ToList());
    }

    // ───────────────────────────── 準備階段 ─────────────────────────────

    /// <summary>
    /// 準備階段：完整備份 → 驗證 → NORECOVERY 還原為冷庫（停在還原中）。熱庫保持可寫，不影響服務。
    /// 失敗時清除本次建立的還原中冷庫；已有待核准的換庫時拒絕。
    /// </summary>
    /// <param name="periodText">封存期間：2026（年度）、2026H1（上半年）、2026H2（下半年）；空白 = 使用建議期間（已跳過重疊的既有冷庫）</param>
    public async Task<LogRotationPrepareResult> PrepareAsync(string? periodText, string requestedBy, CancellationToken cancellationToken = default)
    {
        var started = timeProvider.GetUtcNow();
        var steps = new List<string>();
        var ctx = BuildContext();

        await using var connection = await OpenAsync(ctx, cancellationToken).ConfigureAwait(false);
        await AcquireLockOrThrowAsync(connection, ctx, cancellationToken).ConfigureAwait(false);
        var coldCreated = false;
        var cold = ctx.HotDb;

        try
        {
            // 期間：明確指定者須與既有冷庫不重疊；未指定時採建議期間
            var existingPeriods = new List<LogRotationPeriod>();
            foreach (var known in catalog.Colds)
            {
                if (LogRotationNaming.TryParseColdPeriod(ctx.HotDb, known.Database, out var knownPeriod)) existingPeriods.Add(knownPeriod);
            }

            existingPeriods.AddRange(await ExistingPeriodsAsync(connection, ctx, cancellationToken).ConfigureAwait(false));

            LogRotationPeriod targetPeriod;
            if (string.IsNullOrWhiteSpace(periodText))
            {
                targetPeriod = LogRotationNaming.Suggest(timeProvider.GetLocalNow(), ConfiguredGranularity(), existingPeriods).Period;
            }
            else if (!LogRotationPeriod.TryParse(periodText, out targetPeriod))
            {
                throw new LogRotationException($"封存期間格式不正確：'{periodText}'（可用 2026、2026H1、2026H2）。");
            }

            var overlap = existingPeriods.Where(e => e.Overlaps(targetPeriod)).Select(e => e.DisplayName).Distinct().ToList();
            if (overlap.Count > 0)
            {
                throw new LogRotationException($"封存期間 {targetPeriod.DisplayName} 與既有冷庫重疊（{string.Join("、", overlap)}），請改選其他期間。");
            }

            cold = ColdName(ctx.HotDb, targetPeriod);
            logger.LogWarning("[LOG 換庫] 準備階段開始：{Hot} → {Cold}（期間 {Period}，發起者：{By}）", ctx.HotDb, cold, targetPeriod.DisplayName, requestedBy);

            var hotState = await DatabaseStateAsync(connection, ctx, ctx.HotDb, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(hotState, "ONLINE", StringComparison.OrdinalIgnoreCase))
            {
                throw new LogRotationException($"熱庫 {ctx.HotDb} 不存在或不在線上（目前狀態：{hotState ?? "不存在"}），無法準備換庫。");
            }

            var existing = await StagedDatabasesAsync(connection, ctx, cancellationToken).ConfigureAwait(false);
            if (existing.Count > 0)
            {
                throw new LogRotationException($"已有待核准的換庫（冷庫 {existing[0]} 已備妥）。請先核准完成，或取消後再重新準備。");
            }

            if (await DatabaseStateAsync(connection, ctx, cold, cancellationToken).ConfigureAwait(false) is not null)
            {
                throw new LogRotationException($"資料庫 {cold} 已存在（不可覆寫）。請更換年度，或確認該年度是否已換庫。");
            }

            var directory = ctx.BackupDirectory ?? await DefaultBackupDirectoryAsync(connection, ctx, cancellationToken).ConfigureAwait(false)
                ?? throw new LogRotationException($"無法取得預設備份目錄，請設定 {LogRotationOptions.BackupDirectoryKey}（SQL Server 主機上已存在的目錄）。");

            var backupFile = LogRotationNaming.BackupFilePath(directory, ctx.HotDb, targetPeriod, differential: false, timeProvider.GetUtcNow());

            // 非 COPY_ONLY 的完整備份：核准階段才能以差異備份銜接（差異備份以最近一次完整備份為基底）
            await StepAsync(steps, "完整備份熱庫", () => BackupAsync(connection, ctx, ctx.HotDb, backupFile, differential: false, copyOnly: false, cancellationToken)).ConfigureAwait(false);
            await StepAsync(steps, "驗證備份檔", () => VerifyBackupAsync(connection, ctx, backupFile, cancellationToken)).ConfigureAwait(false);

            coldCreated = true;
            await StepAsync(steps, $"還原為冷庫 {cold}（還原中，待核准）",
                () => RestoreFullAsync(connection, ctx, ctx.HotDb, cold, backupFile, recovery: false, cancellationToken)).ConfigureAwait(false);

            var staged = await StagedDatabasesAsync(connection, ctx, cancellationToken).ConfigureAwait(false);
            if (!staged.Contains(cold, StringComparer.OrdinalIgnoreCase))
            {
                throw new LogRotationException($"還原後冷庫 {cold} 不在預期的「還原中」狀態，已中止。");
            }

            coldCreated = false;
            var duration = timeProvider.GetUtcNow() - started;
            logger.LogWarning("[LOG 換庫] 準備階段完成：冷庫 {Cold} 已備妥待核准，備份檔 {File}，耗時 {Seconds:0} 秒", cold, backupFile, duration.TotalSeconds);
            return new LogRotationPrepareResult(cold, targetPeriod.Suffix, targetPeriod.DisplayName, backupFile, duration, steps);
        }
        catch (Exception ex) when (ex is not LogRotationException)
        {
            throw ToRotationException(ex, "準備階段");
        }
        finally
        {
            if (coldCreated)
            {
                // 本次建立的半成品冷庫（失敗中止）：只刪除「還原中」狀態者
                await TryDropRestoringAsync(connection, ctx, cold).ConfigureAwait(false);
            }

            await ReleaseLockAsync(connection, ctx).ConfigureAwait(false);
        }
    }

    // ───────────────────────────── 取消 ─────────────────────────────

    /// <summary>取消待核准的換庫：刪除還原中的冷庫（只刪「還原中」的資料庫；備份檔保留）</summary>
    public async Task<string> CancelAsync(string requestedBy, CancellationToken cancellationToken = default)
    {
        var ctx = BuildContext();
        await using var connection = await OpenAsync(ctx, cancellationToken).ConfigureAwait(false);
        await AcquireLockOrThrowAsync(connection, ctx, cancellationToken).ConfigureAwait(false);

        try
        {
            var staged = await StagedDatabasesAsync(connection, ctx, cancellationToken).ConfigureAwait(false);
            if (staged.Count == 0) throw new LogRotationException("目前沒有待核准的換庫。");

            foreach (var name in staged)
            {
                await ExecAsync(connection, $"DROP DATABASE {Q(name)};", ctx.CommandTimeout, cancellationToken).ConfigureAwait(false);
                logger.LogWarning("[LOG 換庫] 已取消待核准的換庫並刪除還原中的冷庫 {Cold}（操作者：{By}）", name, requestedBy);
            }

            return $"已取消換庫，並刪除還原中的冷庫 {string.Join("、", staged)}（備份檔保留）。";
        }
        catch (Exception ex) when (ex is not LogRotationException)
        {
            throw ToRotationException(ex, "取消換庫");
        }
        finally
        {
            await ReleaseLockAsync(connection, ctx).ConfigureAwait(false);
        }
    }

    // ───────────────────────────── 核准階段 ─────────────────────────────

    /// <summary>
    /// 核准階段（不可逆）。冪等：依目前狀態接續——
    /// 有「還原中」冷庫 → 凍結、收尾冷庫、核對、驗證、唯讀、刪舊熱庫、重建熱庫；熱庫不存在 → 只重建熱庫。
    /// </summary>
    public async Task<LogRotationFinalizeResult> FinalizeAsync(string approvedBy, CancellationToken cancellationToken = default)
    {
        var started = timeProvider.GetUtcNow();
        var steps = new List<string>();
        var ctx = BuildContext();

        await using var connection = await OpenAsync(ctx, cancellationToken).ConfigureAwait(false);
        await AcquireLockOrThrowAsync(connection, ctx, cancellationToken).ConfigureAwait(false);

        var hotFrozen = false;
        var hotDropped = false;
        var usedFallback = false;
        string? cold = null;
        var period = default(LogRotationPeriod);
        var counts = new Dictionary<string, long>();

        try
        {
            var hotState = await DatabaseStateAsync(connection, ctx, ctx.HotDb, cancellationToken).ConfigureAwait(false);
            var staged = await StagedDatabasesAsync(connection, ctx, cancellationToken).ConfigureAwait(false);

            if (staged.Count > 1)
            {
                throw new LogRotationException($"偵測到多個還原中的冷庫（{string.Join("、", staged)}），請 DBA 確認後取消多餘者。");
            }

            if (staged.Count == 1)
            {
                cold = staged[0];
                if (!LogRotationNaming.TryParseColdPeriod(ctx.HotDb, cold, out period))
                {
                    throw new LogRotationException($"還原中的資料庫 {cold} 名稱不符 {ctx.HotDb}_<年度>，拒絕處理。");
                }

                if (!string.Equals(hotState, "ONLINE", StringComparison.OrdinalIgnoreCase))
                {
                    throw new LogRotationException($"熱庫 {ctx.HotDb} 不在線上（狀態：{hotState ?? "不存在"}），無法核准換庫。");
                }

                logger.LogWarning("[LOG 換庫] 核准階段開始：{Hot} → {Cold}（核准者：{By}）。熱庫將暫停寫入，日誌暫存於本機緩衝檔。", ctx.HotDb, cold, approvedBy);

                // 1. 凍結熱庫寫入：非 db_owner 的連線（所有 API 節點的日誌寫入）立即被中斷，寫入失敗者落檔緩衝，結束後回補
                await StepAsync(steps, "凍結熱庫寫入（RESTRICTED_USER）",
                    () => ExecAsync(connection, $"ALTER DATABASE {Q(ctx.HotDb)} SET RESTRICTED_USER WITH ROLLBACK IMMEDIATE;", ctx.CommandTimeout, cancellationToken)).ConfigureAwait(false);
                hotFrozen = true;

                // 2. 差異備份 → 收尾冷庫（RECOVERY）；基底不符等失敗時改為凍結期間重新完整備份（較慢）
                usedFallback = !await TryFinishWithDifferentialAsync(connection, ctx, cold, steps, cancellationToken).ConfigureAwait(false);
                if (usedFallback)
                {
                    await FallbackFullAsync(connection, ctx, cold, steps, cancellationToken).ConfigureAwait(false);
                }

                // 3. 核對筆數（熱庫已凍結，冷庫必須與熱庫完全一致）
                counts = await StepAsync(steps, "核對熱庫 / 冷庫筆數", () => CompareCountsAsync(connection, ctx, cold, cancellationToken)).ConfigureAwait(false);

                // 4. 驗證冷庫 Ledger 完整性（須在轉唯讀前：產生摘要會寫入資料庫）
                await StepAsync(steps, "驗證冷庫 Ledger 完整性", () => VerifyLedgerAsync(connection, ctx, cold, cancellationToken)).ConfigureAwait(false);

                // 5. 冷庫轉唯讀
                await StepAsync(steps, "冷庫設為 READ_ONLY", () => SetReadOnlyAsync(connection, ctx, cold, cancellationToken)).ConfigureAwait(false);

                // 6. 刪除舊熱庫（不可逆；上述所有核對通過後才會執行）
                await StepAsync(steps, $"刪除舊熱庫 {ctx.HotDb}",
                    () => ExecAsync(connection,
                        $"ALTER DATABASE {Q(ctx.HotDb)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {Q(ctx.HotDb)};",
                        ctx.CommandTimeout, cancellationToken)).ConfigureAwait(false);
                hotDropped = true;
                hotFrozen = false;
            }
            else if (hotState is null)
            {
                // 上次換庫在「刪除舊熱庫」之後中斷：冷庫已完成，只剩重建熱庫
                steps.Add("偵測到熱庫不存在、冷庫已完成：只重建熱庫（復原模式）");
                logger.LogWarning("[LOG 換庫] 復原模式：重建熱庫 {Hot}（核准者：{By}）", ctx.HotDb, approvedBy);
                hotDropped = true;
                var colds = catalog.Colds;
                cold = colds.Count > 0 ? colds[^1].Database : null;
                if (cold is not null) LogRotationNaming.TryParseColdPeriod(ctx.HotDb, cold, out period);
            }
            else
            {
                throw new LogRotationException("目前沒有待核准的換庫，請先執行準備階段。");
            }

            // 7. 建立全新熱庫、帳號授權、Migration，並重新載入分層目錄
            await StepAsync(steps, $"建立全新熱庫 {ctx.HotDb}", () => ExecAsync(connection, $"CREATE DATABASE {Q(ctx.HotDb)};", ctx.CommandTimeout, cancellationToken)).ConfigureAwait(false);
            await StepAsync(steps, "建立帳號使用者與授權", () => ProvisionHotAsync(ctx, cancellationToken)).ConfigureAwait(false);

            SqlConnection.ClearAllPools();
            await StepAsync(steps, "套用日誌庫 Migration", () => MigrateAsync(cancellationToken)).ConfigureAwait(false);

            catalog.Refresh();
            steps.Add($"已重新載入冷熱分層目錄：冷庫 {catalog.Colds.Count} 個");

            var duration = timeProvider.GetUtcNow() - started;
            logger.LogWarning("[LOG 換庫] 核准階段完成：冷庫 {Cold}（READ_ONLY）、新熱庫 {Hot}，耗時 {Seconds:0} 秒；差異備份回退完整備份：{Fallback}",
                cold, ctx.HotDb, duration.TotalSeconds, usedFallback);

            return new LogRotationFinalizeResult(cold ?? string.Empty, period.Suffix, period.DisplayName, usedFallback, HotRecreated: true, counts, duration, steps);
        }
        catch (Exception ex) when (ex is not LogRotationException)
        {
            throw ToRotationException(ex, "核准階段");
        }
        finally
        {
            if (hotFrozen && !hotDropped)
            {
                // 失敗中止：熱庫尚在，恢復多使用者，日誌立即可繼續寫入
                try
                {
                    await ExecAsync(connection, $"ALTER DATABASE {Q(ctx.HotDb)} SET MULTI_USER WITH ROLLBACK IMMEDIATE;", ctx.CommandTimeout, CancellationToken.None).ConfigureAwait(false);
                    logger.LogWarning("[LOG 換庫] 核准階段中止，已恢復熱庫 {Hot} 為 MULTI_USER，日誌恢復寫入。", ctx.HotDb);
                }
                catch (Exception restoreError)
                {
                    logger.LogError(restoreError, "[LOG 換庫] 無法恢復熱庫 {Hot} 為 MULTI_USER，請 DBA 立即處理：ALTER DATABASE {Hot} SET MULTI_USER。", ctx.HotDb, ctx.HotDb);
                }
            }

            await ReleaseLockAsync(connection, ctx).ConfigureAwait(false);
        }
    }

    // ───────────────────────────── 備份 / 還原 ─────────────────────────────

    private async Task<bool> TryFinishWithDifferentialAsync(SqlConnection connection, Ctx ctx, string cold, List<string> steps, CancellationToken cancellationToken)
    {
        try
        {
            var directory = ctx.BackupDirectory ?? await DefaultBackupDirectoryAsync(connection, ctx, cancellationToken).ConfigureAwait(false)
                ?? throw new LogRotationException("無法取得預設備份目錄。");
            LogRotationNaming.TryParseColdPeriod(ctx.HotDb, cold, out var period);
            var diffFile = LogRotationNaming.BackupFilePath(directory, ctx.HotDb, period, differential: true, timeProvider.GetUtcNow());

            await StepAsync(steps, "差異備份熱庫（凍結後）", () => BackupAsync(connection, ctx, ctx.HotDb, diffFile, differential: true, copyOnly: false, cancellationToken)).ConfigureAwait(false);
            await StepAsync(steps, "以差異備份收尾冷庫（RECOVERY）", () => RestoreDifferentialAsync(connection, ctx, cold, diffFile, cancellationToken)).ConfigureAwait(false);
            return true;
        }
        catch (LogRotationException ex)
        {
            // 常見原因：排程完整備份在準備階段之後又做了一次完整備份，差異基底已改變
            logger.LogWarning(ex, "[LOG 換庫] 差異備份無法銜接準備階段的冷庫，改為凍結期間重新完整備份。");
            steps.Add($"差異備份無法銜接，改為重新完整備份（{ex.Message}）");
            return false;
        }
    }

    private async Task FallbackFullAsync(SqlConnection connection, Ctx ctx, string cold, List<string> steps, CancellationToken cancellationToken)
    {
        // 只刪「還原中」的冷庫（絕不刪除已上線的資料庫）
        await StepAsync(steps, "清除還原中的冷庫", async () =>
        {
            if (!await TryDropRestoringAsync(connection, ctx, cold).ConfigureAwait(false) &&
                await DatabaseStateAsync(connection, ctx, cold, cancellationToken).ConfigureAwait(false) is not null)
            {
                throw new LogRotationException($"無法清除還原中的冷庫 {cold}，已中止（熱庫未刪除）。");
            }
        }).ConfigureAwait(false);

        var directory = ctx.BackupDirectory ?? await DefaultBackupDirectoryAsync(connection, ctx, cancellationToken).ConfigureAwait(false)
            ?? throw new LogRotationException("無法取得預設備份目錄。");
        LogRotationNaming.TryParseColdPeriod(ctx.HotDb, cold, out var period);
        var file = LogRotationNaming.BackupFilePath(directory, ctx.HotDb, period, differential: false, timeProvider.GetUtcNow());

        await StepAsync(steps, "完整備份熱庫（凍結期間）", () => BackupAsync(connection, ctx, ctx.HotDb, file, differential: false, copyOnly: true, cancellationToken)).ConfigureAwait(false);
        await StepAsync(steps, "驗證備份檔", () => VerifyBackupAsync(connection, ctx, file, cancellationToken)).ConfigureAwait(false);
        await StepAsync(steps, $"還原為冷庫 {cold}（RECOVERY）", () => RestoreFullAsync(connection, ctx, ctx.HotDb, cold, file, recovery: true, cancellationToken)).ConfigureAwait(false);
    }

    private static Task BackupAsync(SqlConnection connection, Ctx ctx, string database, string file, bool differential, bool copyOnly, CancellationToken cancellationToken)
    {
        var options = new List<string> { "CHECKSUM", "INIT", "STATS = 10" };
        if (differential) options.Insert(0, "DIFFERENTIAL");
        if (copyOnly) options.Insert(0, "COPY_ONLY");

        return ExecAsync(connection, $"BACKUP DATABASE {Q(database)} TO DISK = @file WITH {string.Join(", ", options)};",
            ctx.CommandTimeout, cancellationToken, new SqlParameter("@file", SqlDbType.NVarChar, 520) { Value = file });
    }

    private static Task VerifyBackupAsync(SqlConnection connection, Ctx ctx, string file, CancellationToken cancellationToken) =>
        ExecAsync(connection, "RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM;",
            ctx.CommandTimeout, cancellationToken, new SqlParameter("@file", SqlDbType.NVarChar, 520) { Value = file });

    private static async Task RestoreFullAsync(SqlConnection connection, Ctx ctx, string hot, string cold, string file, bool recovery, CancellationToken cancellationToken)
    {
        // 以熱庫實體檔名推導冷庫檔名（MOVE），避免還原時覆蓋熱庫自己的檔案
        var moves = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name, physical_name FROM sys.master_files WHERE database_id = DB_ID(@db);";
            command.Parameters.Add(new SqlParameter("@db", SqlDbType.NVarChar, 128) { Value = hot });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var logical = reader.GetString(0);
                var physical = reader.GetString(1);
                var target = LogRotationNaming.DeriveColdFilePath(physical, hot, cold)
                    ?? throw new LogRotationException($"無法由熱庫實體檔名推導冷庫檔名（{physical} 未包含熱庫名稱 {hot}），為避免覆蓋熱庫檔案已中止；請 DBA 手動還原。");
                moves.Add($"MOVE N'{logical.Replace("'", "''")}' TO N'{target.Replace("'", "''")}'");
            }
        }

        if (moves.Count == 0) throw new LogRotationException("讀不到熱庫的實體檔案資訊（換庫帳號需具 CREATE ANY DATABASE）。");

        var mode = recovery ? "RECOVERY" : "NORECOVERY";
        await ExecAsync(connection,
            $"RESTORE DATABASE {Q(cold)} FROM DISK = @file WITH CHECKSUM, {mode}, {string.Join(", ", moves)}, STATS = 10;",
            ctx.CommandTimeout, cancellationToken, new SqlParameter("@file", SqlDbType.NVarChar, 520) { Value = file }).ConfigureAwait(false);
    }

    private static Task RestoreDifferentialAsync(SqlConnection connection, Ctx ctx, string cold, string file, CancellationToken cancellationToken) =>
        ExecAsync(connection, $"RESTORE DATABASE {Q(cold)} FROM DISK = @file WITH CHECKSUM, RECOVERY, STATS = 10;",
            ctx.CommandTimeout, cancellationToken, new SqlParameter("@file", SqlDbType.NVarChar, 520) { Value = file });

    // ───────────────────────────── 核對 / 驗證 ─────────────────────────────

    private static async Task<Dictionary<string, long>> CompareCountsAsync(SqlConnection connection, Ctx ctx, string cold, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, long>();
        foreach (var table in LedgerTables)
        {
            var hotCount = await CountAsync(connection, ctx, ctx.HotDb, table, cancellationToken).ConfigureAwait(false);
            var coldCount = await CountAsync(connection, ctx, cold, table, cancellationToken).ConfigureAwait(false);

            // 任一邊讀不到一律視為不一致（不放行）
            if (hotCount is null || coldCount is null || hotCount != coldCount)
            {
                throw new LogRotationException(
                    $"冷庫與熱庫筆數不一致或無法核對（log.{table}：熱庫 {hotCount?.ToString() ?? "無法讀取"} / 冷庫 {coldCount?.ToString() ?? "無法讀取"}），已中止，未刪除任何資料。");
            }

            result[table] = coldCount.Value;
        }

        return result;
    }

    private static async Task<long?> CountAsync(SqlConnection connection, Ctx ctx, string database, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT_BIG(*) FROM {Q(database)}.[log].{Q(table)};";
        command.CommandTimeout = ctx.CommandTimeout;
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Task VerifyLedgerAsync(SqlConnection connection, Ctx ctx, string cold, CancellationToken cancellationToken)
    {
        // 於冷庫內產生目前摘要並驗證（失敗會擲出錯誤）；動態 EXEC 讓 USE 只作用於該批次，不改變連線的資料庫內容
        var script = $"USE {Q(cold)}; " +
                     "CREATE TABLE #digest (digest nvarchar(max)); " +
                     "INSERT INTO #digest EXEC sys.sp_generate_database_ledger_digest; " +
                     "DECLARE @d nvarchar(max) = (SELECT TOP 1 digest FROM #digest); " +
                     "EXEC sys.sp_verify_database_ledger @d;";

        return ExecAsync(connection, "EXEC (@script);", ctx.CommandTimeout, cancellationToken,
            new SqlParameter("@script", SqlDbType.NVarChar, -1) { Value = script });
    }

    private static async Task SetReadOnlyAsync(SqlConnection connection, Ctx ctx, string cold, CancellationToken cancellationToken)
    {
        await ExecAsync(connection, $"ALTER DATABASE {Q(cold)} SET READ_ONLY WITH ROLLBACK IMMEDIATE;", ctx.CommandTimeout, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(DATABASEPROPERTYEX(@db, 'Updateability') AS nvarchar(20));";
        command.Parameters.Add(new SqlParameter("@db", SqlDbType.NVarChar, 128) { Value = cold });
        var updateability = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        if (!string.Equals(updateability, "READ_ONLY", StringComparison.OrdinalIgnoreCase))
        {
            throw new LogRotationException($"冷庫 {cold} 未能設為 READ_ONLY（目前：{updateability ?? "未知"}），已中止，未刪除舊熱庫。");
        }
    }

    // ───────────────────────────── 重建熱庫 ─────────────────────────────

    /// <summary>
    /// 新熱庫由換庫帳號建立（擁有者 = 換庫帳號）：建立 log schema 與執行期 / 部署 / 備份帳號的資料庫使用者與授權，
    /// 並啟用快照隔離（Ledger 驗證需要）；內容與 docs/sql/20261105_create_log_database.sql 一致。登入不存在者略過並警告。
    /// </summary>
    private async Task ProvisionHotAsync(Ctx ctx, CancellationToken cancellationToken)
    {
        var hotConnection = new SqlConnectionStringBuilder(ctx.MasterConnection) { InitialCatalog = ctx.HotDb }.ConnectionString;
        await using var connection = new SqlConnection(hotConnection);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ExecAsync(connection, "IF SCHEMA_ID(N'log') IS NULL EXEC (N'CREATE SCHEMA [log] AUTHORIZATION [dbo]');", ctx.CommandTimeout, cancellationToken).ConfigureAwait(false);

        var app = UserOf(configuration[LogDatabaseOptions.RuntimeConnectionKey]);
        var deploy = UserOf(configuration[LogDatabaseOptions.MigrationConnectionKey]) ?? app;
        var backup = UserOf(configuration["PersistentSettings:ConnectionStrings:BackupConnection"]);

        if (app is not null)
        {
            await ProvisionUserAsync(connection, ctx, app,
                "GRANT SELECT, INSERT ON SCHEMA::[log] TO {0}; GRANT VIEW LEDGER CONTENT TO {0};", cancellationToken).ConfigureAwait(false);
        }

        if (deploy is not null)
        {
            await ProvisionUserAsync(connection, ctx, deploy,
                "GRANT ALTER, CONTROL ON SCHEMA::[log] TO {0}; GRANT CREATE TABLE TO {0}; GRANT ENABLE LEDGER TO {0}; GRANT VIEW LEDGER CONTENT TO {0};", cancellationToken).ConfigureAwait(false);
        }

        if (backup is not null)
        {
            await ProvisionUserAsync(connection, ctx, backup,
                "ALTER ROLE [db_backupoperator] ADD MEMBER {0};", cancellationToken).ConfigureAwait(false);
        }

        await ExecAsync(connection,
            $"IF (SELECT snapshot_isolation_state FROM sys.databases WHERE name = DB_NAME()) = 0 ALTER DATABASE {Q(ctx.HotDb)} SET ALLOW_SNAPSHOT_ISOLATION ON;",
            ctx.CommandTimeout, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProvisionUserAsync(SqlConnection connection, Ctx ctx, string user, string grantTemplate, CancellationToken cancellationToken)
    {
        // 登入不存在（例如尚未建立備份登入）：略過並警告，不中止換庫（DBA 建好登入後重跑 20261105_create_log_database.sql）。
        // 帳號名稱只以參數傳入，於伺服器端 QUOTENAME 後取代範本中的 {0}，避免注入
        const string sql =
            """
            IF SUSER_ID(@user) IS NULL
            BEGIN
                SELECT CAST(0 AS bit);
                RETURN;
            END;
            DECLARE @quoted nvarchar(300) = QUOTENAME(@user);
            IF DATABASE_PRINCIPAL_ID(@user) IS NULL
            BEGIN
                DECLARE @create nvarchar(700) = N'CREATE USER ' + @quoted + N' FOR LOGIN ' + @quoted + N';';
                EXEC (@create);
            END;
            DECLARE @grant nvarchar(max) = REPLACE(@template, N'{0}', @quoted);
            EXEC (@grant);
            SELECT CAST(1 AS bit);
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = ctx.CommandTimeout;
        command.Parameters.Add(new SqlParameter("@user", SqlDbType.NVarChar, 128) { Value = user });
        command.Parameters.Add(new SqlParameter("@template", SqlDbType.NVarChar, -1) { Value = grantTemplate });

        var created = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (created is false)
        {
            logger.LogWarning("[LOG 換庫] 伺服器沒有登入 {User}，已略過新熱庫的使用者與授權；請 DBA 建立登入後執行 docs/sql/20261105_create_log_database.sql。", user);
        }
    }

    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        // 新 Scope：取得全新的 LogDbContext（不沿用換庫前已建立的內容）
        using var scope = scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<LogMigrationService>().MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    // ───────────────────────────── 共用 ─────────────────────────────

    private Ctx BuildContext()
    {
        var rotation = configuration[LogRotationOptions.ConnectionKey];
        if (string.IsNullOrWhiteSpace(rotation))
        {
            throw new LogRotationException(
                $"未設定換庫帳號連線 {LogRotationOptions.ConnectionKey}（請依 docs/sql/20261110_grant_log_rotation_account.sql 建立 rotate_sgs_user，並以環境變數注入連線字串）。");
        }

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(rotation);
        }
        catch (ArgumentException ex)
        {
            throw new LogRotationException($"換庫帳號連線字串格式不正確：{ex.Message}");
        }

        // 帳號分離：換庫帳號不得與執行期 / 部署 / 備份 / 主庫帳號相同（避免把高權限併入常態使用的帳號）
        var rotationUser = builder.UserID;
        if (!string.IsNullOrWhiteSpace(rotationUser))
        {
            foreach (var key in new[]
                     {
                         LogDatabaseOptions.RuntimeConnectionKey, LogDatabaseOptions.MigrationConnectionKey,
                         "PersistentSettings:ConnectionStrings:BackupConnection",
                         "PersistentSettings:ConnectionStrings:DefaultConnection",
                         "PersistentSettings:ConnectionStrings:MigrationConnection"
                     })
            {
                if (string.Equals(UserOf(configuration[key]), rotationUser, StringComparison.OrdinalIgnoreCase))
                {
                    throw new LogRotationException($"換庫帳號 {rotationUser} 與 {key} 使用同一個 SQL 帳號；換庫必須使用專用帳號（最小權限）。");
                }
            }
        }

        var hot = catalog.Hot.Database;
        try
        {
            LogRotationNaming.ValidateDatabaseName(hot, nameof(hot));
        }
        catch (ArgumentException ex)
        {
            throw new LogRotationException(ex.Message);
        }

        var timeout = configuration.GetValue("LogRotation:CommandTimeoutSeconds", 3600);
        timeout = Math.Clamp(timeout, 60, 14400);

        builder.InitialCatalog = "master";
        builder.ApplicationName = "PhysLIMS.LogRotation";
        var directory = configuration[LogRotationOptions.BackupDirectoryKey];
        return new Ctx(hot, builder.ConnectionString, string.IsNullOrWhiteSpace(directory) ? null : directory.Trim(), timeout);
    }

    private static async Task<SqlConnection> OpenAsync(Ctx ctx, CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(ctx.MasterConnection);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch (SqlException ex)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw new LogRotationException($"無法以換庫帳號連線資料庫（SQL 錯誤 {ex.Number}）：{ex.Message}", ex);
        }
    }

    private static string Q(string name) => "[" + name.Replace("]", "]]") + "]";

    private static string ColdName(string hot, LogRotationPeriod period)
    {
        try
        {
            return LogRotationNaming.ColdDatabaseName(hot, period);
        }
        catch (ArgumentException ex)
        {
            throw new LogRotationException(ex.Message, ex);
        }
    }

    private static string SafeColdName(string hot, LogRotationPeriod period)
    {
        try
        {
            return LogRotationNaming.ColdDatabaseName(hot, period);
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
    }

    private static string? UserOf(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return builder.IntegratedSecurity || string.IsNullOrWhiteSpace(builder.UserID) ? null : builder.UserID;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static async Task<string?> DatabaseStateAsync(SqlConnection connection, Ctx ctx, string database, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT state_desc FROM sys.databases WHERE name = @name;";
        command.CommandTimeout = ctx.CommandTimeout;
        command.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = database });
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    /// <summary>「還原中」的冷庫：名稱符合 {熱庫}_{期間}（2026 / 2026H1 / 2026H2）且 state = RESTORING（準備階段完成、等待核准）</summary>
    private static async Task<List<string>> StagedDatabasesAsync(SqlConnection connection, Ctx ctx, CancellationToken cancellationToken)
    {
        var names = await DatabaseNamesAsync(connection, ctx, "AND state = 1", cancellationToken).ConfigureAwait(false);
        return names.Where(n => LogRotationNaming.TryParseColdPeriod(ctx.HotDb, n, out _)).ToList();
    }

    /// <summary>伺服器上所有符合 {熱庫}_{期間} 命名的資料庫（任何狀態，含待核准的還原中冷庫）所佔的期間，供建議期間與重疊檢查使用</summary>
    private static async Task<List<LogRotationPeriod>> ExistingPeriodsAsync(SqlConnection connection, Ctx ctx, CancellationToken cancellationToken)
    {
        var names = await DatabaseNamesAsync(connection, ctx, string.Empty, cancellationToken).ConfigureAwait(false);
        var periods = new List<LogRotationPeriod>();
        foreach (var name in names)
        {
            if (LogRotationNaming.TryParseColdPeriod(ctx.HotDb, name, out var period)) periods.Add(period);
        }

        return periods;
    }

    private static async Task<List<string>> DatabaseNamesAsync(SqlConnection connection, Ctx ctx, string extraCondition, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT name FROM sys.databases WHERE name LIKE @pattern ESCAPE '\\' {extraCondition} ORDER BY name;";
        command.CommandTimeout = ctx.CommandTimeout;
        command.Parameters.Add(new SqlParameter("@pattern", SqlDbType.NVarChar, 300) { Value = LogRotationNaming.ColdLikePattern(ctx.HotDb) });

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) names.Add(reader.GetString(0));
        return names;
    }

    private LogRotationGranularity ConfiguredGranularity() =>
        Enum.TryParse<LogRotationGranularity>(configuration[LogRotationOptions.GranularityKey], ignoreCase: true, out var granularity)
            ? granularity
            : LogRotationGranularity.Year;

    private static async Task<string?> DefaultBackupDirectoryAsync(SqlConnection connection, Ctx ctx, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(260));";
        command.CommandTimeout = ctx.CommandTimeout;
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>只刪除「還原中」的冷庫（名稱經檢查、狀態為 RESTORING）；回傳是否已刪除。絕不刪除已上線的資料庫。</summary>
    private async Task<bool> TryDropRestoringAsync(SqlConnection connection, Ctx ctx, string cold)
    {
        try
        {
            if (!LogRotationNaming.TryParseColdPeriod(ctx.HotDb, cold, out _)) return false;

            var state = await DatabaseStateAsync(connection, ctx, cold, CancellationToken.None).ConfigureAwait(false);
            if (!string.Equals(state, "RESTORING", StringComparison.OrdinalIgnoreCase)) return false;

            await ExecAsync(connection, $"DROP DATABASE {Q(cold)};", ctx.CommandTimeout, CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[LOG 換庫] 清除還原中的冷庫 {Cold} 失敗，請 DBA 手動確認。", cold);
            return false;
        }
    }

    // 跨節點互斥：以 SQL Server 應用程式鎖（Session 範圍，連線關閉即釋放）
    private static async Task<bool> TryLockAsync(SqlConnection connection, Ctx ctx, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @res, @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0; SELECT @r;";
        command.CommandTimeout = ctx.CommandTimeout;
        command.Parameters.Add(new SqlParameter("@res", SqlDbType.NVarChar, 255) { Value = LockResource });
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is int code && code >= 0;
    }

    private static async Task AcquireLockOrThrowAsync(SqlConnection connection, Ctx ctx, CancellationToken cancellationToken)
    {
        if (!await TryLockAsync(connection, ctx, cancellationToken).ConfigureAwait(false))
        {
            throw new LogRotationException("已有換庫作業正在執行中（可能來自其他 API 節點），請稍後再試。");
        }
    }

    private static async Task ReleaseLockAsync(SqlConnection connection, Ctx ctx)
    {
        try
        {
            if (connection.State != ConnectionState.Open) return;
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC sp_releaseapplock @Resource = @res, @LockOwner = N'Session';";
            command.CommandTimeout = 30;
            command.Parameters.Add(new SqlParameter("@res", SqlDbType.NVarChar, 255) { Value = LockResource });
            await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // 連線關閉時鎖會自動釋放
        }
    }

    private static async Task ExecAsync(SqlConnection connection, string sql, int timeoutSeconds, CancellationToken cancellationToken, params SqlParameter[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = timeoutSeconds;
        foreach (var parameter in parameters) command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task StepAsync(List<string> steps, string name, Func<Task> action)
    {
        await StepAsync<object?>(steps, name, async () =>
        {
            await action().ConfigureAwait(false);
            return null;
        }).ConfigureAwait(false);
    }

    private async Task<T> StepAsync<T>(List<string> steps, string name, Func<Task<T>> action)
    {
        logger.LogInformation("[LOG 換庫] {Step}…", name);
        try
        {
            var result = await action().ConfigureAwait(false);
            steps.Add($"完成：{name}");
            return result;
        }
        catch (LogRotationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ToRotationException(ex, name);
        }
    }

    private LogRotationException ToRotationException(Exception ex, string step)
    {
        if (ex is OperationCanceledException) return new LogRotationException($"{step}已取消。", ex);

        if (ex is SqlException sql)
        {
            var hint = sql.Number is 229 or 262 or 300 or 916 or 15247
                ? "（換庫帳號權限不足，請確認已執行 docs/sql/20261110_grant_log_rotation_account.sql）"
                : sql.Number is 3201 or 3013 or 3241 or 3242 or 3243 or 3201
                    ? "（備份目錄無法存取：請確認目錄存在、SQL Server 服務帳號可寫入）"
                    : string.Empty;
            logger.LogError(ex, "[LOG 換庫] {Step}失敗（SQL 錯誤 {Number}）", step, sql.Number);
            return new LogRotationException($"{step}失敗（SQL 錯誤 {sql.Number}）：{sql.Message}{hint}", ex);
        }

        logger.LogError(ex, "[LOG 換庫] {Step}發生未預期的錯誤", step);
        return new LogRotationException($"{step}發生未預期的錯誤：{ex.Message}", ex);
    }
}
