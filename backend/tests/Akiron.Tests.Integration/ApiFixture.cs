using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Akiron.Tests.Integration.ApiFixture))]

namespace Akiron.Tests.Integration;

/// <summary>
/// One PostgreSQL container and one running API for the whole test run. The host applies the real
/// migrations at startup, which also proves they run on an empty database. Tests isolate
/// themselves with fresh tenants and e-mail addresses instead of wiping tables, so they can run
/// in parallel.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private WebApplicationFactory<Program>? _factory;

    public IServiceProvider Services => Factory.Services;

    private WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("The fixture is not initialised.");

    /// <summary>A client with its own cookie jar: one browser, one session.</summary>
    public HttpClient CreateClient() =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            // UseSetting, not ConfigureAppConfiguration: Program reads configuration before
            // Build(), and only host settings are visible that early.
            builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
            builder.UseSetting("Jwt:SecretKey", "integration-test-signing-key-with-more-than-32-bytes");
            builder.UseSetting("Auth:CookieSecure", "false");
            builder.UseSetting("Database:MigrateOnStartup", "true");
            builder.UseSetting("RateLimiting:Auth:PermitLimit", "100000");
        });

        // Forces the host to start, which runs the migrations once for all tests.
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }
}
