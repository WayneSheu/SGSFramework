namespace SGS.Modules.MAN.Application.Options;

/// <summary>
/// 操作手冊管理（MAN）模組組態（區段 "Manual"）。AI 生成一律使用「本機／內網」的 Ollama，截圖與規格資料不得送往外部服務。
/// </summary>
public sealed class ManOptions
{
    public const string SectionName = "Manual";

    /// <summary>是否啟用 AI 生成（關閉時仍可人工維護手冊主題）。預設關閉，僅於開發或內網環境啟用。</summary>
    public bool Enabled { get; set; }

    /// <summary>Ollama 服務位址；僅允許本機或內網位址（loopback／私有網段／單一標籤主機名）。</summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>看圖寫教學使用的視覺模型。</summary>
    public string VisionModel { get; set; } = "qwen3-vl:8b";

    /// <summary>純文字潤飾使用的模型（保留給後續階段）。</summary>
    public string TextModel { get; set; } = "qwen2.5:14b";

    /// <summary>單次生成逾時（分鐘）。視覺模型在整合式顯示卡上可能需要數分鐘。</summary>
    public int TimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// 模型 context 長度（token）。視覺模型的圖片、提示詞與（thinking 版模型的）思考內容都計入；
    /// 過小時思考內容會吃光額度，導致正文為空。數值越大越吃記憶體。
    /// </summary>
    public int NumCtx { get; set; } = 16384;

    /// <summary>
    /// 單次呼叫的輸出 token 上限（含 thinking 版模型的思考內容）。正常生成約 1,500 token；
    /// 思考失控時在此截斷（done_reason=length）並觸發重試，避免一次卡滿逾時時間。
    /// </summary>
    public int MaxOutputTokens { get; set; } = 4096;

    /// <summary>單一作業的最大嘗試次數（模型輸出為空或無法解析時重試；逾時與連線錯誤不重試）。</summary>
    public int MaxAttempts { get; set; } = 2;

    /// <summary>上傳截圖大小上限（位元組）。</summary>
    public int MaxImageBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>生成佇列容量；佇列滿時拒絕新作業，避免無限堆積。</summary>
    public int QueueCapacity { get; set; } = 20;

    /// <summary>
    /// 提供後端規格上下文的 OpenAPI 文件位址（建議使用 user-manual 文件，例如 https://localhost:7253/openapi/user-manual.json）。
    /// 空白＝不使用規格上下文，僅依截圖生成。
    /// </summary>
    public string? OpenApiUrl { get; set; }
}
