using System.Net;
using System.Net.Http.Json;
using Akiron.Modules.Crm.Features;
using Akiron.Modules.Finance.Features.Charges;
using Akiron.Modules.Finance.Features.Reminders;
using Akiron.Modules.Finance.Features.Statements;
using Akiron.Modules.Timeline.Features;

namespace Akiron.Tests.Integration.Finance;

public sealed class ReminderTests(ApiFixture api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync(Cancel)}");
        return (await response.Content.ReadFromJsonAsync<T>(Cancel))!;
    }

    private async Task<(HttpClient Owner, Guid PartyId)> OwnerWithClientAsync(string? contactEmail)
    {
        var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync();
        using var party = await owner.PostAsJsonAsync("/api/v1/crm/parties", new { kind = "company", name = "Ada Yapı", isCustomer = true, isSupplier = false }, Cancel);
        var partyId = (await ReadAsync<PartyResponse>(party, HttpStatusCode.Created)).Id;
        if (contactEmail is not null)
        {
            using var contact = await owner.PostAsJsonAsync($"/api/v1/crm/parties/{partyId}/contacts", new { fullName = "Oya Kaplan", email = contactEmail, isPrimary = true }, Cancel);
            Assert.Equal(HttpStatusCode.Created, contact.StatusCode);
        }

        return (owner, partyId);
    }

    private static async Task<ChargeResponse> ChargeAsync(HttpClient client, Guid partyId, DateOnly dueDate)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/finance/charges", new { partyId, description = "Katalog tasarımı", amount = 18_000m, dueDate }, Cancel);
        return await ReadAsync<ChargeResponse>(response, HttpStatusCode.Created);
    }

    [Fact]
    public async Task Remind_EmailsThePrimaryContact_AndNotesItOnTheTimeline()
    {
        var email = ApiClientExtensions.UniqueEmail();
        var (owner, partyId) = await OwnerWithClientAsync(email);
        var charge = await ChargeAsync(owner, partyId, Today.AddDays(-5));

        using var remind = await owner.PostAsJsonAsync($"/api/v1/finance/open-items/{charge.Instalments[0].Id}/remind", new { }, Cancel);
        var sent = await ReadAsync<ReminderResponse>(remind, HttpStatusCode.OK);
        var items = await owner.GetFromJsonAsync<IReadOnlyList<OpenItemRow>>($"/api/v1/finance/open-items?partyId={partyId}", Cancel);
        await api.DeliverOutboxAsync();
        var timeline = await owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/party/{partyId}", Cancel);

        Assert.Equal((email, 1), (sent.Email, sent.RemindersSent));
        var message = Assert.Single(api.EmailsTo(email));
        Assert.Contains(charge.Number, message.Subject, StringComparison.Ordinal);
        Assert.Contains("18.000,00 TRY", message.TextBody, StringComparison.Ordinal);
        Assert.NotNull(Assert.Single(items!).LastRemindedAt);
        Assert.Contains(timeline!.Items, item => item.Type == "finance.reminder.sent");
    }

    [Fact]
    public async Task Remind_WithoutAnyAddress_AsksForOne()
    {
        var (owner, partyId) = await OwnerWithClientAsync(contactEmail: null);
        var charge = await ChargeAsync(owner, partyId, Today);

        using var remind = await owner.PostAsJsonAsync($"/api/v1/finance/open-items/{charge.Instalments[0].Id}/remind", new { }, Cancel);
        var typed = ApiClientExtensions.UniqueEmail();
        using var withTyped = await owner.PostAsJsonAsync($"/api/v1/finance/open-items/{charge.Instalments[0].Id}/remind", new { email = typed }, Cancel);

        Assert.Equal("finance.reminder.no_email", await remind.ReadErrorCodeAsync());
        Assert.Equal(HttpStatusCode.OK, withTyped.StatusCode);
        Assert.Single(api.EmailsTo(typed));
    }

    [Fact]
    public async Task Digest_TellsCollectorsOncePerDay()
    {
        var (owner, partyId) = await OwnerWithClientAsync(contactEmail: null);
        await ChargeAsync(owner, partyId, Today.AddDays(-3));
        await ChargeAsync(owner, partyId, Today);

        await api.RunJobAsync<PaymentDigestJob>(Today);
        await api.RunJobAsync<PaymentDigestJob>(Today);
        await api.DeliverOutboxAsync();
        using var inbox = await owner.GetAsync("/api/v1/notifications", Cancel);
        var body = await inbox.Content.ReadAsStringAsync(Cancel);

        Assert.Equal(1, System.Text.RegularExpressions.Regex.Count(body, "finance.payments.digest"));
        Assert.Contains("\"overdue\":1", body, StringComparison.Ordinal);
        Assert.Contains("\"dueToday\":1", body, StringComparison.Ordinal);
    }
}
