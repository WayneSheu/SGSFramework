using Serilog.Enrichers.Sensitive;

namespace SGSFramework.Core.Mask
{
    /// <summary>
    /// 連結 Serilog 與 MaskingService 的橋樑。它負責在解構字串時進行特徵比對。
    /// </summary>
    public class UniversalMaskingOperator : IMaskingOperator
    {
        private readonly MaskingService _maskService;
        // 注意：刻意不快取判定結果 —— 快取鍵即為原始明文 (個資、Token)，會常駐於記憶體 (違反 ISO 27001 A.8.12 最小保留)
        public UniversalMaskingOperator(MaskingService maskService) => _maskService = maskService;

        public MaskingResult Mask(string input, string mask)
        {
            if (string.IsNullOrWhiteSpace(input) || input.Length < 2)
                return new MaskingResult { Match = false, Result = input };

            // 1. 執行全域格式掃描 (金鑰, 手機, Email, 信用卡...)
            string result = _maskService.MaskString(input);

            // 2. 特徵識別 (姓名/)
            if (string.Equals(input, result))
            {
                if (_maskService.IsPotentialName(input))
                    result = _maskService.MaskName(input);
            }

            bool isMatched = !string.Equals(input, result);

            return new MaskingResult { Match = isMatched, Result = result };

        }
    }
}
