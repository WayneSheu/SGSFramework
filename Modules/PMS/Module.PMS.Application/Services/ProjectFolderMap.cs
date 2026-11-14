using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 交付物歸檔路徑對照（SOP 5.1／5.2）：依生命週期階段與模組推導 `[專案代號]_[專案名稱]/` 下的資料夾，
/// 供人工歸檔對照與前端顯示（附件實體仍在 FMS）。
/// </summary>
public static class ProjectFolderMap
{
    public static string Resolve(ProjectPhase stage, string docType, string? moduleCode, string? moduleName)
    {
        if (!string.IsNullOrWhiteSpace(moduleCode))
            return $"03_開發與技術/04_模組開發/{moduleCode}_{moduleName}";

        var type = docType.Trim().ToUpperInvariant();

        // 具名文件類型優先
        var byType = type switch
        {
            "SRS" or "BRD" or "PRD" => "02_需求與設計/01_商業與系統需求",
            "WIREFRAME" or "UIUX" => "02_需求與設計/02_UI_UX設計",
            "ERD" or "OPENAPI" or "ARCH" or "THREATMODEL" => "02_需求與設計/03_系統架構設計",
            "SAST" or "DAST" or "SIT" or "CROSSVERIFY" => "04_測試與品保/02_測試報告",
            "TESTPLAN" => "04_測試與品保/01_測試計畫與案例",
            "UAT" => "04_測試與品保/03_使用者驗收(UAT)",
            "RELEASECHECK" or "ROLLBACK" => "05_部署與維運/01_上線計畫",
            "RCA" or "MONTHLYREPORT" or "RELEASENOTES" => "05_部署與維運/04_客戶維運紀錄",
            "CR" or "RISK" => "01_專案管理/04_變更與風險",
            _ => null
        };
        if (byType is not null) return byType;

        return stage switch
        {
            ProjectPhase.Initiation => "01_專案管理/01_啟動與規劃",
            ProjectPhase.Requirement => "02_需求與設計/01_商業與系統需求",
            ProjectPhase.Design => "02_需求與設計/03_系統架構設計",
            ProjectPhase.Development => "03_開發與技術/01_開發規範",
            ProjectPhase.Testing => "04_測試與品保/02_測試報告",
            ProjectPhase.DeployClose => "05_部署與維運/01_上線計畫",
            _ => "01_專案管理/01_啟動與規劃"
        };
    }
}
