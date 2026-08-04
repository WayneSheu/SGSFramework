using MediatR;
using Microsoft.Extensions.Logging;

namespace SGSFramework.Core.CQRS.Behaviors
{
    /// <summary>
    /// Provides a pipeline behavior that handles exceptions thrown during the processing of a request and logs them
    /// using the configured logger.
    /// </summary>
    /// <remarks>This behavior should be registered in the request pipeline to ensure that unhandled
    /// exceptions are consistently logged. It does not alter the exception; exceptions are rethrown after logging. This
    /// allows other behaviors or the calling code to handle the exception as needed.</remarks>
    /// <typeparam name="TRequest">The type of the request being handled.</typeparam>
    /// <typeparam name="TResponse">The type of the response returned by the handler.</typeparam>
    public class ExceptionHandlingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        private readonly ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> _logger;

        public ExceptionHandlingBehavior(ILogger<ExceptionHandlingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            try
            {
                return await next();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Request {Request} failed with exception {Message}", typeof(TRequest).Name, ex.Message);
                throw;
            }
        }
    }
}
