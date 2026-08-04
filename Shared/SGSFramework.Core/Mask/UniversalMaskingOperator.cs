using Serilog.Enrichers.Sensitive;
using System.Collections.Concurrent;

namespace SGSFramework.Core.Mask
{
    /// <summary>
    /// 連結 Serilog 與 MaskingService 的橋樑。它負責在解構字串時進行特徵比對。
    /// </summary>
    public class UniversalMaskingOperator : IMaskingOperator
    {
        private readonly MaskingService _maskService;
        // 快取重複出現的字串判定結果，避免重複計算統編加權
        private static readonly ConcurrentDictionary<string, string> _resultCache = new();
        public UniversalMaskingOperator(MaskingService maskService) => _maskService = maskService;

        public MaskingResult Mask(string input, string mask)
        {
            if (string.IsNullOrWhiteSpace(input) || input.Length < 2)
                return new MaskingResult { Match = false, Result = input };

            if (_resultCache.TryGetValue(input, out var cached))
                return new MaskingResult { Match = true, Result = cached };
            // 1. 執行全域格式掃描 (手機, Email, 信用卡...)
            string result = _maskService.MaskString(input);

            // 2. 特徵識別 (姓名/)
            if (string.Equals(input, result))
            {
                if (_maskService.IsPotentialName(input))
                    result = _maskService.MaskName(input);
            }

            bool isMatched = !string.Equals(input, result);
            if (isMatched && _resultCache.Count < 1000)
                _resultCache.TryAdd(input, result);

            return new MaskingResult { Match = isMatched, Result = result };

        }
    }
}
