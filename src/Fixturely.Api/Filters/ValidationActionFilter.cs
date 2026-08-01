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
            if (argument is null || IsSimpleRouteOrQueryType(argument.GetType()))
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

    /// <summary>
    /// Route/query-bound action parameters (a bare <c>string</c> invitation token, a
    /// <c>Guid</c> id, a page number, etc.) are never one of this API's request-body DTOs, but
    /// some of those primitive .NET types (most notably <c>string</c>) can still resolve an
    /// <see cref="IValidator{T}"/> from DI if any <see cref="AbstractValidator{T}"/> in the
    /// assembly happens to target that primitive type for internal composition purposes (e.g.
    /// <c>PasswordRuleValidator : AbstractValidator&lt;string&gt;</c>, composed into
    /// <c>RegisterRequestValidator</c>/<c>ResetPasswordRequestValidator</c> via
    /// <c>.SetValidator(...)</c>, not meant to run standalone). Without this guard, any bare
    /// string/Guid/int route or query parameter would be incorrectly validated against whichever
    /// unrelated primitive-typed validator happens to be registered - e.g. an invitation token
    /// being rejected with "Password must contain at least one special character" purely because
    /// <see cref="PasswordRuleValidator"/> is the only <c>IValidator&lt;string&gt;</c> in the
    /// container. Only our own DTOs (always reference types outside the BCL) are ever intended
    /// to be validated here.
    /// </summary>
    private static bool IsSimpleRouteOrQueryType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType.IsPrimitive
            || underlyingType.IsEnum
            || underlyingType == typeof(string)
            || underlyingType == typeof(Guid)
            || underlyingType == typeof(decimal)
            || underlyingType == typeof(DateTime)
            || underlyingType == typeof(DateTimeOffset)
            || underlyingType == typeof(TimeSpan);
    }
}
