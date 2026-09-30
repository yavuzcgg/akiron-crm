using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Akiron.Modules.Crm.Features;

namespace Akiron.Tests.Integration.Crm;

public sealed class CustomFieldTests(ApiFixture api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly string[] Sectors = ["Mobilya", "Tekstil", "Gıda"];

    private async Task<HttpClient> OwnerWithFieldsAsync()
    {
        var owner = api.CreateClient();
        using var _ = await owner.RegisterAsync();
        foreach (var field in new object[]
        {
            new { label = "Sektör", type = "select", options = Sectors, isRequired = true },
            new { label = "Sözleşme bitişi", type = "date" },
            new { label = "Aylık bütçe", type = "number" },
            new { label = "Instagram hesabı", type = "text" },
        })
        {
            using var response = await owner.PostAsJsonAsync("/api/v1/crm/custom-fields", field, Cancel);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        return owner;
    }

    private static object Party(Dictionary<string, string?> customFields) =>
        new { kind = "company", name = "ABC Mobilya", isCustomer = true, isSupplier = false, customFields };

    [Fact]
    public async Task Party_WithValidCustomValues_StoresThemNormalised()
    {
        using var owner = await OwnerWithFieldsAsync();
        var fields = await owner.GetFromJsonAsync<JsonElement>("/api/v1/crm/custom-fields", Cancel);

        using var created = await owner.PostAsJsonAsync("/api/v1/crm/parties", Party(new()
        {
            ["sektor"] = "Mobilya",
            ["sozlesme_bitisi"] = "2027-01-31",
            ["aylik_butce"] = "25000.50",
            ["instagram_hesabi"] = "  @abcmobilya  ",
        }), Cancel);
        var party = (await created.Content.ReadFromJsonAsync<PartyResponse>(Cancel))!;

        Assert.Equal(["sektor", "sozlesme_bitisi", "aylik_butce", "instagram_hesabi"], fields.EnumerateArray().Select(field => field.GetProperty("key").GetString()));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("25000.50", party.CustomFields["aylik_butce"]);
        Assert.Equal("@abcmobilya", party.CustomFields["instagram_hesabi"]);
    }

    [Fact]
    public async Task Party_WithAMissingRequiredOrBadValue_IsRefusedNamingTheField()
    {
        using var owner = await OwnerWithFieldsAsync();

        using var missing = await owner.PostAsJsonAsync("/api/v1/crm/parties", Party(new() { ["aylik_butce"] = "10" }), Cancel);
        using var badChoice = await owner.PostAsJsonAsync("/api/v1/crm/parties", Party(new() { ["sektor"] = "Otomotiv" }), Cancel);
        using var badDate = await owner.PostAsJsonAsync("/api/v1/crm/parties", Party(new() { ["sektor"] = "Gıda", ["sozlesme_bitisi"] = "31.01.2027" }), Cancel);
        using var unknown = await owner.PostAsJsonAsync("/api/v1/crm/parties", Party(new() { ["sektor"] = "Gıda", ["yok"] = "x" }), Cancel);

        Assert.Equal("crm.custom_field.required", await missing.ReadErrorCodeAsync());
        Assert.Contains("Sektör", await missing.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.Equal("crm.custom_field.invalid", await badChoice.ReadErrorCodeAsync());
        Assert.Equal("crm.custom_field.invalid", await badDate.ReadErrorCodeAsync());
        Assert.Equal("crm.custom_field.unknown", await unknown.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task RemovedField_KeepsItsValuesAndItsKey()
    {
        using var owner = await OwnerWithFieldsAsync();
        using var created = await owner.PostAsJsonAsync("/api/v1/crm/parties", Party(new() { ["sektor"] = "Tekstil", ["instagram_hesabi"] = "@abc" }), Cancel);
        var party = (await created.Content.ReadFromJsonAsync<PartyResponse>(Cancel))!;
        var fields = await owner.GetFromJsonAsync<JsonElement>("/api/v1/crm/custom-fields", Cancel);
        var instagram = fields.EnumerateArray().Single(field => field.GetProperty("key").GetString() == "instagram_hesabi").GetProperty("id").GetGuid();

        using var removed = await owner.DeleteAsync($"/api/v1/crm/custom-fields/{instagram}", Cancel);
        using var readded = await owner.PostAsJsonAsync("/api/v1/crm/custom-fields", new { label = "Instagram hesabı", type = "text" }, Cancel);
        using var updated = await owner.PutAsJsonAsync($"/api/v1/crm/parties/{party.Id}", new
        {
            kind = "company", name = "ABC Mobilya", isCustomer = true, isSupplier = false,
            customFields = new Dictionary<string, string?> { ["sektor"] = "Gıda" },
        }, Cancel);
        var after = (await updated.Content.ReadFromJsonAsync<PartyResponse>(Cancel))!;

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal("instagram_hesabi_2", (await readded.Content.ReadFromJsonAsync<JsonElement>(Cancel)).GetProperty("key").GetString());
        Assert.Equal("Gıda", after.CustomFields["sektor"]);
        Assert.Equal("@abc", after.CustomFields["instagram_hesabi"]);
    }

    [Fact]
    public async Task CustomFields_OfAnotherTenant_AreNotSeenOrAccepted()
    {
        using var owner = await OwnerWithFieldsAsync();
        using var other = api.CreateClient();
        using var _ = await other.RegisterAsync();

        var otherFields = await other.GetFromJsonAsync<JsonElement>("/api/v1/crm/custom-fields", Cancel);
        using var foreign = await other.PostAsJsonAsync("/api/v1/crm/parties", Party(new() { ["sektor"] = "Mobilya" }), Cancel);

        Assert.Equal(0, otherFields.GetArrayLength());
        Assert.Equal("crm.custom_field.unknown", await foreign.ReadErrorCodeAsync());
    }
}
