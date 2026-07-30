using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Fixturely.Api.Filters;

/// <summary>
/// Runs any registered FluentValidation <see cref="IValidator{T}"/> against each action
/// argument before the action executes, throwing <see cref="ValidationException"/> on
/// failure so the central <see cref="Fixturely.Api.Middleware.ExceptionHandlingMiddleware"/>
/// can translate it into an RFC 7807 ProblemDetails response.
/// </summary>
public sealed class ValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            var validator = context.HttpContext.RequestServices.GetService(validatorType) as IValidator;

            if (validator is null)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }

        await next();
    }
}
