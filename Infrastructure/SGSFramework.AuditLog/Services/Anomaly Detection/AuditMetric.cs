using System;
using System.Collections.Generic;
using System.Text;

//using Microsoft.ML.Data;

namespace SES.AuditLog.Services.Anomaly_Detection
{
    public class AuditMetric
    {
        // 時間序列雖然重要，但在 IID 算法中，我們主要關注數值的序列變化
        public float Value { get; set; } // 例如：每分鐘的 Delete 次數
    }

    public class AuditAnomalyPrediction
    {
        //[VectorType(3)]
        public double[] Prediction { get; set; }
        // Prediction[0] = Alert (1 = 異常, 0 = 正常)
        // Prediction[1] = Raw Score
        // Prediction[2] = P-Value (機率值)
    }
}
