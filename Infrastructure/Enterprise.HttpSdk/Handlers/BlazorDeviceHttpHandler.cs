// 檔案: BlazorDeviceHttpHandler.cs
using Microsoft.JSInterop;

public sealed class BlazorDeviceHttpHandler : DelegatingHandler
{
    private readonly IJSRuntime _jsRuntime;

    public BlazorDeviceHttpHandler(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        string deviceId = "PENDING-DEVICE-ID"; // 預設值
        try
        {
            // 增加一個簡單檢查，避免還沒注入就呼叫
            var isReady = await _jsRuntime.InvokeAsync<bool>("eval", ct, "!!window.blazorDeviceInterop");
            if (isReady)
            {
                deviceId = await _jsRuntime.InvokeAsync<string>("blazorDeviceInterop.getOrCreateDeviceId", ct);
            }
        }
        catch (Exception) { /* 發生錯誤時保持 PENDING */ }

        request.Headers.TryAddWithoutValidation("X-Device-Id", deviceId);
        return await base.SendAsync(request, ct);
    }
}