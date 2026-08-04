using MediatR;
using Serilog;
using Serilog.Context;
using System.Diagnostics;

namespace SGSFramework.Core.CQRS.Behaviors
{
    /// <summary>
    /// MediatR PipelineBehavior 紀錄 Request/Response
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
       where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            // 1. 取得 Request 名稱
            string requestName = typeof(TRequest).Name;

            //// 2. 使用 LogContext 注入 Command/Query 名稱，後續所有 Log 都會帶此欄位
            //using (LogContext.PushProperty("CommandName", requestName))
            //{
            //    Log.Information("[CQRS] 開始處理 {RequestName}", requestName);

            //    // 紀錄 Request 內容 (序列化時注意敏感資訊)
            //    // Log.Information("[CQRS] 參數: {Payload}", JsonSerializer.Serialize(request));

            //    var timer = Stopwatch.StartNew();
            //    try
            //    {
            //        var response = await next();
            //        timer.Stop();

            //        // 3. 紀錄完成與耗時
            //        Log.Information("[CQRS] 處理完成 {RequestName}，耗時: {Elapsed}ms",
            //            requestName, timer.ElapsedMilliseconds);

            //        return response;
            //    }
            //    catch (Exception ex)
            //    {
            //        timer.Stop();
            //        Log.Error(ex, "[CQRS] 處理 {RequestName} 時發生錯誤，耗時: {Elapsed}ms",
            //            requestName, timer.ElapsedMilliseconds);
            //        throw;
            //    }
            //}


            // 注入「業務屬性」
            // CorrelationId 已經由 Middleware 處理了，這裡不重複 Push
            using (LogContext.PushProperty("ModuleName", "CQRS"))
            {
                using (LogContext.PushProperty("Operation", requestName))
                {
                    Log.Information("[CQRS] 開始處理 {RequestName}", requestName);

                    var timer = Stopwatch.StartNew();
                    try
                    {
                        var response = await next();
                        timer.Stop();

                        Log.Information("[CQRS] 處理完成 {RequestName}，耗時: {Elapsed}ms",
                            requestName, timer.ElapsedMilliseconds);

                        return response;
                    }
                    catch (Exception ex)
                    {
                        timer.Stop();
                        Log.Error(ex, "[CQRS] 處理 {RequestName} 時發生錯誤，耗時: {Elapsed}ms",
                            requestName, timer.ElapsedMilliseconds);
                        throw;
                    }
                }
            }
        }
    }
}
