using Microsoft.EntityFrameworkCore;
using SES.AuditLog.Services;
//using Microsoft.ML;
//using Microsoft.ML.Transforms.TimeSeries;

namespace SES.AuditLog.Services
{

    /// <summary>
    /// 基於 ML.NET 的異常刪除檢測 (Anomaly Detection)
    /// 偵測 「時間序列上的突波 (Spike)」。
    /// 假設我們將每分鐘的刪除操作次數聚合起來，ML.NET 的 IidSpikeEstimator (獨立同分布突波檢測) 非常適合這種場景。
    /// </summary>
    public class AnomalyDetectorService
    {
        //private readonly MLContext _mlContext;
        //private ITransformer _model;
        //private PredictionEngine<AuditMetric, AuditAnomalyPrediction> _engine;

        //public AnomalyDetectorService()
        //{
        //    _mlContext = new MLContext();
        //    TrainModel(); // 初始化模型管線
        //}

        //private void TrainModel()
        //{
        //    // 1. 建立空的 DataView 來定義 Schema (因為這是時間序列，我們會持續餵資料)
        //    var data = new List<AuditMetric>();
        //    var dataView = _mlContext.Data.LoadFromEnumerable(data);

        //    // 2. 定義管線：使用 IidSpikeEstimator
        //    // Confidence: 信心水準 (95% 信心水準)
        //    // PvalueHistoryLength: 用過去多少個點來計算滑動視窗 (例如過去 30 分鐘)
        //    var pipeline = _mlContext.Transforms.DetectIidSpike(
        //        outputColumnName: nameof(AuditAnomalyPrediction.Prediction),
        //        inputColumnName: nameof(AuditMetric.Value),
        //        confidence: 95.0,
        //        pvalueHistoryLength: 30
        //    );

        //    // 3. Fit 模型 (對於 IID Spike，這裡主要是初始化結構)
        //    _model = pipeline.Fit(dataView);
        //    _engine = _mlContext.Model.CreatePredictionEngine<AuditMetric, AuditAnomalyPrediction>(_model);
        //}

        //// 4. 檢測方法 (每分鐘呼叫一次)
        //public void CheckForAnomaly(int deleteCount)
        //{
        //    var input = new AuditMetric { Value = (float)deleteCount };
        //    var result = _engine.Predict(input);

        //    // 解析結果
        //    var isAnomaly = result.Prediction[0] == 1;
        //    var score = result.Prediction[1];
        //    var pValue = result.Prediction[2];

        //    if (isAnomaly)
        //    {
        //        Console.ForegroundColor = ConsoleColor.Red;
        //        Console.WriteLine($"[警報] 偵測到異常刪除量！數值: {deleteCount}, 分數: {score:F2}");
        //        // TODO: 觸發 Email 通知或鎖定帳戶邏輯
        //        Console.ResetColor();
        //    }
        //    else
        //    {
        //        Console.WriteLine($"[正常] 刪除量: {deleteCount} (P-Value: {pValue:F2})");
        //    }
        //}
    }
}

//3.模擬測試
//模擬一個正常的使用場景，突然出現大量刪除。

//C#
//// 模擬程式
//var detector = new AnomalyDetectorService();

//// 1. 暖身期：模擬正常的刪除頻率 (每分鐘 0~5 筆)
//Console.WriteLine("--- 系統學習常態中 ---");
//var random = new Random();
//for (int i = 0; i < 30; i++)
//{
//    detector.CheckForAnomaly(random.Next(0, 5));
//}

//// 2. 攻擊模擬：突然每分鐘刪除 100 筆
//Console.WriteLine("\n--- 模擬攻擊開始 ---");
//detector.CheckForAnomaly(100); // 這應該會觸發 Alert
//detector.CheckForAnomaly(120);