using System;
using System.Collections.Generic;
using System.Text;

namespace Enterprise.HttpSdk.Interfaces
{

    public interface IApiClient
    {
        // 允許 GET 請求，且回傳特定泛型 TResult 結果
        Task<TResult?> GetAsync<TResult>(string endpoint, CancellationToken cancellationToken = default);

        //  允許傳入泛型 Request Body，並回傳特定泛型 TResult 結果
        Task<TResult?> PostAsync<TRequest, TResult>(string endpoint, TRequest data, CancellationToken cancellationToken = default);
    }

}
