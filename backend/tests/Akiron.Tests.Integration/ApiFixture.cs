using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Akiron.BuildingBlocks.Email;
using Akiron.BuildingBlocks.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Akiron.Tests.Integration.ApiFixture))]

namespace Akiron.Tests.Integration;

/// <summary>
/// One PostgreSQL container and one running API for the whole test run. The host applies the real
/// migrations at startup, which also proves they run on an empty database. Tests isolate
/// themselves with fresh tenants and e-mail addresses instead of wiping tables, so they can run
/// in parallel.
/// </summary>
public sealed partial class ApiFixture : IAsyncLifetime
{
    private readonly CapturingEmailSender _emails = new();

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

            // Tests deliver the outbox themselves (DeliverOutboxAsync) so they never race a timer.
            builder.UseSetting("Outbox:Enabled", "false");

            builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<IEmailSender>(_emails)));
        });

        // Forces the host to start, which runs the migrations once for all tests.
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Delivers every pending outbox message, as the background dispatcher would.</summary>
    public async Task DeliverOutboxAsync()
    {
        var processor = Services.GetRequiredService<OutboxProcessor>();

        // Another test may hold some rows (SKIP LOCKED); go round until a pass finds nothing.
        for (var pass = 0; pass < 20; pass++)
        {
            if (await processor.ProcessAsync(CancellationToken.None) == 0)
            {
                await Task.Delay(25);
                if (await processor.ProcessAsync(CancellationToken.None) == 0)
                {
                    return;
                }
            }
        }
    }

    /// <summary>The invitation token from the last e-mail sent to <paramref name="email"/>.</summary>
    public string InvitationTokenFor(string email)
    {
        var body = _emails.Sent.Last(message => message.To == email).TextBody;
        return InviteLink().Match(body).Groups["token"].Value;
    }

    [GeneratedRegex(@"/invite\?token=(?<token>[0-9A-F]+)")]
    private static partial Regex InviteLink();

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }
}

/// <summary>Keeps sent e-mails in memory so tests can follow links, as a person would.</summary>
internal sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
