using System.Text;

namespace SGS.Modules.MAN.Application.Rules;

/// <summary>組合「看圖寫教學」的提示詞（純函式）：嚴格依據截圖與規格，不得編造未出現的按鈕或規則。</summary>
public static class ManualPromptBuilder
{
    public const string SystemPrompt =
        """
        你是一位資深的軟體技術文件撰寫專家（Technical Writer），為終端用戶撰寫清晰、易懂的繁體中文操作教學。
        寫作規範：
        1. 語氣親切、專業，使用「您」稱呼用戶，避免工程師術語。
        2. 介面上的按鈕、選單或欄位名稱一律使用粗體標示，例如：**儲存設定**。
        3. 操作步驟使用編號列表（1. 2. 3.），每個步驟只包含一個核心動作。
        4. 容易出錯或需要留意的限制，寫在注意事項中。
        5. 嚴格依據提供的截圖與規格撰寫；畫面或規格中沒有的功能、按鈕、欄位，絕對不要編造。看不清楚的文字請略過，不要猜測。
        """;

    /// <summary>建立使用者提示詞；<paramref name="apiContext"/> 為已過濾與截斷的後端規格（可為空）。</summary>
    public static string BuildUserPrompt(string title, string featureKey, string? moduleName, string? apiContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(featureKey);

        var sb = new StringBuilder();
        sb.AppendLine($"請觀察這張軟體功能「{title.Trim()}」（代碼：{featureKey.Trim()}）的介面截圖，撰寫用戶操作教學。");
        if (!string.IsNullOrWhiteSpace(moduleName)) sb.AppendLine($"此功能屬於「{moduleName.Trim()}」模組。");

        if (!string.IsNullOrWhiteSpace(apiContext))
        {
            sb.AppendLine();
            sb.AppendLine("【後端系統規格（包含必填欄位、字數限制與錯誤說明，請據此補充注意事項）】");
            sb.AppendLine(apiContext.Trim());
        }

        sb.AppendLine();
        sb.AppendLine("請嚴格只輸出一個 JSON 物件，不要包含其他文字，欄位如下：");
        sb.AppendLine("""
            {
              "tooltip": "給畫面內嵌氣泡提示用的超簡短說明，30 字以內，說明這個功能的核心用途",
              "steps_markdown": "詳細操作步驟（繁體中文 Markdown 編號列表，按鈕與欄位名稱加粗，每行一個步驟）",
              "precautions": "容易出錯的注意事項（Markdown 條列；若沒有可寫「無」）"
            }
            """);
        return sb.ToString();
    }
}
