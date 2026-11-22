using System.Text;
using Microsoft.Extensions.Options;
using SGS.Modules.MTG.Application.Options;
using SGS.Modules.MTG.Application.Rules;

namespace SGS.Modules.MTG.Application.Services;

/// <summary>摘要所需的模板（Map 指令與 Reduce 骨架）。</summary>
public sealed record SummaryTemplate(string MapPrompt, string ReducePrompt);

/// <summary>
/// 超長會議 Map-Reduce 摘要：先切塊，逐塊 Map 提取重點，再 Reduce 成固定骨架的 Markdown 紀要。
/// 各時段重點合併後仍過長時，先分批濃縮再 Reduce（有層數上限，避免無窮迴圈）。
/// </summary>
public sealed class MapReduceSummarizer(ITextGenerator generator, IOptionsMonitor<MtgOptions> options)
{
    /// <summary>所有提示詞共用的防幻覺約束。</summary>
    public const string Guard =
        "重要規則：只能依據提供的逐字稿內容整理，不得編造未提及的數字、人名、日期或承諾；資訊不足時寫「未提及」。請使用繁體中文，保留英文專有名詞。";

    private const int MaxCondenseRounds = 4;

    public async Task<string> SummarizeAsync(string transcript, SummaryTemplate template, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(template);
        var opt = options.CurrentValue;

        var chunks = TranscriptChunker.Chunk(transcript, opt.ChunkChars, opt.OverlapChars);
        if (chunks.Count == 0) throw new InvalidOperationException("逐字稿為空，無法產生紀要。");

        // Map：逐塊提取重點（依序執行，避免同時佔用多份模型 context）
        var notes = new List<string>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prompt = $"{Guard}\n\n{template.MapPrompt}\n\n【逐字稿片段 {i + 1}/{chunks.Count}】\n{chunks[i]}";
            var output = (await generator.GenerateAsync(prompt, cancellationToken).ConfigureAwait(false)).Trim();
            notes.Add(string.IsNullOrWhiteSpace(output) ? "（此時段未提取到重點）" : output);
        }

        // 合併後過長時分批濃縮
        for (var round = 0; round < MaxCondenseRounds && Combine(notes).Length > opt.ChunkChars; round++)
        {
            var batches = Batch(notes, opt.ChunkChars);
            if (batches.Count >= notes.Count) break; // 無法再合併（單一重點已超過上限），直接進入 Reduce

            var condensed = new List<string>(batches.Count);
            foreach (var batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var prompt = $"{Guard}\n\n請將以下多個時段的重點合併、去除重複，保留所有數字、決議與待辦，輸出條列重點：\n\n{Combine(batch)}";
                var output = (await generator.GenerateAsync(prompt, cancellationToken).ConfigureAwait(false)).Trim();
                condensed.Add(string.IsNullOrWhiteSpace(output) ? Combine(batch) : output);
            }

            notes = condensed;
        }

        // Reduce：整合為最終紀要
        var reducePrompt = $"{Guard}\n\n{template.ReducePrompt}\n\n【各時段重點】\n{Combine(notes)}";
        var minutes = (await generator.GenerateAsync(reducePrompt, cancellationToken).ConfigureAwait(false)).Trim();
        minutes = StripMarkdownFence(minutes);
        if (string.IsNullOrWhiteSpace(minutes)) throw new InvalidOperationException("模型未產生紀要內容，請重新產生。");
        return minutes;
    }

    private static string Combine(IEnumerable<string> notes)
    {
        var sb = new StringBuilder();
        var i = 1;
        foreach (var note in notes)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append("### 時段 ").Append(i++).Append('\n').Append(note);
        }
        return sb.ToString();
    }

    private static List<List<string>> Batch(List<string> notes, int maxChars)
    {
        var batches = new List<List<string>>();
        var current = new List<string>();
        var length = 0;
        foreach (var note in notes)
        {
            if (current.Count > 0 && length + note.Length > maxChars)
            {
                batches.Add(current);
                current = [];
                length = 0;
            }
            current.Add(note);
            length += note.Length;
        }
        if (current.Count > 0) batches.Add(current);
        return batches;
    }

    /// <summary>模型常把整份輸出包在 ```markdown 圍欄內；去除最外層圍欄，保留內容。</summary>
    public static string StripMarkdownFence(string text)
    {
        var t = text.Trim();
        if (!t.StartsWith("```", StringComparison.Ordinal)) return t;

        var firstNewline = t.IndexOf('\n');
        if (firstNewline < 0) return t;
        var inner = t[(firstNewline + 1)..];
        if (inner.EndsWith("```", StringComparison.Ordinal)) inner = inner[..^3];
        return inner.Trim();
    }
}
