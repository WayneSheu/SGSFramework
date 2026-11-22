using System.Text.RegularExpressions;

namespace SGS.Modules.MTG.Application.Rules;

/// <summary>貼上逐字稿解析出的片段。</summary>
public sealed record ParsedSegment(int Seq, string? Speaker, string Text);

/// <summary>
/// 逐字稿貼上解析（純函式）：每個非空行為一個片段；行首「[講者] 」或「講者：」會被拆出為講者標籤。
/// 講者標籤限制長度，避免把一般句子（例如含冒號的敘述）誤判為講者。
/// </summary>
public static partial class TranscriptParser
{
    private const int MaxSpeakerLength = 20;

    [GeneratedRegex(@"^\[(?<s>[^\[\]]{1,20})\]\s*(?<t>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex BracketSpeaker();

    [GeneratedRegex(@"^(?<s>[^\s:：\[\]]{1,20})[:：]\s*(?<t>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ColonSpeaker();

    public static IReadOnlyList<ParsedSegment> Parse(string? text, int startSeq = 1)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var result = new List<ParsedSegment>();
        var seq = startSeq;
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            string? speaker = null;
            var body = line;
            var m = BracketSpeaker().Match(line);
            if (!m.Success) m = ColonSpeaker().Match(line);
            if (m.Success && m.Groups["s"].Value.Length <= MaxSpeakerLength)
            {
                speaker = m.Groups["s"].Value.Trim();
                body = m.Groups["t"].Value.Trim();
            }

            result.Add(new ParsedSegment(seq++, speaker, body));
        }

        return result;
    }
}
