using FluentAssertions;
using SGS.Modules.MAN.Application.Rules;
using Xunit;

namespace SGS.Modules.MAN.Tests;

public sealed class GenerationRulesTests
{
    [Theory]
    [InlineData("http://localhost:11434", true)]
    [InlineData("http://LOCALHOST:11434/", true)]
    [InlineData("http://127.0.0.1:11434", true)]
    [InlineData("http://[::1]:11434", true)]
    [InlineData("http://10.1.2.3:11434", true)]
    [InlineData("http://172.16.0.5", true)]
    [InlineData("http://172.31.255.1", true)]
    [InlineData("http://192.168.1.20:11434", true)]
    [InlineData("http://169.254.10.10", true)]
    [InlineData("http://ollama-host:11434", true)]
    [InlineData("https://localhost:7253/openapi/user-manual.json", true)]
    [InlineData("http://172.32.0.1", false)]
    [InlineData("http://172.15.0.1", false)]
    [InlineData("http://8.8.8.8:11434", false)]
    [InlineData("http://[2001:db8::1]", false)]
    [InlineData("https://api.example.com", false)]
    [InlineData("http://ollama.corp.example.com", false)]
    [InlineData("ftp://localhost", false)]
    [InlineData("http://user:pw@localhost:11434", false)]
    [InlineData("localhost:11434", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Ollama位址只允許本機或內網(string? url, bool expected) =>
        GenerationRules.IsAllowedOllamaUrl(url).Should().Be(expected);

    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];
    private static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0, 0];
    private static byte[] Webp() => "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();

    [Fact]
    public void 以檔案標頭辨識圖片格式()
    {
        GenerationRules.DetectImageType(Png()).Should().Be("image/png");
        GenerationRules.DetectImageType(Jpeg()).Should().Be("image/jpeg");
        GenerationRules.DetectImageType(Webp()).Should().Be("image/webp");
        GenerationRules.DetectImageType("GIF89a......"u8.ToArray()).Should().BeNull();
        GenerationRules.DetectImageType("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray()).Should().BeNull();
        GenerationRules.DetectImageType([]).Should().BeNull();
    }

    [Fact]
    public void 驗證截圖_空檔_過大_格式不支援()
    {
        GenerationRules.ValidateImage([], 1024).Should().NotBeNull();
        GenerationRules.ValidateImage(Png(), 5).Should().NotBeNull();
        GenerationRules.ValidateImage("not an image at all"u8.ToArray(), 1024).Should().NotBeNull();
        GenerationRules.ValidateImage(Png(), 1024).Should().BeNull();
    }

    [Fact]
    public void 驗證作業欄位()
    {
        GenerationRules.ValidateRequest("export_report", "匯出報表", null).Should().BeNull();
        GenerationRules.ValidateRequest("Bad Key", "匯出報表", null).Should().NotBeNull();
        GenerationRules.ValidateRequest("export_report", " ", null).Should().NotBeNull();
        GenerationRules.ValidateRequest("export_report", "匯出報表", new string('x', 51)).Should().NotBeNull();
    }
}

public sealed class ManualOutputParserTests
{
    [Fact]
    public void 解析標準輸出()
    {
        var result = ManualOutputParser.TryParse(
            """{"tooltip":"匯出報表","steps_markdown":"1. 點擊 **匯出**","precautions":"需有權限"}""", out var error);

        error.Should().BeNull();
        result.Should().NotBeNull();
        result!.Tooltip.Should().Be("匯出報表");
        result.StepsMarkdown.Should().Contain("**匯出**");
        result.Precautions.Should().Be("需有權限");
    }

    [Fact]
    public void 容忍程式碼區塊與前後說明()
    {
        var raw = "好的，以下是結果：\n```json\n{\"tooltip\":\"提示\",\"steps_markdown\":\"1. 步驟\",\"precautions\":\"\"}\n```\n希望有幫助";
        var result = ManualOutputParser.TryParse(raw, out _);

        result.Should().NotBeNull();
        result!.Precautions.Should().Be("無");
    }

