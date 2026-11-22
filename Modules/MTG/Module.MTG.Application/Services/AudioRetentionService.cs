using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.MTG.Application.Contracts;
using SGS.Modules.MTG.Application.Options;
using SGS.Modules.MTG.Infrastructure.Dbcontexts;
using SGS.Modules.MTG.Infrastructure.Entities;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.MTG.Application.Services;

/// <summary>
/// 會議錄音保存期限：超過 <c>Meeting:AudioRetentionDays</c> 的錄音從檔案管理（FMS）刪除，逐字稿與紀要保留。
/// <para>
/// 只處理已轉錄或轉錄失敗的錄音（排隊中、轉錄中的不碰），單次最多 200 筆，由 SCH 排程以系統帳號每日呼叫。
/// 先刪 FMS、成功後才標記為過期；FMS 刪除失敗的維持原狀，下一次排程再試。FMS 回報「不存在或已刪除」也視為已清除，
/// 所以人工在檔案管理刪過的錄音不會讓排程永遠卡住。
/// </para>
/// </summary>
public sealed class AudioRetentionService(
    MTGDbContext db,
    IFileManagementService files,
    IOptionsMonitor<MtgOptions> options,
    TimeProvider clock,
    ILogger<AudioRetentionService> logger)
{
    public const int MaxRetentionDays = 3650;
    public const int BatchSize = 200;

    public static string ExpiredMessage(int days) => $"錄音已依保存期限（{days} 天）刪除，逐字稿與紀要保留。";

    public async Task<AudioPurgeResult> PurgeExpiredAsync(CancellationToken ct)
    {
        var days = Math.Clamp(options.CurrentValue.AudioRetentionDays, 0, MaxRetentionDays);
        if (days == 0) return new AudioPurgeResult(0, 0, 0, 0, "未設定錄音保存天數（Meeting:AudioRetentionDays = 0），不自動刪除錄音。");

        var now = clock.GetUtcNow();
        var cutoff = now.AddDays(-days);

        var query = db.MeetingAudios.Where(a => (a.Status == AudioStatus.Transcribed || a.Status == AudioStatus.Failed) && a.CreatedAtUtc < cutoff);
        var candidates = await query.CountAsync(ct).ConfigureAwait(false);
        var batch = await query.OrderBy(a => a.CreatedAtUtc).Take(BatchSize).ToListAsync(ct).ConfigureAwait(false);

        var expired = 0;
        var failed = 0;
        foreach (var audio in batch)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // 回傳 false＝FMS 裡已不存在或早已刪除：目的（錄音不在了）已達成，照樣標記
                await files.DeleteAsync(audio.FileId, FileAccessScope.Internal, ct).ConfigureAwait(false);

                audio.Status = AudioStatus.Expired;
                audio.ExpiredAtUtc = now;
                audio.StatusMessage = ExpiredMessage(days);
                audio.UpdatedBy = "system:mtg-audio-retention";
                audio.UpdatedAtUtc = now;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                expired++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                db.ChangeTracker.Clear();
                logger.LogWarning(ex, "[MTG] 刪除過期錄音 {AudioId}（FMS {FileId}）失敗，下次排程再試。", audio.Id, audio.FileId);
            }
        }

        var message = $"保存 {days} 天：符合 {candidates} 筆，已刪除 {expired} 筆" + (failed > 0 ? $"，失敗 {failed} 筆（下次再試）" : "") +
                      (candidates > batch.Count ? $"；單次上限 {BatchSize} 筆，其餘下次處理" : "") + "。";
        logger.LogInformation("[MTG] 過期錄音清除：{Message}", message);
        return new AudioPurgeResult(days, candidates, expired, failed, message);
    }
}
