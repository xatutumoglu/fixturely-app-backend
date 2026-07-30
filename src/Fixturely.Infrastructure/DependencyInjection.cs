using Fixturely.Application.Abstractions.Caching;
using Fixturely.Application.Abstractions.Email;
using Fixturely.Application.Abstractions.Identity;
using Fixturely.Application.Abstractions.Persistence;
using Fixturely.Application.Abstractions.Security;
using Fixturely.Application.Common;
using Fixturely.Infrastructure.Caching;
using Fixturely.Infrastructure.Email;
using Fixturely.Infrastructure.Identity;
using Fixturely.Infrastructure.Options;
using Fixturely.Infrastructure.Persistence;
using Fixturely.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Fixturely.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        var identityBuilder = services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
        });

        identityBuilder.AddEntityFrameworkStores<ApplicationDbContext>();
        identityBuilder.AddDefaultTokenProviders();

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration["Redis:ConnectionString"] ?? "localhost:6379"));

        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<ISessionStore, RedisSessionStore>();

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
        services.Configure<FrontendOptions>(configuration.GetSection("Frontend"));
        services.Configure<SessionOptions>(configuration.GetSection("Session"));

        services.Configure<RefreshTokenOptions>(options =>
        {
            options.RefreshTokenDays = configuration.GetValue<int?>("Jwt:RefreshTokenDays") ?? 7;
        });

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IEmailSender, BrevoSmtpEmailSender>();
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
