USE [PhysLIMSDB]
GO

/****** 物件:  View [org].[vw_UserAccessibleLabs]    指令碼日期: 2026/11/18 上午 11:10:27 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER OFF
GO

CREATE   VIEW [org].[vw_UserAccessibleLabs] AS
    WITH Level1Orgs AS (
        SELECT 
            Id,
            TenantLabId,
            Code,
            Name,
            NodePath
        FROM [org].[Organization] WITH (NOLOCK)
        WHERE Level = 1 AND IsActive = 1 AND IsDeleted = 0
    )
    SELECT 
        ul.UserId,
        lab.Id AS LabId,
        lab.TenantLabId AS TenantLabId,
        lab.Code AS LabCode,
        lab.Name AS LabName,
        lab.NodePath AS Path,
        lab.Level AS HierarchyLevel,
        ul.IsPrimary,
        ISNULL(p.Id, lab.Id) AS ParentLabId,
        ISNULL(p.TenantLabId, lab.TenantLabId) AS ParentTenantLabId,
        ISNULL(p.Code, lab.Code) AS ParentLabCode,
        ISNULL(p.Name, lab.Name) AS ParentLabName
    FROM [core].[UserLabMappings] ul WITH (NOLOCK)
    INNER JOIN [org].[Organization] lab WITH (NOLOCK) 
        ON ul.TenantLabId = lab.TenantLabId
    LEFT JOIN Level1Orgs p 
        ON CAST(lab.NodePath AS hierarchyid).IsDescendantOf(CAST(p.NodePath AS hierarchyid)) = 1
    WHERE ul.IsActive = 1 AND lab.IsActive = 1 AND lab.IsDeleted = 0;
GO


