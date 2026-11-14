using System.Text.Json.Serialization;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>專案生命週期六階段（SOP 3.2）。數值順序即推進順序。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectPhase
{
    Initiation = 1,
    Requirement = 2,
    Design = 3,
    Development = 4,
    Testing = 5,
    DeployClose = 6
}

/// <summary>專案所處期別：UAT 驗收期／SLA 維運期（SOP 5.4）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectStage
{
    Uat = 0,
    Sla = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectStatus
{
    Active = 0,
    Closed = 1,
    Voided = 2
}

/// <summary>RACI 角色（SOP 2.2）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RaciRole
{
    Pm = 1,
    TechLead = 2,
    Be = 3,
    Fe = 4,
    Stakeholder = 5
}

/// <summary>SSDLC 四道強制卡控節點（SOP 3.3）。數值順序即放行順序。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GateKind
{
    Design = 1,
    Build = 2,
    Verification = 3,
    Release = 4
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GateStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>專案代號保留狀態：核發後永不重用。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProjectCodeStatus
{
    Reserved = 0,
    Active = 1,
    Retired = 2
}

/// <summary>檔名命名類型（SOP 5.5）。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NamingKind
{
    General = 0,
    Issue = 1,
    Module = 2
}
