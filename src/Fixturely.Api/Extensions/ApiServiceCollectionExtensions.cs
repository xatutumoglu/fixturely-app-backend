using System.Text;
using System.Threading.RateLimiting;
using Fixturely.Application.Abstractions.Security;
using Fixturely.Api.Security;
using Fixturely.Infrastructure.Options;
using Fixturely.Infrastructure.Persistence;
using HealthChecks.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;

namespace Fixturely.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    private const string FrontendCorsPolicy = "FrontendCorsPolicy";

    public static IServiceCollection AddFixturelyApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var isTestEnvironment = environment.IsEnvironment("Testing");
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddControllers();
        services.AddEndpointsApiExplorer();

        services.AddCors(options =>
        {
            var frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";

            options.AddPolicy(FrontendCorsPolicy, policy =>
            {
                policy.WithOrigins(frontendBaseUrl)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();

        var authSensitivePermitLimit = isTestEnvironment ? 100_000 : 10;
        var invitationSensitivePermitLimit = isTestEnvironment ? 100_000 : 20;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("auth-sensitive", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = authSensitivePermitLimit,
                        QueueLimit = 0
                    }));

            options.AddPolicy("invitation-sensitive", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = invitationSensitivePermitLimit,
                        QueueLimit = 0
                    }));
        });

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Fixturely API",
                Version = "v1",
                Description = "Backend API for Fixturely football tournament management platform."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter 'Bearer' followed by a space and your JWT access token."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });

            options.EnableAnnotations();
        });

        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>("sqlserver", tags: new[] { "ready" })
            .AddCheck<Fixturely.Api.HealthChecks.RedisHealthCheck>("redis", tags: new[] { "ready" })
            .AddCheck<Fixturely.Api.HealthChecks.SmtpConfigurationHealthCheck>("smtp-configuration", tags: new[] { "ready" });

        return services;
    }

    public static string FrontendCorsPolicyName => FrontendCorsPolicy;
}
