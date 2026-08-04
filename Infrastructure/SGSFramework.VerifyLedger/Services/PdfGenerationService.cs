using RazorLight;
using SGSFramework.VerifyLedger.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.VerifyLedger.Services
{
    public class PdfGenerationService
    {
        public async Task<string> GenerateHtmlAsync(LedgerVerificationResult report)
        {
            var engine = new RazorLightEngineBuilder()
                .UseEmbeddedResourcesProject(typeof(PdfGenerationService))
                .Build();

            // 使用預先定義好的 Razor (.cshtml) 模板
            string template = await System.IO.File.ReadAllTextAsync("Templates/LedgerReport.cshtml");
            return await engine.CompileRenderStringAsync("report", template, report);
        }
    }
}
