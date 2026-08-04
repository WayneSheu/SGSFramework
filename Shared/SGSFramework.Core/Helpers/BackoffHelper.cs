namespace SGSFramework.Core.Helpers
{
    public static class BackoffHelper
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// 計算下一次重試的排程時間
        /// </summary>
        /// <param name="retryCount">目前的重試次數</param>
        /// <param name="baseSeconds">基底秒數 (預設 2 秒)</param>
        /// <returns>預計執行的 UTC 時間</returns>
        public static DateTime CalculateNextRetryTime(int retryCount, int baseSeconds = 2)
        {
            // 限制指數上限，避免數值溢位 (2^10 = 1024秒，約17分鐘)
            int power = Math.Min(retryCount, 10);

            // 指數延遲 (2, 4, 8, 16, 32...)
            double delaySeconds = Math.Pow(baseSeconds, power);

            // 加入 10% ~ 20% 的隨機抖動 (Jitter)，分散資料庫抓取壓力
            double jitter = delaySeconds * (_random.NextDouble() * 0.2);

            return DateTime.UtcNow.AddSeconds(delaySeconds + jitter);
        }
    }
}
