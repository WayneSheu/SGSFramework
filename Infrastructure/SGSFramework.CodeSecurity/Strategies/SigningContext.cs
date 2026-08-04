using SGSFramework.CodeSecurity.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Strategies
{
    public class SigningContext
    {
        private readonly ISigningStrategy _strategy;

        public SigningContext(ISigningStrategy strategy)
            => _strategy = strategy;

        public async Task<SigningResult> ExecuteSigningAsync(string path, string? tsUrl)
        {
            // 統一錯誤防護
            try { return await _strategy.SignAsync(path, tsUrl); }
            catch (Exception ex) { return SigningResult.CreateFailure(path, ex.Message); }
        }
    }
}
