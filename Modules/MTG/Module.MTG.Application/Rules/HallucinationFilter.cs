namespace SGS.Modules.MTG.Application.Rules;

/// <summary>
/// 移除 Whisper 在靜音或雜音段「幻聽」出的無關文字（純函式）。
/// Whisper 的訓練資料含大量影片字幕，遇到結尾靜音、長停頓或雜音時，常會憑空產生字幕片尾，且多半是簡體中文，
/// 例如「请不吝点赞 订阅 转发 打赏支持明镜与点点栏目」、「字幕志愿者 杨茜茜」、「感謝觀看」、「Thanks for watching」。
/// <para>
/// 實測這類片段的 <c>no_speech_prob</c> 是 0、<c>avg_logprob</c> 約 -0.03（模型對它很有把握），所以無法用信心值判斷，
/// 只能比對已知片語。為避免誤刪正常談話（例如有人真的說「請訂閱服務」），命中片語必須<b>涵蓋整段文字的一半以上</b>才移除。
/// </para>
/// </summary>
public static class HallucinationFilter
{
    /// <summary>已知幻聽片語（簡繁各一，比對前會去除標點與空白並轉小寫）。</summary>
    private static readonly string[] Phrases =
    [
        // 中文影片片尾
        "请不吝点赞", "請不吝點贊", "請不吝點讚",
        "订阅转发打赏", "訂閱轉發打賞",
        "明镜与点点栏目", "明鏡與點點欄目", "点点栏目", "點點欄目",
        "优优独播剧场", "優優獨播劇場",
        "感谢观看", "感謝觀看", "谢谢观看", "謝謝觀看", "感谢收看", "感謝收看",
        "订阅我的频道", "訂閱我的頻道", "欢迎订阅", "歡迎訂閱", "请订阅", "請訂閱",
        // 字幕署名
        "字幕志愿者", "字幕志願者", "字幕君", "中文字幕由", "字幕由amara", "由amaraorg社群提供的字幕", "amaraorg",
        // 英文
        "thanksforwatching", "thankyouforwatching", "pleasesubscribe", "likeandsubscribe", "subscribetomychannel",
        "subtitlesby", "transcribedby", "yoyotelevisionseriesexclusive",
        // 日文
        "ご視聴ありがとうございました", "ご視聴ありがとうございます"
    ];

    private static readonly string[] Needles = Phrases.Select(Normalize).Distinct().ToArray();

    /// <summary>只保留字母與數字（CJK 字元算字母），並轉小寫：去除標點、空白與符號。</summary>
    public static string Normalize(string text) =>
        string.Concat((text ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();

    /// <summary>整段文字是否主要由已知幻聽片語構成：所有命中片語涵蓋的字元（重疊只算一次）達整段的一半以上。</summary>
    public static bool IsHallucination(string? text)
    {
        var normalized = Normalize(text ?? string.Empty);
        if (normalized.Length == 0) return false;

        // 實際的片尾常由好幾個片語組成（「请不吝点赞」「订阅转发打赏」「明镜与点点栏目」），所以要合計涵蓋範圍，而不是只看單一片語
        var covered = new bool[normalized.Length];
        foreach (var needle in Needles)
        {
            for (var i = normalized.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = normalized.IndexOf(needle, i + 1, StringComparison.Ordinal))
            {
                for (var k = i; k < i + needle.Length; k++) covered[k] = true;
            }
        }

        return covered.Count(c => c) * 2 >= normalized.Length;
    }

    /// <summary>移除疑似幻聽的片段；回傳過濾後的結果與被移除的片段數。時間與其餘欄位維持原樣。</summary>
    public static (TranscriptionResult Result, int Removed) Apply(TranscriptionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var kept = result.Segments.Where(s => !IsHallucination(s.Text)).ToList();
        var removed = result.Segments.Count - kept.Count;
        return removed == 0 ? (result, 0) : (result with { Segments = kept }, removed);
    }

    /// <summary>成功轉錄但移除過片段時，給使用者看的說明；沒有移除時回傳 null。</summary>
    public static string? DescribeRemoval(int removed) =>
        removed <= 0 ? null : $"已自動移除 {removed} 段疑似幻聽的無關文字（例如結尾靜音產生的字幕片尾）。";
}
