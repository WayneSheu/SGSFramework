namespace SGS.Modules.MTG.Application.Options;

/// <summary>
/// 會議助理（MTG）模組組態（區段 "Meeting"）。紀要生成一律使用「本機／內網」的 Ollama，逐字稿不得送往外部服務。
/// </summary>
public sealed class MtgOptions
{
    public const string SectionName = "Meeting";

    /// <summary>是否啟用 AI 紀要生成（關閉時仍可維護會議與逐字稿）。預設關閉。</summary>
    public bool Enabled { get; set; }

    /// <summary>Ollama 服務位址；僅允許本機或內網位址。</summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>摘要使用的文字模型。</summary>
    public string TextModel { get; set; } = "qwen3:14b";

    /// <summary>單次模型呼叫逾時（分鐘）。</summary>
    public int TimeoutMinutes { get; set; } = 10;

    /// <summary>模型 context 長度（token）；規劃預設 6144，以控制共享記憶體用量。</summary>
    public int NumCtx { get; set; } = 6144;

    /// <summary>單次輸出 token 上限。</summary>
    public int MaxOutputTokens { get; set; } = 2048;

    /// <summary>逐字稿切塊的字元上限。</summary>
    public int ChunkChars { get; set; } = 4500;

    /// <summary>相鄰切塊的前後文重疊字元數。</summary>
    public int OverlapChars { get; set; } = 300;

    /// <summary>單一會議逐字稿的字元數上限（貼上或寫入時檢查）。</summary>
    public int MaxTranscriptChars { get; set; } = 2_000_000;

    /// <summary>紀要生成佇列容量；滿了拒絕新作業。</summary>
    public int QueueCapacity { get; set; } = 10;

    /// <summary>
    /// 會議錄音保存天數：已轉錄（或轉錄失敗）超過此天數的錄音由排程從檔案管理刪除，逐字稿與紀要保留。0 = 不自動刪除（預設）。
    /// 設定大於 0 時排程器才會註冊「MTG 過期音檔清除」作業（mtg-audio-retention）。
    /// </summary>
    public int AudioRetentionDays { get; set; }

    /// <summary>語音轉錄服務設定（區段 "Meeting:Whisper"）。</summary>
    public WhisperOptions Whisper { get; set; } = new();
}

/// <summary>
/// 語音轉錄服務（區段 "Meeting:Whisper"）。伺服器只呼叫本機／內網的 HTTP 轉錄服務，不內嵌原生推論程式庫
/// （外掛的第三方原生套件會被簽章檢查隔離）。協定採 OpenAI 相容的 <c>POST /v1/audio/transcriptions</c>
/// （whisper.cpp server 以 <c>--inference-path /v1/audio/transcriptions</c>、faster-whisper-server、Speaches、LocalAI 皆支援）。
/// </summary>
public sealed class WhisperOptions
{
    /// <summary>是否啟用音檔轉錄（關閉時仍可貼上逐字稿，但不能上傳音檔）。預設關閉。</summary>
    public bool Enabled { get; set; }

    /// <summary>轉錄服務位址；僅允許本機或內網位址。</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>轉錄端點路徑。</summary>
    public string Path { get; set; } = "/v1/audio/transcriptions";

    /// <summary>模型名稱（OpenAI 相容協定的 model 欄位；whisper.cpp 會忽略，faster-whisper-server 等需填實際模型）。</summary>
    public string Model { get; set; } = "whisper-1";

    /// <summary>語言代碼（例如 zh、en）；空白＝由服務自動偵測。中英夾雜的會議建議指定 zh。</summary>
    public string? Language { get; set; } = "zh";

    /// <summary>
    /// 單次轉錄逾時（分鐘）。實測 large-v3-turbo q5_0 在純 CPU 約為音檔長度的 1.4 倍（17.8 秒音檔 25 秒），
    /// 一小時的會議需要約 85 分鐘；請設為預期最長音檔的 2 倍以上（預設 180 分鐘，約可處理 2 小時的錄音）。
    /// </summary>
    public int TimeoutMinutes { get; set; } = 180;

    /// <summary>音檔大小上限（位元組）：轉錄時整檔讀入記憶體（不落地）後送出，須小於 FMS 分類 meeting-audio 的上限。</summary>
    public long MaxAudioBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>轉錄佇列容量；滿了拒絕新作業。</summary>
    public int QueueCapacity { get; set; } = 10;
}
