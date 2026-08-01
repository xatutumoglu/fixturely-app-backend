using System.Net;
using Fixturely.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Fixturely.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleAsync(context, exception);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = Map(exception);

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning("Request {Method} {Path} failed with {StatusCode}: {Message}",
                context.Request.Method, context.Request.Path, statusCode, exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception is IdentityValidationException identityValidationException
                ? string.Join(" ", identityValidationException.Errors)
                : exception.Message,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.io/{statusCode}"
        };

        if (exception is ValidationException fluentValidationException)
        {
            problemDetails.Extensions["errors"] = fluentValidationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        if (_environment.IsDevelopment() && statusCode >= 500)
        {
            problemDetails.Extensions["exception"] = exception.ToString();
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static (int StatusCode, string Title) Map(Exception exception) => exception switch
    {
        TournamentNotFoundException => ((int)HttpStatusCode.NotFound, "Tournament not found"),
        UnauthorizedTournamentAccessException => ((int)HttpStatusCode.Forbidden, "Access denied"),
        InvalidCredentialsException => ((int)HttpStatusCode.Unauthorized, "Invalid credentials"),
        EmailNotConfirmedException => ((int)HttpStatusCode.Unauthorized, "Email not confirmed"),
        AccountDisabledException => ((int)HttpStatusCode.Unauthorized, "Account disabled"),
        InvalidRefreshTokenException => ((int)HttpStatusCode.Unauthorized, "Invalid refresh token"),
        ConcurrencyConflictException => ((int)HttpStatusCode.Conflict, "Concurrency conflict"),
        ValidationException => ((int)HttpStatusCode.BadRequest, "Validation failed"),
        IdentityValidationException => ((int)HttpStatusCode.BadRequest, "Request failed"),
        InvalidScoreException => ((int)HttpStatusCode.BadRequest, "Invalid score"),
        InvalidFixtureGenerationException => ((int)HttpStatusCode.BadRequest, "Invalid fixture generation"),
        InvalidTournamentStateException => ((int)HttpStatusCode.BadRequest, "Invalid tournament state"),
        ParticipantAlreadyExistsException => ((int)HttpStatusCode.BadRequest, "Participant already exists"),
        InvitationException => ((int)HttpStatusCode.BadRequest, "Invitation error"),
        UserNotRegisteredException => ((int)HttpStatusCode.NotFound, "Recipient not registered"),
        TournamentGroupCompositionException => ((int)HttpStatusCode.BadRequest, "Invalid group composition"),
        KnockoutPairingException => ((int)HttpStatusCode.BadRequest, "Invalid knockout pairing"),
        DomainException => ((int)HttpStatusCode.BadRequest, "Request failed"),
        _ => ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred")
    };
}
