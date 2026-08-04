using Microsoft.Extensions.Options;
using SGSFramework.DataProtection.Configurations;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SGSFramework.DataProtection.Handlers
{
    public class HmsSignatureHandler : DelegatingHandler
    {
        private readonly HmsOptions _options;
        public HmsSignatureHandler(IOptions<HmsOptions> options) => _options = options.Value;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var content = request.Content != null ? await request.Content.ReadAsStringAsync(ct) : string.Empty;

            // 自動注入簽章 Header
            var signature = GenerateSignature(content);
            request.Headers.Add("X-HMS-Signature", signature);

            return await base.SendAsync(request, ct);
        }

        private string GenerateSignature(string payload)
        {
            var keyBytes = Encoding.UTF8.GetBytes(_options.SharedSecret);
            var messageBytes = Encoding.UTF8.GetBytes(payload);
            using var hmac = new HMACSHA256(keyBytes);
            return Convert.ToBase64String(hmac.ComputeHash(messageBytes));
        }
    }
}
