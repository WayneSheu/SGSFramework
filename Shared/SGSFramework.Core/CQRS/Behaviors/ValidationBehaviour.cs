using FluentValidation;
using MediatR;
using ValidationException = FluentValidation.ValidationException;

namespace SGSFramework.Core.CQRS.Behaviors
{
    /// <summary>
    /// Defines a pipeline behavior that applies validation logic to a request before passing it to the next handler in
    /// the pipeline.
    /// </summary>
    /// <remarks>If any validation failures are detected, a ValidationException is thrown and the request is
    /// not processed further. This behavior should be registered in the pipeline to ensure that all requests are
    /// validated consistently.</remarks>
    /// <typeparam name="TRequest">The type of the request message to validate.</typeparam>
    /// <typeparam name="TResponse">The type of the response message returned by the handler.</typeparam>
    /// <param name="validators">The collection of validators to apply to the request. Each validator is used to validate the request before it
    /// is handled.</param>
    public class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators = validators;

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (_validators.Any())
            {
                var context = new ValidationContext<TRequest>(request);

                var validationResults = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
                var failures = validationResults.SelectMany(r => r.Errors).Where(f => f != null).ToList();

                if (failures.Any())
                {
                    throw new ValidationException(failures);
                }
            }

            return await next();
        }
    }
}
