using FluentAssertions;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Application.Services;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

public class DeliverableNamingTests
{
    [Theory]
    [InlineData("20261113_系統需求規格書_v1.0_Alex.pdf")]
    [InlineData("20261113_SRS_v2.1.3_Alex.docx")]
    public void General_Valid(string name) => DeliverableNaming.Check(NamingKind.General, name).Valid.Should().BeTrue();

    [Theory]
    [InlineData("系統需求規格書_v1.0_Alex.pdf")]                 // 缺日期
    [InlineData("20261113_系統需求規格書_Alex.pdf")]             // 缺版本號
    [InlineData("20261113_系統需求規格書_v1.0.pdf")]             // 缺作者
    [InlineData("20261113_系統需求規格書_1.0_Alex.pdf")]         // 版本無 v
    [InlineData("20261113_系統需求規格書_v1.0_Alex")]            // 缺副檔名
    public void General_WrongShape_Invalid(string name) => DeliverableNaming.Check(NamingKind.General, name).Valid.Should().BeFalse();

    [Theory]
    [InlineData("20261113_系統需求規格書_最終版_v1.0_Alex.pdf")]
    [InlineData("20261113_系統需求規格書_v1.0_最新修正.pdf")]
    public void ForbiddenWords_Rejected(string name)
    {
        var result = DeliverableNaming.Check(NamingKind.General, name);
        result.Valid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("禁用字眼"));
    }

    [Fact]
    public void InvalidCalendarDate_Rejected()
    {
        var result = DeliverableNaming.Check(NamingKind.General, "20261340_系統需求規格書_v1.0_Alex.pdf");
        result.Valid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("日期"));
    }

    [Theory]
    [InlineData("20261113_PRJ-001_結帳錯誤畫面.png")]
    [InlineData("20261113_LIMS2-1024_Log 摘要.txt")]
    public void Issue_Valid(string name) => DeliverableNaming.Check(NamingKind.Issue, name).Valid.Should().BeTrue();

    [Theory]
    [InlineData("20261113_結帳錯誤畫面.png")]
    [InlineData("20261113_prj-001_結帳錯誤畫面.png")]
    [InlineData("20261113_PRJ-1_結帳錯誤畫面.png")]
    public void Issue_Invalid(string name) => DeliverableNaming.Check(NamingKind.Issue, name).Valid.Should().BeFalse();

    [Fact]
    public void Module_Valid() =>
        DeliverableNaming.Check(NamingKind.Module, "20261113_M01_會員模組_開發規格_v1.0_Alex.docx").Valid.Should().BeTrue();

    [Theory]
    [InlineData("20261113_會員模組_開發規格_v1.0_Alex.docx")]        // 缺 Mxx
    [InlineData("20261113_M1_會員模組_開發規格_v1.0_Alex.docx")]     // Mxx 位數不符
    public void Module_Invalid(string name) => DeliverableNaming.Check(NamingKind.Module, name).Valid.Should().BeFalse();

    [Fact]
    public void PathPrefix_IsIgnored() =>
        DeliverableNaming.Check(NamingKind.General, @"C:\docs\20261113_系統需求規格書_v1.0_Alex.pdf").Valid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyName_Invalid(string? name) => DeliverableNaming.Check(NamingKind.General, name).Valid.Should().BeFalse();

    [Fact]
    public void FolderMap_ModuleDocs_GoToModuleFolder() =>
        ProjectFolderMap.Resolve(ProjectPhase.Development, "Spec", "M01", "會員模組").Should().Be("03_開發與技術/04_模組開發/M01_會員模組");

    [Theory]
    [InlineData("SRS", "02_需求與設計/01_商業與系統需求")]
    [InlineData("openapi", "02_需求與設計/03_系統架構設計")]
    [InlineData("SAST", "04_測試與品保/02_測試報告")]
    [InlineData("RCA", "05_部署與維運/04_客戶維運紀錄")]
    public void FolderMap_KnownDocTypes(string docType, string expected) =>
        ProjectFolderMap.Resolve(ProjectPhase.Development, docType, null, null).Should().Be(expected);

    [Fact]
    public void FolderMap_UnknownDocType_FallsBackToStage() =>
        ProjectFolderMap.Resolve(ProjectPhase.Initiation, "Other", null, null).Should().Be("01_專案管理/01_啟動與規劃");
}
