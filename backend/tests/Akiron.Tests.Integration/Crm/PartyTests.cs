using System.Net;
using System.Net.Http.Json;
using Akiron.BuildingBlocks.Web;
using Akiron.Modules.Crm.Features;
using Akiron.Modules.Crm.Features.ListParties;
using Akiron.Modules.Timeline.Features;

namespace Akiron.Tests.Integration.Crm;

public sealed class PartyTests(ApiFixture api)
{
    private const string PartiesPath = "/api/v1/crm/parties";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static object Company(string name, string? taxNumber = null, string? code = null, bool isSupplier = false) => new
    {
        kind = "company",
        name,
        isCustomer = true,
        isSupplier,
        code,
        taxNumber,
        taxOffice = "Kadıköy",
        email = "Muhasebe@Example.com",
        city = "İstanbul",
    };

    private async Task<HttpClient> OwnerAsync()
    {
        var client = api.CreateClient();
        using var _ = await client.RegisterAsync();
        return client;
    }

    private static async Task<PartyResponse> CreateAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(PartiesPath, body, Cancel);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PartyResponse>(Cancel))!;
    }

    [Fact]
    public async Task Create_WithoutACode_NumbersTheSeriesAndNormalises()
    {
        using var owner = await OwnerAsync();

        var first = await CreateAsync(owner, Company("ABC Mobilya", "1234567890"));
        var second = await CreateAsync(owner, Company("Deniz Lojistik"));

        Assert.Equal("C00001", first.Code);
        Assert.Equal("C00002", second.Code);
        Assert.Equal("muhasebe@example.com", first.Email);
        Assert.Equal("company", first.Kind);
    }

    [Fact]
    public async Task Create_WithAHandTypedCode_KeepsItAndTheSeriesSkipsIt()
    {
        using var owner = await OwnerAsync();

        var manual = await CreateAsync(owner, Company("Logo'dan Gelen", code: "C00001"));
        var automatic = await CreateAsync(owner, Company("Yeni Cari"));
        using var duplicate = await owner.PostAsJsonAsync(PartiesPath, Company("Çakışan", code: "C00001"), Cancel);

        Assert.Equal("C00001", manual.Code);
        Assert.Equal("C00002", automatic.Code);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("crm.party.code_taken", await duplicate.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Create_WithAMistypedVkn_IsRefusedOnTheField()
    {
        using var owner = await OwnerAsync();

        using var response = await owner.PostAsJsonAsync(PartiesPath, Company("ABC Mobilya", "1234567891"), Cancel);
        var body = await response.Content.ReadAsStringAsync(Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"taxNumber\"", body, StringComparison.Ordinal);
        Assert.Contains("crm.party.tax_number_invalid", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_NeitherCustomerNorSupplier_IsRefused()
    {
        using var owner = await OwnerAsync();

        using var response = await owner.PostAsJsonAsync(PartiesPath, new { kind = "person", name = "Boş Rol", isCustomer = false, isSupplier = false }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("crm.party.role_required", await response.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_SearchesWithoutTurkishLettersAndSortsTheTurkishWay()
    {
        using var owner = await OwnerAsync();
        await CreateAsync(owner, Company("IŞIK Tekstil"));
        await CreateAsync(owner, Company("Çınar Ajans"));
        await CreateAsync(owner, Company("Cengiz Yapı", isSupplier: true));
        await CreateAsync(owner, Company("Zeytin Gıda"));

        var search = await owner.GetFromJsonAsync<PagedResult<PartyListItem>>($"{PartiesPath}?search=isik", Cancel);
        var all = await owner.GetFromJsonAsync<PagedResult<PartyListItem>>(PartiesPath, Cancel);
        var suppliers = await owner.GetFromJsonAsync<PagedResult<PartyListItem>>($"{PartiesPath}?role=supplier", Cancel);

        Assert.Equal("IŞIK Tekstil", Assert.Single(search!.Items).Name);
        Assert.Equal(["Cengiz Yapı", "Çınar Ajans", "IŞIK Tekstil", "Zeytin Gıda"], all!.Items.Select(item => item.Name));
        Assert.Equal("Cengiz Yapı", Assert.Single(suppliers!.Items).Name);
    }

    [Fact]
    public async Task List_WithAnUnknownRole_IsRefused()
    {
        using var owner = await OwnerAsync();

        using var response = await owner.GetAsync($"{PartiesPath}?role=partner", Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_WritesTheChangedFieldsOnThePartyTimeline()
    {
        using var owner = await OwnerAsync();
        var party = await CreateAsync(owner, Company("ABC Mobilya"));

        using var update = await owner.PutAsJsonAsync($"{PartiesPath}/{party.Id}", new
        {
            kind = "company",
            name = "ABC Mobilya A.Ş.",
            isCustomer = true,
            isSupplier = false,
            taxOffice = "Kadıköy",
            email = "muhasebe@example.com",
            city = "Ankara",
        }, Cancel);
        await api.DeliverOutboxAsync();
        var timeline = await owner.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/party/{party.Id}", Cancel);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(["crm.party.updated", "crm.party.created"], timeline!.Items.Select(item => item.Type));
        var fields = timeline.Items[0].Payload.GetProperty("fields").EnumerateArray().Select(field => field.GetString());
        Assert.Equal(["name", "city"], fields);
    }

    [Fact]
    public async Task Contacts_MarkingOnePrimary_UnmarksTheOther()
    {
        using var owner = await OwnerAsync();
        var party = await CreateAsync(owner, Company("ABC Mobilya"));

        using var first = await owner.PostAsJsonAsync($"{PartiesPath}/{party.Id}/contacts", new { fullName = "Ayşe Yılmaz", isPrimary = true }, Cancel);
        using var second = await owner.PostAsJsonAsync($"{PartiesPath}/{party.Id}/contacts", new { fullName = "Mert Kaya", title = "Satın alma", isPrimary = true }, Cancel);
        var detail = await owner.GetFromJsonAsync<PartyResponse>($"{PartiesPath}/{party.Id}", Cancel);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(["Mert Kaya", "Ayşe Yılmaz"], detail!.Contacts.Select(contact => contact.FullName));
        Assert.Equal([true, false], detail.Contacts.Select(contact => contact.IsPrimary));
    }

    [Fact]
    public async Task Archive_HidesThePartyAndFreesItsCode()
    {
        using var owner = await OwnerAsync();
        var party = await CreateAsync(owner, Company("Eski Müşteri", code: "M-01"));

        using var archive = await owner.DeleteAsync($"{PartiesPath}/{party.Id}", Cancel);
        using var detail = await owner.GetAsync($"{PartiesPath}/{party.Id}", Cancel);
        var reused = await CreateAsync(owner, Company("Yeni Müşteri", code: "M-01"));

        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal("crm.party.not_found", await detail.ReadErrorCodeAsync());
        Assert.Equal("M-01", reused.Code);
    }

    [Fact]
    public async Task Parties_AreInvisibleAndUntouchableFromAnotherTenant()
    {
        using var first = await OwnerAsync();
        using var second = await OwnerAsync();
        var party = await CreateAsync(first, Company("Gizli Müşteri"));

        var list = await second.GetFromJsonAsync<PagedResult<PartyListItem>>(PartiesPath, Cancel);
        using var read = await second.GetAsync($"{PartiesPath}/{party.Id}", Cancel);
        using var write = await second.PutAsJsonAsync($"{PartiesPath}/{party.Id}", new { kind = "company", name = "Ele geçirildi", isCustomer = true, isSupplier = false }, Cancel);
        using var contact = await second.PostAsJsonAsync($"{PartiesPath}/{party.Id}/contacts", new { fullName = "Sızma" }, Cancel);
        using var archive = await second.DeleteAsync($"{PartiesPath}/{party.Id}", Cancel);
        var stillThere = await first.GetFromJsonAsync<PartyResponse>($"{PartiesPath}/{party.Id}", Cancel);

        Assert.Empty(list!.Items);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, contact.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, archive.StatusCode);
        Assert.Equal("Gizli Müşteri", stillThere!.Name);
        Assert.Empty(stillThere.Contacts);
    }

    [Fact]
    public async Task Parties_ForAMemberWithoutThePermission_AreForbidden()
    {
        using var owner = await OwnerAsync();
        var memberEmail = ApiClientExtensions.UniqueEmail();
        using var _ = await owner.InviteAsync(memberEmail);
        using var member = api.CreateClient();
        using var __ = await member.AcceptInvitationAsync(api.InvitationTokenFor(memberEmail));

        using var list = await member.GetAsync(PartiesPath, Cancel);
        using var create = await member.PostAsJsonAsync(PartiesPath, Company("Yetkisiz"), Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }
}
