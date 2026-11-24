using System.Text;
using FluentAssertions;
using SGS.Modules.FMS.Application.Validation;
using Xunit;

namespace SGS.Modules.FMS.Tests;

/// <summary>音訊容器簽章檢查（會議助理的會議錄音）：只認檔頭簽章，副檔名與內容必須一致，且須由分類明確開放。</summary>
public class FileSignatureInspectorAudioTests
{
    private static readonly string[] AudioAllowed = [".wav", ".mp3", ".m4a", ".mp4", ".ogg", ".oga", ".flac", ".webm"];
    private readonly FileSignatureInspector _inspector = new();

    private static byte[] Wav() => [.. "RIFF"u8, 0x24, 0x08, 0x00, 0x00, .. "WAVEfmt "u8, 0x10, 0, 0, 0];
    private static byte[] Mp3WithId3() => [.. "ID3"u8, 0x04, 0x00, 0x00, 0x00, 0x00, 0x0F, 0x76, 0x54, 0x49];
    private static byte[] Mp3Frame() => [0xFF, 0xFB, 0x90, 0x64, 0x00, 0x00, 0x00, 0x00];
    private static byte[] Mp4Video() => [0x00, 0x00, 0x00, 0x20, .. "ftypmp42"u8, 0, 0, 0, 0, .. "mp42isom"u8];
    private static byte[] M4a() => [0x00, 0x00, 0x00, 0x20, .. "ftypM4A "u8, 0, 0, 0, 0, .. "isomiso2"u8];
    private static byte[] Ogg() => [.. "OggS"u8, 0x00, 0x02, 0, 0, 0, 0, 0, 0, 0, 0];
    private static byte[] Flac() => [.. "fLaC"u8, 0x00, 0x00, 0x00, 0x22, 0x10, 0x00];
    private static byte[] WebM() => [0x1A, 0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01];

    private Task<FileInspectionResult> InspectAsync(byte[] content, string fileName, string[]? allowed = null) =>
        _inspector.InspectAsync(new MemoryStream(content), fileName, allowed ?? AudioAllowed);

    public static IEnumerable<object[]> ValidAudio() =>
    [
        [Wav(), "meeting.wav", "audio/wav"],
        [Mp3WithId3(), "meeting.mp3", "audio/mpeg"],
        [Mp3Frame(), "meeting.mp3", "audio/mpeg"],
        [M4a(), "meeting.m4a", "audio/mp4"],
        [Mp4Video(), "meeting.mp4", "audio/mp4"],
        [Ogg(), "meeting.ogg", "audio/ogg"],
        [Ogg(), "meeting.oga", "audio/ogg"],
        [Flac(), "meeting.flac", "audio/flac"],
        [WebM(), "meeting.webm", "audio/webm"]
    ];

    [Theory]
    [MemberData(nameof(ValidAudio))]
    public async Task 各種音訊格式的檔頭與副檔名相符時通過(byte[] content, string fileName, string contentType)
    {
        var result = await InspectAsync(content, fileName);

        result.IsValid.Should().BeTrue(result.Error);
        result.Format!.ContentType.Should().Be(contentType);
        result.Format.IsImage.Should().BeFalse("音訊不走圖片重新編碼");
        result.Format.Extension.Should().Be(Path.GetExtension(fileName));
    }

    [Fact]
    public async Task 副檔名大小寫不影響判定()
    {
        (await InspectAsync(Wav(), "MEETING.WAV")).IsValid.Should().BeTrue();
    }

    public static IEnumerable<object[]> Mismatches() =>
    [
        [Wav(), "meeting.mp3"],
        [Mp3WithId3(), "meeting.wav"],
        [Ogg(), "meeting.flac"],
        [Flac(), "meeting.ogg"],
        [M4a(), "meeting.mp3"],
        [WebM(), "meeting.wav"]
    ];

    [Theory]
    [MemberData(nameof(Mismatches))]
    public async Task 內容與副檔名不符時拒絕(byte[] content, string fileName)
    {
        var result = await InspectAsync(content, fileName);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("副檔名不符");
    }

    [Fact]
    public async Task 圖片改名成音訊副檔名會被拒絕()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D];
        (await InspectAsync(png, "meeting.mp3")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task 可執行檔改名成音訊副檔名會被拒絕()
    {
        byte[] pe = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];
        var result = await InspectAsync(pe, "meeting.wav");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("可執行檔");
    }

    [Fact]
    public async Task 純文字改名成音訊副檔名會被拒絕()
    {
        var result = await InspectAsync(Encoding.UTF8.GetBytes("這不是音訊檔，只是一段文字。"), "meeting.mp3");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RIFF容器但不是WAVE時不會被當成WAV()
    {
        // RIFF....AVI  是影片容器，不是音訊
        byte[] avi = [.. "RIFF"u8, 0, 0, 0, 0, .. "AVI LIST"u8];
        (await InspectAsync(avi, "meeting.wav")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task 兩個位元組的假MPEG同步字若層位元不合法則拒絕()
    {
        // 0xFF 0xF1 是 ADTS AAC（層 = 00），不是 MP3
        byte[] adts = [0xFF, 0xF1, 0x50, 0x80, 0x00, 0x1F, 0xFC];
        (await InspectAsync(adts, "meeting.mp3")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task MP4家族但品牌不是音訊常見品牌時拒絕()
    {
        byte[] heic = [0x00, 0x00, 0x00, 0x18, .. "ftypheic"u8, 0, 0, 0, 0];
        (await InspectAsync(heic, "meeting.m4a")).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task 分類沒有開放音訊副檔名時一律拒絕()
    {
        var result = await InspectAsync(Wav(), "meeting.wav", [".pdf", ".png"]);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("不允許的檔案類型");
    }

    [Fact]
    public async Task 空檔案拒絕()
    {
        (await InspectAsync([], "meeting.wav")).IsValid.Should().BeFalse();
    }
}
