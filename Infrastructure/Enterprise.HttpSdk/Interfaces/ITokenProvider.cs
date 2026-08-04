using System;
using System.Collections.Generic;
using System.Text;

namespace Enterprise.HttpSdk.Interfaces
{
    public interface ITokenProvider
    {
        // 讓套件內部的 DelegatingHandler 呼叫，不關心 Token 來自 LocalStorage 還是 Redis
        Task<string?> GetTokenAsync(CancellationToken cancellationToken = default);
    }
}
