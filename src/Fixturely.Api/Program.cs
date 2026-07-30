using Fixturely.Api.Extensions;
using Fixturely.Api.Filters;
using Fixturely.Api.Middleware;
using Fixturely.Application;
using Fixturely.Infrastructure;
using Fixturely.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddEnvironmentVariables(prefix: "FIXTURELY_");

    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithCorrelationId()
            .WriteTo.Console();
    });

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddFixturelyApi(builder.Configuration, builder.Environment);

    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationActionFilter>();
    });

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService("Fixturely.Api"))
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
                .AddRedisInstrumentation();

            var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
            }
        });

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Fixturely API v1");
        });
    }
    else
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    app.UseRouting();

    app.UseCors(ApiServiceCollectionExtensions.FrontendCorsPolicyName);

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseMiddleware<SessionValidationMiddleware>();
    app.UseAuthorization();

    app.MapControllers();

    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (app.Environment.IsEnvironment("Testing"))
        {
            // Integration tests manage schema creation/migration themselves via Testcontainers.
        }
        else if (!app.Environment.IsEnvironment("Development") || builder.Configuration.GetValue<bool>("AutoMigrate"))
        {
            await dbContext.Database.MigrateAsync();
        }
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Fixturely API terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
}
