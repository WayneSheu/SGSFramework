using System.Text.Json;
using FluentAssertions;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Infrastructure.Entities;
using Xunit;

namespace SGS.Modules.PMS.Tests;

/// <summary>API 規格 1.1：列舉一律以字串傳遞（Host 未全域註冊 JsonStringEnumConverter，故以屬性標註）。</summary>
public class EnumJsonTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Enums_SerializeAsStrings()
    {
        var dto = new ProjectGateDto(GateKind.Build, GateStatus.Approved, null, null, null);
        var json = JsonSerializer.Serialize(dto, Web);
        json.Should().Contain("\"gate\":\"Build\"").And.Contain("\"status\":\"Approved\"");
    }

    [Fact]
    public void Request_AcceptsStringEnums_IncludingNullableFilter()
    {
        var request = JsonSerializer.Deserialize<ProjectSearchRequest>(
            """{"pageIndex":2,"filter":{"stage":"Sla","phase":"Development"}}""", Web)!;
        request.PageIndex.Should().Be(2);
        request.Filter!.Stage.Should().Be(ProjectStage.Sla);
        request.Filter.Phase.Should().Be(ProjectPhase.Development);
        request.Filter.Status.Should().BeNull();
    }

    [Fact]
    public void CreateRequest_ParsesStage() =>
        JsonSerializer.Deserialize<CreateProjectRequest>(
            """{"code":"PRJ","name":"x","stage":"Uat","pmUserId":"00000000-0000-0000-0000-000000000001","techLeadUserId":"00000000-0000-0000-0000-000000000002"}""", Web)!
            .Stage.Should().Be(ProjectStage.Uat);
}
