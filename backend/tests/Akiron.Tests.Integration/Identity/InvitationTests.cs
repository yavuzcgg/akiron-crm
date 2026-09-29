using System.Net;
using System.Net.Http.Json;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Identity.Features.Invitations;
using Akiron.Modules.Identity.Features.ListMembers;
using Akiron.Modules.Identity.Features.Sessions;

namespace Akiron.Tests.Integration.Identity;

public sealed class InvitationTests(ApiFixture api)
{
    private static readonly Uri MembersUri = new("/api/v1/identity/members", UriKind.Relative);

    [Fact]
    public async Task Accept_AsANewPerson_CreatesTheAccountAndSignsIntoTheInvitingWorkspace()
    {
        using var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync(organizationName: "Davet Eden Ajans");
        var inviteeEmail = ApiClientExtensions.UniqueEmail();

        using var invite = await owner.InviteAsync(inviteeEmail);
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);

        var token = api.InvitationTokenFor(inviteeEmail);
        using var invitee = api.CreateClient();
        var preview = await invitee.GetFromJsonAsync<InvitationPreviewResponse>(
            $"/api/v1/identity/invitations/preview?token={token}", TestContext.Current.CancellationToken);
        Assert.NotNull(preview);
        Assert.Equal("Davet Eden Ajans", preview.WorkspaceName);
        Assert.False(preview.AccountExists);

        using var accepted = await invitee.AcceptInvitationAsync(token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var session = await accepted.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken);
        Assert.Equal("Davet Eden Ajans", session!.TenantName);
        Assert.Equal("member", session.Role);

        var members = await owner.GetFromJsonAsync<PagedResult<MemberResponse>>(MembersUri, TestContext.Current.CancellationToken);
        Assert.Equal(2, members!.TotalCount);

        // A link works once.
        using var again = await api.CreateClient().AcceptInvitationAsync(token);
        Assert.Equal("identity.invitation.invalid", await again.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Accept_ForAnExistingAccount_NeedsThatAccountsPassword()
    {
        var existingEmail = ApiClientExtensions.UniqueEmail();
        using var elsewhere = api.CreateClient();
        using var __ = await elsewhere.RegisterAsync(existingEmail, organizationName: "Kendi Ajansı");

        using var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync(organizationName: "İkinci Ajans");
        using var invite = await owner.InviteAsync(existingEmail, role: "admin");
        var token = api.InvitationTokenFor(existingEmail);

        using var wrongPassword = await api.CreateClient().AcceptInvitationAsync(token, password: "baska-birinin-parolasi");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal("identity.auth.invalid_credentials", await wrongPassword.ReadErrorCodeAsync());

        using var accepted = await api.CreateClient().AcceptInvitationAsync(token, fullName: null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var session = await accepted.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken);
        Assert.Equal("İkinci Ajans", session!.TenantName);
        Assert.Equal("admin", session.Role);
    }

    [Fact]
    public async Task Invite_AsOwner_IsRefused()
    {
        using var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync();

        using var response = await owner.InviteAsync(ApiClientExtensions.UniqueEmail(), role: "owner");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("identity.invitation.owner_not_invitable", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Invite_SomeoneAlreadyInTheWorkspace_ReturnsAlreadyMember()
    {
        var email = ApiClientExtensions.UniqueEmail();
        using var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync(email);

        using var response = await owner.InviteAsync(email);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("identity.member.already_member", await response.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Invite_Again_DisablesTheEarlierLink()
    {
        var email = ApiClientExtensions.UniqueEmail();
        using var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync();

        using var first = await owner.InviteAsync(email);
        var firstToken = api.InvitationTokenFor(email);
        using var second = await owner.InviteAsync(email);

        using var preview = await api.CreateClient().GetAsync(
            new Uri($"/api/v1/identity/invitations/preview?token={firstToken}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, preview.StatusCode);

        var open = await owner.GetFromJsonAsync<List<InvitationResponse>>("/api/v1/identity/invitations", TestContext.Current.CancellationToken);
        Assert.Single(open!);
    }

    [Fact]
    public async Task Revoke_MakesTheLinkUseless()
    {
        var email = ApiClientExtensions.UniqueEmail();
        using var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync();
        using var invite = await owner.InviteAsync(email);
        var created = await invite.Content.ReadFromJsonAsync<InvitationResponse>(TestContext.Current.CancellationToken);

        using var revoke = await owner.DeleteAsync(new Uri($"/api/v1/identity/invitations/{created!.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var accept = await api.CreateClient().AcceptInvitationAsync(api.InvitationTokenFor(email));
        Assert.Equal(HttpStatusCode.NotFound, accept.StatusCode);
    }

    [Fact]
    public async Task Invite_WithoutThePermission_IsForbidden()
    {
        using var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync();
        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var invite = await owner.InviteAsync(memberEmail);
        using var member = api.CreateClient();
        using var __ = await member.AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));

        using var response = await member.InviteAsync(ApiClientExtensions.UniqueEmail());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
