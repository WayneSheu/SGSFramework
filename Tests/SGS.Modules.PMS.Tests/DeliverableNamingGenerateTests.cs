using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class DeliverableNamingGenerateTests
{
    private static readonly DateOnly Day = new(2026, 11, 14);

    [Fact]
    public void General_ProducesStandardName()
    {
        var r = DeliverableNaming.Generate(Day, "系統需求規格書", "v1.0", "Alex", ".pdf");
        r.Success.Should().BeTrue();
        r.FileName.Should().Be("20261114_系統需求規格書_v1.0_Alex.pdf");
    }

    [Fact]
    public void Module_ProducesModuleName()
    {
        var r = DeliverableNaming.Generate(Day, "開發規格", "v1.0", "Alex", "docx", "m01", "會員模組");
        r.Success.Should().BeTrue();
        r.FileName.Should().Be("20261114_M01_會員模組_開發規格_v1.0_Alex.docx");
    }

    [Theory]
    [InlineData("1.0", "v1.0")]
    [InlineData("V2.1", "v2.1")]
    [InlineData("v1.2.3", "v1.2.3")]
    [InlineData(" v3.0 ", "v3.0")]
    public void Version_IsNormalized(string input, string expected) =>
        DeliverableNaming.NormalizeVersion(input).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v1")]
    [InlineData("v1.0.0.0")]
    [InlineData("final")]
    [InlineData("v1.a")]
    public void Version_Invalid_ReturnsNull(string? input) => DeliverableNaming.NormalizeVersion(input).Should().BeNull();

    [Fact]
    public void Generate_RejectsInvalidVersion() =>
        DeliverableNaming.Generate(Day, "SRS", "latest", "Alex", ".pdf").Success.Should().BeFalse();

    [Theory]
    [InlineData("需求規格書_最終版")]
    [InlineData("最新修正的設計")]
    public void Generate_RejectsForbiddenWordsInDocName(string docName)
    {
        var r = DeliverableNaming.Generate(Day, docName, "v1.0", "Alex", ".pdf");
        r.Success.Should().BeFalse();
        r.Error.Should().Contain("禁用字眼");
    }

    [Fact]
    public void Underscores_AndWhitespace_BecomeHyphens()
    {
        var r = DeliverableNaming.Generate(Day, "系統 需求__規格　書", "v1.0", "Alex", ".pdf");
        r.FileName.Should().Be("20261114_系統-需求-規格-書_v1.0_Alex.pdf");
    }

    [Fact]
    public void PathAndInvalidChars_AreStripped()
    {
        var r = DeliverableNaming.Generate(Day, @"..\..\evil/SRS:*?""<>|", "v1.0", "Alex", ".pdf");
        r.Success.Should().BeTrue();
        r.FileName.Should().NotContainAny("\\", "/", ":", "*", "?", "\"", "<", ">", "|");
        DeliverableNaming.Check(NamingKind.General, r.FileName!).Valid.Should().BeTrue();
    }

    [Fact]
    public void Author_EmailDomain_IsDropped()
    {
        var r = DeliverableNaming.Generate(Day, "SRS", "v1.0", "sysadmin@sgs.local", ".pdf");
        r.FileName.Should().EndWith("_sysadmin.pdf");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyAuthor_FallsBackToUnknown(string? author) =>
        DeliverableNaming.Generate(Day, "SRS", "v1.0", author, ".pdf").FileName.Should().EndWith("_unknown.pdf");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pdf!")]
    [InlineData(".")]
    public void Generate_RejectsBadExtension(string? ext) =>
        DeliverableNaming.Generate(Day, "SRS", "v1.0", "Alex", ext).Success.Should().BeFalse();

    [Fact]
    public void Extension_IsLowercased() =>
        DeliverableNaming.Generate(Day, "SRS", "v1.0", "Alex", ".PDF").FileName.Should().EndWith(".pdf");

    [Fact]
    public void EmptyDocName_Rejected() =>
        DeliverableNaming.Generate(Day, "  ", "v1.0", "Alex", ".pdf").Success.Should().BeFalse();

    [Fact]
    public void TooLongName_Rejected() =>
        DeliverableNaming.Generate(Day, new string('字', 300), "v1.0", "Alex", ".pdf").Error.Should().Contain("過長");

    [Fact]
    public void Module_WithEmptyModuleName_Rejected() =>
        DeliverableNaming.Generate(Day, "SRS", "v1.0", "Alex", ".pdf", "M01", " ").Success.Should().BeFalse();

    [Fact]
    public void GeneratedNames_AlwaysPassCheck()
    {
        foreach (var (doc, ver, who) in new[] { ("API 規格", "1.0", "wayne"), ("ERD", "v2.3.1", "a.b"), ("測試報告", "V1.0", "x@y.z") })
        {
            var r = DeliverableNaming.Generate(Day, doc, ver, who, ".docx");
            r.Success.Should().BeTrue();
            DeliverableNaming.Check(NamingKind.General, r.FileName!).Valid.Should().BeTrue(r.FileName);
        }
    }
}
