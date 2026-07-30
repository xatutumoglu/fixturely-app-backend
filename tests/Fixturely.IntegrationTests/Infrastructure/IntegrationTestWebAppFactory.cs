using Fixturely.Application.Abstractions.Email;
using Fixturely.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using Xunit;

namespace Fixturely.IntegrationTests.Infrastructure;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Fixturely_Test_2024!")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public TestEmailCapture EmailCapture { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _sqlContainer.GetConnectionString(),
                ["Redis:ConnectionString"] = _redisContainer.GetConnectionString(),
                ["Jwt:Issuer"] = "Fixturely.Tests",
                ["Jwt:Audience"] = "Fixturely.Tests.Client",
                ["Jwt:SigningKey"] = "integration-test-signing-key-please-change-me-32-chars-min",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Session:IdleTimeoutMinutes"] = "15",
                ["Frontend:BaseUrl"] = "http://localhost:5173",
                ["Smtp:Host"] = "localhost",
                ["Smtp:Port"] = "1025",
                ["Smtp:FromEmail"] = "no-reply@fixturely.test",
                ["Smtp:FromName"] = "Fixturely Tests"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailCapture);
        });
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        await _redisContainer.StartAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
