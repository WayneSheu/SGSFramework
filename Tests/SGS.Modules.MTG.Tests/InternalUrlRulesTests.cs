using FluentAssertions;
using SGS.Modules.MTG.Application.Rules;
using Xunit;

namespace SGS.Modules.MTG.Tests;

public class InternalUrlRulesTests
{
    [Theory]
    [InlineData("http://localhost:11434")]
    [InlineData("http://127.0.0.1:11434")]
    [InlineData("http://10.1.2.3:11434")]
    [InlineData("http://172.16.0.5:11434")]
    [InlineData("http://192.168.1.10")]
    [InlineData("https://ollama-host:11434")]
    [InlineData("http://[::1]:11434")]
    public void 本機與內網位址允許(string url) => InternalUrlRules.IsAllowed(url).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://localhost")]
    [InlineData("http://8.8.8.8")]
    [InlineData("http://172.32.0.1")]
    [InlineData("https://api.example.com")]
    [InlineData("http://user:pass@localhost")]
    public void 外部或不合法位址拒絕(string? url) => InternalUrlRules.IsAllowed(url).Should().BeFalse("逐字稿不得送往外部");
}
