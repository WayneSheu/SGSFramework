using FluentAssertions;
using SGS.Modules.FMS.Application.Services;
using SGSFramework.Core.FileManagement;
using Xunit;

namespace SGS.Modules.FMS.Tests;

public class StorageFolderTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("/", null)]
    [InlineData("reports/FOOD", "reports/FOOD")]
    [InlineData("/reports/FOOD/", "reports/FOOD")]
    [InlineData(@"reports\lab_A\ROHS-2", "reports/lab_A/ROHS-2")]
    public void Valid_folders_are_normalized(string? input, string? expected)
    {
        FileManagementService.NormalizeFolder(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("reports/../../etc")]
    [InlineData("reports//FOOD")]
    [InlineData("reports/.hidden")]
    [InlineData("reports/食品")]
    [InlineData("C:/windows")]
    [InlineData("reports/a b")]
    public void Path_traversal_and_invalid_characters_are_rejected(string input)
    {
        var act = () => FileManagementService.NormalizeFolder(input);

        act.Should().Throw<FileRejectedException>();
    }

    [Fact]
    public void Folder_longer_than_200_characters_is_rejected()
    {
        var act = () => FileManagementService.NormalizeFolder(new string('a', 201));

        act.Should().Throw<FileRejectedException>();
    }
}
