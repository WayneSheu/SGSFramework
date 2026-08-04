using SGSFramework.CodeSecurity.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.CodeSecurity.Services.Signings
{
    /// <summary>
    /// 定義簽署服務介面
    /// </summary>
    public interface ISigningService
    {
        /// <summary>
        /// 簽署指定的檔案，並可選擇提供時間戳記 URL
        /// </summary>
        /// <param name="artifactPath"></param>
        /// <param name="timestampUrl"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<SigningResult> SignArtifactAsync(string artifactPath, string? timestampUrl = null, CancellationToken ct = default);


    }
}
