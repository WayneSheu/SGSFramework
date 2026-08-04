using Microsoft.Data.SqlTypes;

namespace SGSFramework.Core.Abstractions.Entities.Vectorizations
{
    /// <summary>
    /// 向量嵌入實體接口，專為 SQL Server 2025 向量索引和語義搜尋優化設計
    /// </summary>
    public interface IEmbeddingEntity
    {
        // 原始摘要：用於顯示結果或讓 LLM 進行二次處理
        // 加入介面後，Worker 在處理不同實體時也能統一記錄日誌或驗證
        string? ProcessedSummary { get; set; }

        // 向量數據：用於資料庫計算相似度
        SqlVector<float> Embedding { get; set; }

        // 追蹤時間
        DateTimeOffset CreatedAt { get; set; }

        // 追蹤原始憑證來源
        string? RawFileLink { get; set; }



    }
}
