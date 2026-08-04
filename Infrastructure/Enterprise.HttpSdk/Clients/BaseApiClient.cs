using Enterprise.HttpSdk.Exceptions;
using Enterprise.HttpSdk.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;

namespace Enterprise.HttpSdk.Clients
{
    //// 為 API 用戶端提供通用功能的基底類別。
    //public class BaseApiClient : IApiClient
    //{
    //    private readonly HttpClient _httpClient;
    //    private readonly ILogger<BaseApiClient> _logger;

    //    public BaseApiClient(HttpClient httpClient, ILogger<BaseApiClient> logger)
    //    {
    //        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    //        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    //    }

    //    // 發送 HTTP GET 請求並返回結果。
    //    public async Task<TResult?> GetAsync<TResult>(string endpoint, CancellationToken cancellationToken = default)
    //    {
    //        // 驗證端點是否為空或空白。
    //        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

    //        try
    //        {
    //            // 發送 HTTP GET 請求。
    //            var response = await _httpClient.GetAsync(endpoint, cancellationToken);

    //            // 檢查 HTTP 回應狀態碼。
    //            if (!response.IsSuccessStatusCode)
    //            {
    //                // 讀取並記錄錯誤內容。
    //                var content = await response.Content.ReadAsStringAsync(cancellationToken);
    //                _logger.LogError("API GET failed. Status: {Status}, Content: {Content}", response.StatusCode, content);
    //                // 拋出自定義異常。
    //                throw new HttpApiClientException(response.StatusCode, $"Request to {endpoint} failed.", content);
    //            }
    //            // 解析 JSON 回應內容。
    //            return await response.Content.ReadFromJsonAsync<TResult>(cancellationToken: cancellationToken);
    //        }
    //        catch (HttpRequestException ex)
    //        {
    //            // 處理 HTTP 請求例外。
    //            _logger.LogError(ex, "Network error occurred during GET from {Endpoint}", endpoint);
    //            throw;
    //        }
    //    }

    //    // 發送 HTTP POST 請求並返回結果。
    //    public async Task<TResult?> PostAsync<TRequest, TResult>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
    //    {
    //        // 驗證端點是否為空或空白，並檢查數據是否為 null。
    //        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
    //        ArgumentNullException.ThrowIfNull(data);

    //        try
    //        {
    //            // 發送 HTTP POST 請求。
    //            var response = await _httpClient.PostAsJsonAsync(endpoint, data, cancellationToken);
    //            // 檢查 HTTP 回應狀態碼。
    //            if (!response.IsSuccessStatusCode)
    //            {
    //                // 讀取並記錄錯誤內容。
    //                var content = await response.Content.ReadAsStringAsync(cancellationToken);
    //                _logger.LogError("API POST failed. Status: {Status}, Content: {Content}", response.StatusCode, content);
    //                throw new HttpApiClientException(response.StatusCode, $"Request to {endpoint} failed.", content);
    //            }
    //            // 解析 JSON 回應內容。
    //            return await response.Content.ReadFromJsonAsync<TResult>(cancellationToken: cancellationToken);
    //        }
    //        catch (Exception ex)
    //        {
    //            // 處理其他例外。
    //            _logger.LogError(ex, "POST request to {Endpoint} encountered an error.", endpoint);
    //            throw;
    //        }
    //    }
    //}

    /// <summary>
    /// BaseApiClient.cs (基底通訊類別)
    /// 將錯誤處理與通訊邏輯集中於此，為所有 API 客戶端提供標準化介面。
    /// </summary>
    public abstract class BaseApiClient : IApiClient
    {
        protected readonly HttpClient _httpClient;
        protected readonly ILogger<BaseApiClient> _logger;

        protected BaseApiClient(HttpClient httpClient, ILogger<BaseApiClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TResult?> GetAsync<TResult>(string endpoint, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

            var response = await _httpClient.GetAsync(endpoint, ct);
            return await HandleResponseAsync<TResult>(response, endpoint, ct);
        }

        public async Task<TResult?> PostAsync<TRequest, TResult>(string endpoint, TRequest data, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
            ArgumentNullException.ThrowIfNull(data);

            var response = await _httpClient.PostAsJsonAsync(endpoint, data, ct);
            return await HandleResponseAsync<TResult>(response, endpoint, ct);
        }

        // 統一回應處理邏輯，確保所有子類別行為一致
        protected async Task<TResult?> HandleResponseAsync<TResult>(HttpResponseMessage response, string endpoint, CancellationToken ct)
        {
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("API Request failed. Endpoint: {Endpoint}, Status: {Status}, Content: {Content}", endpoint, response.StatusCode, content);
                throw new HttpApiClientException(response.StatusCode, $"Request to {endpoint} failed.", content);
            }

            return await response.Content.ReadFromJsonAsync<TResult>(cancellationToken: ct);
        }
    }
}
