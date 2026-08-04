using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace SGSFramework.Core.CQRS.Behaviors
{
    /// <summary>
    /// Provides a pipeline behavior that logs the execution time of a request in the MediatR pipeline.
    /// </summary>
    /// <remarks>This behavior measures and logs the time taken to process each request using the provided
    /// logger. It is typically used for performance monitoring and diagnostics in MediatR-based applications.</remarks>
    /// <typeparam name="TRequest">The type of the request being handled.</typeparam>
    /// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
    public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

        public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await next();
            stopwatch.Stop();

            _logger.LogInformation("Request {Request} executed in {ElapsedMilliseconds}ms", typeof(TRequest).Name, stopwatch.ElapsedMilliseconds);

            return response;
        }
    }
}