    [Fact]
    public void 步驟為陣列時轉成編號列表()
    {
        var result = ManualOutputParser.TryParse(
            """{"tooltip":"提示","steps_markdown":["點擊 **新增**","填寫名稱","3. 按下儲存"],"precautions":["注意一","注意二"]}""", out _);

        result.Should().NotBeNull();
        result!.StepsMarkdown.Should().Contain("1. 點擊 **新增**").And.Contain("2. 填寫名稱").And.Contain("3. 按下儲存");
        result.Precautions.Should().Contain("1. 注意一");
    }

    [Fact]
    public void 提示超過30字會截斷且不超過上限()
    {
        var result = ManualOutputParser.TryParse(
            $$"""{"tooltip":"{{new string('字', 80)}}","steps_markdown":"1. 步驟","precautions":"無"}""", out _);

        result.Should().NotBeNull();
        result!.Tooltip.Length.Should().Be(30);
        result.Tooltip.Should().EndWith("…");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("這不是 JSON")]
    [InlineData("{壞掉的 json")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"tooltip":"提示","steps_markdown":"  "}""")]
    [InlineData("""{"tooltip":"","steps_markdown":"1. 步驟"}""")]
    [InlineData("""{"steps_markdown":"1. 步驟"}""")]
    public void 無法解析或缺少必要欄位時回傳錯誤(string? raw)
    {
        var result = ManualOutputParser.TryParse(raw, out var error);

        result.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
    }
}

public sealed class ManualPromptBuilderTests
{
    [Fact]
    public void 提示詞包含功能資訊與輸出格式要求()
    {
        var prompt = ManualPromptBuilder.BuildUserPrompt("匯出報表", "export_report", "財務", null);

        prompt.Should().Contain("匯出報表").And.Contain("export_report").And.Contain("財務")
            .And.Contain("tooltip").And.Contain("steps_markdown").And.Contain("precautions");
        prompt.Should().NotContain("後端系統規格");
    }

    [Fact]
    public void 有規格上下文時附上()
    {
        var prompt = ManualPromptBuilder.BuildUserPrompt("建立訂單", "create_order", null, "API 路徑：/api/v1/orders");

        prompt.Should().Contain("後端系統規格").And.Contain("/api/v1/orders");
    }

    [Fact]
    public void 系統提示強調不得編造()
    {
        ManualPromptBuilder.SystemPrompt.Should().Contain("不要編造");
    }
}

public sealed class OpenApiContextFilterTests
{
    private const string Doc = """
        {"paths":{"/api/v1/orders":{"post":{"summary":"建立訂單"}},"/api/v1/customers":{"get":{"summary":"客戶"}}},
         "components":{"schemas":{"CreateOrderRequest":{"required":["taxId"]},"CustomerDto":{"type":"object"}}}}
        """;

    [Fact]
    public void 依關鍵字挑出路徑與模型()
    {
        var context = OpenApiContextFilter.Build(Doc, "order");

        context.Should().Contain("/api/v1/orders").And.Contain("CreateOrderRequest");
        context.Should().NotContain("customers").And.NotContain("CustomerDto");
    }

    [Fact]
    public void 超過長度會截斷()
    {
        var bigDoc = "{\"paths\":{\"/api/v1/orders\":{\"x\":\"" + new string('a', 9000) + "\"}}}";

        OpenApiContextFilter.Build(bigDoc, "orders", 500).Length.Should().BeLessThanOrEqualTo(500);
    }

    [Theory]
    [InlineData(null, "order")]
    [InlineData("", "order")]
    [InlineData("not json", "order")]
    [InlineData(Doc, "")]
    [InlineData(Doc, null)]
    [InlineData(Doc, "nomatch")]
    public void 無法取得時回傳空字串(string? json, string? keyword) =>
        OpenApiContextFilter.Build(json, keyword).Should().BeEmpty();
}
