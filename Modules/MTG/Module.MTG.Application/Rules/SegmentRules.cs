using SGS.Modules.MTG.Infrastructure.Entities;

namespace SGS.Modules.MTG.Application.Rules;

/// <summary>要寫入的逐字稿片段（來自音檔轉錄或桌面端上傳）。</summary>
public sealed record NewSegment(string? ClientSegmentId, string? Speaker, double? StartSeconds, double? EndSeconds, string Text);

/// <summary>桌面端批次上傳的驗證與片段建立（純函式）。</summary>
public static class SegmentRules
{
    public const int MaxBatchSize = 500;
    public const int MaxTextLength = 4000;
    public const int MaxClientSegmentIdLength = 64;
    public const int MaxSpeakerLength = 50;

    /// <summary>驗證並整理桌面端批次：去除空白、同批次內相同的用戶端識別只留第一筆；回傳錯誤訊息（通過時為 null）。</summary>
    public static (IReadOnlyList<NewSegment> Items, string? Error) NormalizeClientBatch(IReadOnlyList<NewSegment>? batch)
    {
        if (batch is null || batch.Count == 0) return ([], "請提供至少一筆逐字稿片段。");
        if (batch.Count > MaxBatchSize) return ([], $"單次最多 {MaxBatchSize} 筆片段。");

        var result = new List<NewSegment>(batch.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < batch.Count; i++)
        {
            var s = batch[i];
            var id = s.ClientSegmentId?.Trim();
            if (string.IsNullOrEmpty(id)) return ([], $"第 {i + 1} 筆缺少 clientSegmentId（用來避免重送時重複寫入）。");
            if (id.Length > MaxClientSegmentIdLength) return ([], $"第 {i + 1} 筆的 clientSegmentId 不得超過 {MaxClientSegmentIdLength} 字。");

            var text = s.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return ([], $"第 {i + 1} 筆的文字不得為空。");
            if (text.Length > MaxTextLength) return ([], $"第 {i + 1} 筆的文字不得超過 {MaxTextLength} 字。");

            var speaker = s.Speaker?.Trim();
            if (speaker is { Length: > MaxSpeakerLength }) return ([], $"第 {i + 1} 筆的講者不得超過 {MaxSpeakerLength} 字。");

            var (start, end) = (s.StartSeconds, s.EndSeconds);
            if (start is { } a && (!double.IsFinite(a) || a < 0)) return ([], $"第 {i + 1} 筆的 startSeconds 不正確。");
            if (end is { } b && (!double.IsFinite(b) || b < 0)) return ([], $"第 {i + 1} 筆的 endSeconds 不正確。");
            if (start is { } x && end is { } y && y < x) return ([], $"第 {i + 1} 筆的結束時間早於開始時間。");

            if (!seen.Add(id)) continue;
            result.Add(new NewSegment(id, string.IsNullOrEmpty(speaker) ? null : speaker, start, end, text));
        }

        return (result, null);
    }

    /// <summary>從序號 <paramref name="startSeq"/> 起建立片段實體，並套用術語校正；回傳實體與被校正的句數。</summary>
    public static (List<TranscriptSegment> Segments, int Corrected) Build(
        Guid meetingId, int startSeq, IEnumerable<NewSegment> items, IReadOnlyList<GlossaryTerm> terms, Guid? audioId)
    {
        var segments = new List<TranscriptSegment>();
        var corrected = 0;
        var seq = startSeq;
        foreach (var item in items)
        {
            var fixedText = GlossaryCorrector.Correct(item.Text, terms);
            var changed = !string.Equals(fixedText, item.Text, StringComparison.Ordinal);
            if (changed) corrected++;
            segments.Add(new TranscriptSegment
            {
                Id = Guid.NewGuid(),
                MeetingId = meetingId,
                Seq = seq++,
                Speaker = item.Speaker,
                StartSeconds = item.StartSeconds,
                EndSeconds = item.EndSeconds,
                Text = item.Text,
                CorrectedText = changed ? fixedText : null,
                ClientSegmentId = item.ClientSegmentId,
                AudioId = audioId
            });
        }

        return (segments, corrected);
    }
}
