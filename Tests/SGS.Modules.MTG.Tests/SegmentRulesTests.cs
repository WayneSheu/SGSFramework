using FluentAssertions;
using SGS.Modules.MTG.Application.Rules;
using SGS.Modules.MTG.Application.Services;
using Xunit;

namespace SGS.Modules.MTG.Tests;

public class SegmentRulesTests
{
    private static NewSegment Seg(string? id = "c1", string text = "內容", string? speaker = null, double? start = null, double? end = null) =>
        new(id, speaker, start, end, text);

    // ---------- 桌面端批次驗證 ----------

    [Fact]
    public void 正常批次會整理空白並保留順序()
    {
        var (items, error) = SegmentRules.NormalizeClientBatch([Seg("a", "  第一句 ", " 我方 ", 0, 1), Seg("b", "第二句", "  ", 1, 2)]);

        error.Should().BeNull();
        items.Select(i => (i.ClientSegmentId, i.Text, i.Speaker)).Should().Equal(("a", "第一句", "我方"), ("b", "第二句", null));
    }

    [Fact]
    public void 同批次內相同的用戶端識別只留第一筆()
    {
        var (items, _) = SegmentRules.NormalizeClientBatch([Seg("a", "甲"), Seg("a", "乙"), Seg("b", "丙")]);
        items.Select(i => i.Text).Should().Equal("甲", "丙");
    }

    [Fact]
    public void 空批次或超過上限會被拒絕()
    {
        SegmentRules.NormalizeClientBatch(null).Error.Should().NotBeNull();
        SegmentRules.NormalizeClientBatch([]).Error.Should().NotBeNull();

        var tooMany = Enumerable.Range(0, SegmentRules.MaxBatchSize + 1).Select(i => Seg($"id{i}")).ToList();
        SegmentRules.NormalizeClientBatch(tooMany).Error.Should().Contain("500");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 缺少用戶端識別會被拒絕(string? id) =>
        SegmentRules.NormalizeClientBatch([Seg(id)]).Error.Should().Contain("clientSegmentId");

    [Fact]
    public void 用戶端識別過長會被拒絕() =>
        SegmentRules.NormalizeClientBatch([Seg(new string('x', SegmentRules.MaxClientSegmentIdLength + 1))]).Error.Should().Contain("clientSegmentId");

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void 空文字會被拒絕(string text) =>
        SegmentRules.NormalizeClientBatch([Seg(text: text)]).Error.Should().Contain("文字");

    [Fact]
    public void 文字過長會被拒絕() =>
        SegmentRules.NormalizeClientBatch([Seg(text: new string('字', SegmentRules.MaxTextLength + 1))]).Error.Should().Contain("文字");

    [Fact]
    public void 講者過長會被拒絕() =>
        SegmentRules.NormalizeClientBatch([Seg(speaker: new string('x', SegmentRules.MaxSpeakerLength + 1))]).Error.Should().Contain("講者");

    [Theory]
    [InlineData(-1.0, 2.0)]
    [InlineData(1.0, -2.0)]
    [InlineData(5.0, 2.0)]
    [InlineData(double.NaN, 2.0)]
    [InlineData(1.0, double.PositiveInfinity)]
    public void 不合理的時間會被拒絕(double start, double end) =>
        SegmentRules.NormalizeClientBatch([Seg(start: start, end: end)]).Error.Should().NotBeNull();

    [Fact]
    public void 只有開始或只有結束時間仍可通過() =>
        SegmentRules.NormalizeClientBatch([Seg("a", start: 1.0), Seg("b", end: 2.0)]).Error.Should().BeNull();

    // ---------- 片段建立 ----------

    [Fact]
    public void 建立片段時序號連續_並套用術語校正()
    {
        var meetingId = Guid.NewGuid();
        var audioId = Guid.NewGuid();
        IReadOnlyList<GlossaryTerm> terms = [new("Kubernetes", ["酷伯內提斯"])];

        var (segments, corrected) = SegmentRules.Build(meetingId, 10,
            [Seg("a", "部署到酷伯內提斯", "王經理", 0, 3), Seg("b", "沒有術語", null, 3, 5)], terms, audioId);

        corrected.Should().Be(1);
        segments.Select(s => s.Seq).Should().Equal(10, 11);
        segments[0].Text.Should().Be("部署到酷伯內提斯", "原文保留，校正後文字另存");
        segments[0].CorrectedText.Should().Be("部署到Kubernetes");
        segments[1].CorrectedText.Should().BeNull("沒有被校正時不重複存一份");
        segments.Should().OnlyContain(s => s.MeetingId == meetingId && s.AudioId == audioId);
        (segments[0].StartSeconds, segments[0].EndSeconds, segments[0].Speaker, segments[0].ClientSegmentId).Should().Be((0.0, 3.0, "王經理", "a"));
    }

    [Fact]
    public void 貼上或桌面端的片段沒有來源音檔()
    {
        var (segments, _) = SegmentRules.Build(Guid.NewGuid(), 1, [Seg()], [], audioId: null);
        segments.Single().AudioId.Should().BeNull();
    }

    // ---------- 串流上限 ----------

    [Fact]
    public async Task 串流在上限內完整複製()
    {
        var data = Enumerable.Range(0, 200_000).Select(i => (byte)(i % 251)).ToArray();
        using var dest = new MemoryStream();

        await TranscriptionWorker.CopyWithLimitAsync(new MemoryStream(data), dest, data.Length, CancellationToken.None);

        dest.ToArray().Should().Equal(data);
    }

    [Fact]
    public async Task 串流超過上限時中止並拋出可顯示的錯誤()
    {
        using var dest = new MemoryStream();

        var act = () => TranscriptionWorker.CopyWithLimitAsync(new MemoryStream(new byte[300_000]), dest, 100_000, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("上限");
        dest.Length.Should().BeLessThanOrEqualTo(100_000 + 81920, "超過上限後不應繼續把整個檔案讀進記憶體");
    }
}
