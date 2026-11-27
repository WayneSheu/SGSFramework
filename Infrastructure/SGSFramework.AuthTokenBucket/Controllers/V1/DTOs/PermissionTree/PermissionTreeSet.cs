namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionTree;

using System.Collections.Generic;

/// <summary>
/// 同一份權限資料的兩種分組檢視（共用同一個快取項目，選單 / 功能異動時一併失效）
/// </summary>
/// <param name="Legacy">依程式模組分組（ModuleName 為實際模組名稱；實驗室模組授權等沿用）</param>
/// <param name="Menu">依選單群組分組（領域類 → 選單群組 → 子群組 → 功能；排序、標題與側邊選單一致）</param>
public sealed record PermissionTreeSet(List<PermissionModuleDto> Legacy, List<PermissionModuleDto> Menu);
