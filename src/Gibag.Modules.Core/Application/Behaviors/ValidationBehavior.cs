using FluentValidation;
using Gibag.Shared.Models;
using MediatR;

namespace Gibag.Modules.Core.Application.Behaviors;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var errorsDictionary = _validators
            .Select(x => x.Validate(context))
            .SelectMany(x => x.Errors)
            .Where(x => x != null)
            .GroupBy(
                x => x.PropertyName,
                x => x.ErrorMessage,
                (propertyName, errorMessages) => new
                {
                    Key = propertyName,
                    Values = errorMessages.Distinct().ToArray()
                })
            .ToDictionary(x => x.Key, x => x.Values);

        if (errorsDictionary.Count != 0)
        {
            // For Result<T> we would need to dynamically create a failed result,
            // or we return a standard Result failure if it's just Result.
            // Simplified validation failure for the Result Pattern
            var firstError = errorsDictionary.First();
            var errorMessage = $"{firstError.Key}: {string.Join(", ", firstError.Value)}";
            
            // This is a simplistic approach requiring reflection to return Result<T>. 
            // Usually, a specific ValidationResult class inheriting from Result is used.
            var responseType = typeof(TResponse);
            
            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var resultType = responseType.GetGenericArguments()[0];
                var failureMethod = typeof(Result<>)
                    .MakeGenericType(resultType)
                    .GetMethod("Failure");
                
                return (TResponse)failureMethod!.Invoke(null, new object[] { "Validation.Error", errorMessage })!;
            }

            return (TResponse)(Result)Result.Failure("Validation.Error", errorMessage);
        }

        return await next();
    }
}
