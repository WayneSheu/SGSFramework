using System.Text;

namespace SGS.Modules.MTG.Application.Rules;

/// <summary>
/// 逐字稿切塊（純函式）：依行切分，每塊不超過上限，並保留前一塊結尾的重疊行作為前後文。
/// 單一超長行會被強制切開，確保一定前進、不會無窮迴圈。
/// </summary>
public static class TranscriptChunker
{
    public static IReadOnlyList<string> Chunk(string? transcript, int maxChars, int overlapChars)
    {
        if (string.IsNullOrWhiteSpace(transcript)) return [];
        if (maxChars < 100) throw new ArgumentOutOfRangeException(nameof(maxChars), "切塊上限至少 100 字元。");
        // 重疊須明顯小於上限，否則下一塊可能一開始就塞滿而無法前進
        overlapChars = Math.Clamp(overlapChars, 0, maxChars / 2);

        var lines = SplitLines(transcript, maxChars);
        var chunks = new List<string>();
        var current = new List<string>();
        var currentLength = 0;

        foreach (var line in lines)
        {
            var added = line.Length + (current.Count > 0 ? 1 : 0);
            if (current.Count > 0 && currentLength + added > maxChars)
            {
                chunks.Add(string.Join('\n', current));
                current = TakeOverlap(current, overlapChars);
                currentLength = current.Sum(l => l.Length) + Math.Max(0, current.Count - 1);
                added = line.Length + (current.Count > 0 ? 1 : 0);
                // 重疊加上本行仍超過上限時，放棄重疊，保證本行能放進新塊
                if (currentLength + added > maxChars)
                {
                    current = [];
                    currentLength = 0;
                    added = line.Length;
                }
            }

            current.Add(line);
            currentLength += added;
        }

        if (current.Count > 0) chunks.Add(string.Join('\n', current));
        return chunks;
    }

    private static List<string> SplitLines(string text, int maxChars)
    {
        var result = new List<string>();
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) continue;
            for (var i = 0; i < line.Length; i += maxChars)
                result.Add(line.Substring(i, Math.Min(maxChars, line.Length - i)));
        }
        return result;
    }

    private static List<string> TakeOverlap(List<string> chunkLines, int overlapChars)
    {
        var overlap = new List<string>();
        if (overlapChars <= 0) return overlap;

        var total = 0;
        for (var i = chunkLines.Count - 1; i >= 0; i--)
        {
            var cost = chunkLines[i].Length + (overlap.Count > 0 ? 1 : 0);
            if (total + cost > overlapChars) break;
            overlap.Insert(0, chunkLines[i]);
            total += cost;
        }
        return overlap;
    }

    /// <summary>以每行「[講者] 文字」格式組合逐字稿供模型使用。</summary>
    public static string BuildTranscriptText(IEnumerable<(string? Speaker, string Text)> segments)
    {
        var sb = new StringBuilder();
        foreach (var (speaker, text) in segments)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (sb.Length > 0) sb.Append('\n');
            if (!string.IsNullOrWhiteSpace(speaker)) sb.Append('[').Append(speaker.Trim()).Append("] ");
            sb.Append(text.Trim().Replace('\n', ' ').Replace('\r', ' '));
        }
        return sb.ToString();
    }
}
