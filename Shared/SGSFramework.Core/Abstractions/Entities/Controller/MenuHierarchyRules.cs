namespace SGSFramework.Core.Abstractions.Entities.Controller;

/// <summary>
/// 選單節點階層規則（新增 / 編輯 / 移動共用；前端選單管理對話框亦依相同規則篩選父級）：
/// <list type="bullet">
/// <item>標題（領域類別）：只能位於頂層</item>
/// <item>區塊（系統類別）：頂層或標題之下</item>
/// <item>群組（功能模組）：只能位於區塊之下</item>
/// <item>頁面（功能）：任意容器（頂層、標題、區塊、群組）之下，不可位於頁面之下</item>
/// </list>
/// </summary>
public static class MenuHierarchyRules
{
    /// <summary>判斷 <paramref name="childType"/> 能否掛在 <paramref name="parentType"/> 之下（null 代表頂層）</summary>
    public static bool IsAllowedParent(MenuType childType, MenuType? parentType) => childType switch
    {
        MenuType.Label => parentType is null,
        MenuType.Section => parentType is null or MenuType.Label,
        MenuType.Group => parentType is MenuType.Section,
        MenuType.Page => parentType is not MenuType.Page,
        _ => false
    };

    /// <summary>違反規則時的說明文字</summary>
    public static string Describe(MenuType childType) => childType switch
    {
        MenuType.Label => "「標題」只能位於頂層（無父級選單）。",
        MenuType.Section => "「區塊」只能位於頂層或「標題」之下。",
        MenuType.Group => "「群組」只能位於「區塊」之下。",
        MenuType.Page => "「頁面」不可位於其他「頁面」之下。",
        _ => "不支援的選單節點類型。"
    };
}
