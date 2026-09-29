using System.Net.Http.Json;
using System.Text.Json;
using Akiron.BuildingBlocks.Domain;
using Akiron.BuildingBlocks.Persistence;
using Akiron.Modules.Identity.Features.Invitations;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Akiron.Tests.Integration.Persistence;

/// <summary>Who changed what and when, written in the same transaction as the change.</summary>
public sealed class AuditTrailTests(ApiFixture api)
{
    [Fact]
    public async Task Update_RecordsTheChangedFieldsAndTheActor()
    {
        using var owner = api.CreateClient();
        using var registered = await owner.RegisterAsync();
        var session = await registered.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken);
        using var invite = await owner.InviteAsync(ApiClientExtensions.UniqueEmail());
        var invitation = await invite.Content.ReadFromJsonAsync<InvitationResponse>(TestContext.Current.CancellationToken);

        using var _ = await owner.DeleteAsync(new Uri($"/api/v1/identity/invitations/{invitation!.Id}", UriKind.Relative), TestContext.Current.CancellationToken);

        var changes = await AuditOfAsync("Invitation", invitation.Id.ToString());
        var created = Assert.Single(changes, change => change.Action == "created");
        var revoked = Assert.Single(changes, change => change.Action == "updated");

        Assert.Equal(UserId.From(session!.UserId), revoked.UserId);

        // jsonb normalises formatting and key order, so read it as JSON rather than as text.
        using var revokedChanges = JsonDocument.Parse(revoked.Changes);
        var revokedAt = revokedChanges.RootElement.GetProperty("revokedAt");
        Assert.Equal(JsonValueKind.Null, revokedAt.GetProperty("old").ValueKind);
        Assert.Equal(JsonValueKind.String, revokedAt.GetProperty("new").ValueKind);
        Assert.DoesNotContain("tokenHash", created.Changes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registration_NeverWritesThePasswordHashToTheTrail()
    {
        using var client = api.CreateClient();
        using var registered = await client.RegisterAsync();
        var session = await registered.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken);

        var userCreated = Assert.Single(await AuditOfAsync("User", session!.UserId.ToString()));

        Assert.Contains("\"email\"", userCreated.Changes, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", userCreated.Changes, StringComparison.Ordinal);
    }

    private async Task<List<AuditChange>> AuditOfAsync(string entityType, string entityId)
    {
        await using var scope = api.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        return await db.Set<AuditChange>()
            .Where(change => change.EntityType == entityType && change.EntityId == entityId)
            .OrderBy(change => change.OccurredAt)
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
