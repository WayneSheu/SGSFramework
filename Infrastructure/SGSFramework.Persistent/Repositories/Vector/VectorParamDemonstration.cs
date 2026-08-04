using Microsoft.Data.SqlClient;
using System.Data;

namespace SGSFramework.Persistent.Repositories.Vector
{
    /// <summary>
    /// 針對 SQL Server 2025 高性能查詢參數優化說明模型
    /// </summary>
    public static class VectorParamDemonstration
    {
        /// <summary>
        /// 剖析 limitParam 在 ADO.NET 管線與 SQL 內核中的底層行為
        /// </summary>
        public static void ExplainLimitParameter()
        {
            // 1. 強型別與精確長度宣告：防止 SQL Server 內核將常數誤判為不同長度的型別
            //    使用 SqlDbType.Int 確保參數在傳遞至資料庫時，以原生的 4 Byte 帶號整數 (int) 處理
            int limitValue = 10;// 這裡的 limitValue 可以是任何整數，甚至是來自外部輸入的變數
            var limitParam = new SqlParameter("@Limit", SqlDbType.Int)
            {
                Value = limitValue
            };

            // 2. 緩衝區防禦與性能優化（核心機制）：
            //    在執行如下的原生 SQL 語義檢索或傳統分頁查詢時：
            //    "SELECT TOP (@Limit) * FROM VECTOR_SEARCH(table, column, @Vector, @Limit)"
            //
            //    A. 防止 SQL 注入 (SQL Injection)：
            //       若直接使用字串拼接 (如 $"TOP ({limit})")，一旦數值來源未受信任，將暴露安全性漏洞。
            //       透過 SqlParameter，數值將在通訊協定層 (TDS Protocol) 以 RPC (遠端程序呼叫) 參數隔離傳遞。
            //
            //    B. 促進查詢計畫快取重用 (Query Plan Reuse)：
            //       如果使用硬編碼字串 (例如 TOP (10) 與 TOP (20))，SQL Server 會將其視為兩條完全不同的 SQL 語句，
            //       導致內核必須為每個不同的數量重新執行「編譯」、「優化」與「生成執行計畫」，引發 CPU 劇烈震盪（即 Plan Cache 污染）。
            //       改用 @Limit 參數化後，不論外部傳入 10、100 或 1000，SQL Server 均能精確重用同一個預編譯的 DiskANN 拓撲檢索計畫。
            //
            //    C. 嚴格強型別安全性：
            //       杜絕因 C# 隱式轉換或 Null 狀態不對稱導致的運行期崩潰，完美對齊 C# 10+ 的強型別約束。
        }
    }
}
