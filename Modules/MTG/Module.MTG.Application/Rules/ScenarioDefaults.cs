using System.Text.RegularExpressions;

namespace SGS.Modules.MTG.Application.Rules;

/// <summary>內建場景模板定義（種子資料）。</summary>
public sealed record ScenarioDefinition(string Code, string DisplayName, string MapPrompt, string ReducePrompt);

/// <summary>
/// 四種內建會議場景的 Map / Reduce 提示詞（取自《企業級全本地端 AI 語音與會議助理》規劃）。
/// 原則：Map 明確列出提取維度，Reduce 給嚴格的 Markdown 骨架；防幻覺約束由摘要引擎統一附加，不放在模板內。
/// </summary>
public static partial class ScenarioDefaults
{
    public const string General = "general";
    public const string ClientDiscovery = "client-discovery";
    public const string ArchitectureReview = "architecture-review";
    public const string WeeklySync = "weekly-sync";

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    public static bool IsValidCode(string? code) => code is not null && CodePattern().IsMatch(code);

    public static IReadOnlyList<ScenarioDefinition> All { get; } =
    [
        new(General, "一般綜合會議",
            "你是一位專業的會議記錄員。請從這段會議逐字稿中提取：(1) 討論的主要議題與重點、(2) 已達成的決議、(3) 待辦事項（含負責人與期限，若有提及）、(4) 尚未解決的問題。請用繁體中文條列輸出，保留關鍵數字、專有名詞與英文術語。",
            """
            請整合以下各時段重點，輸出完整的會議紀要，嚴格依照下列 Markdown 骨架，標題不得更動：

            ## 1. 核心摘要
            （3～5 句話概述會議目的與結論）

            ## 2. 討論重點
            （條列各議題的重點）

            ## 3. 決議事項
            （條列已達成的決議；沒有則寫「未提及」）

            ## 4. 待辦行動清單
            | 待辦事項 | 負責人 | 期限 | 備註 |
            |---|---|---|---|
            （沒有提及負責人或期限時填「未提及」）
            """),

        new(ClientDiscovery, "客戶需求訪談",
            "你是一位資深產品經理。請從這段客戶訪談逐字稿中，精準提取：(1) 客戶目前的業務痛點與現狀、(2) 明確提出的功能或系統需求、(3) 提及的預算範圍、上線時程或合規限制、(4) 客戶表現出的疑慮或反對意見。請保留客戶原話中的關鍵數字與系統名稱，用繁體中文條列輸出。",
            """
            請整合以下各時段重點，輸出客戶需求訪談紀要，嚴格依照下列 Markdown 骨架，標題不得更動：

            ## 1. 客戶背景與核心痛點

            ## 2. 需求規格清單
            | 需求項目 | 優先級 (Must-Have / Nice-to-Have) | 驗收標準或細節 |
            |---|---|---|
            （優先級依客戶語氣判斷；無法判斷時填「待確認」）

            ## 3. 時程、預算與專案限制

            ## 4. 潛在風險與待釐清問題 (Open Questions)
            """),

        new(ArchitectureReview, "技術架構評審",
            "你是一位首席系統架構師。請從這段技術評審會議中，提取：(1) 討論的系統模組與技術選型方案（保留英文專有名詞如 API、DB、K8s、Cache）、(2) 關鍵效能指標（Latency、Throughput、併發數）、(3) 團隊針對架構方案提出的質疑與 Trade-off（權衡取捨）、(4) 最終拍板的技術決策。請客觀精簡記錄，不要遺漏數據。",
            """
            請整合以下各時段重點，輸出技術架構評審紀要，嚴格依照下列 Markdown 骨架，標題不得更動：

            ## 1. 架構目標與核心技術選型

            ## 2. 關鍵架構決策紀錄 (ADR 摘要)
            （每項決策包含：決策內容、採納理由、放棄的替代方案）

            ## 3. 非功能性需求與效能指標 (SLA / 併發 / 資安)

            ## 4. 識別出的技術風險與重構待辦 (Tech Debt & Action Items)
            """),

        new(WeeklySync, "跨部門週會",
            "你是一位專案管理辦公室（PMO）主管。請從這段跨部門週會中，按部門或發言者提取：(1) 本週已完成事項、(2) 進度落後或受阻的項目（Blocker）及原因、(3) 需要其他部門協助或主管拍板的資源請求、(4) 下週預計交付的里程碑。請用繁體中文簡潔條列。",
            """
            請整合以下各時段重點，輸出跨部門週會紀要，嚴格依照下列 Markdown 骨架，標題不得更動：

            ## 1. 專案整體健康度總覽
            （一句話總結進度是否如期）

            ## 2. 各部門進度與卡點追蹤
            | 部門/負責人 | 本週關鍵進展 | 當前卡點 (Blocker) | 需協助事項 |
            |---|---|---|---|

            ## 3. 跨部門協調決議事項

            ## 4. 下週核心里程碑與待辦清單
            """)
    ];
}
