using System.Net;
using System.Net.Http.Json;
using System.Text;
using Akiron.Modules.Files.Features;
using Akiron.Modules.Identity.Features.Sessions;
using Akiron.Modules.Timeline.Features;

namespace Akiron.Tests.Integration.Files;

/// <summary>Uploads go to object storage (MinIO in a container); rows keep the tenant boundary.</summary>
public sealed class FileTests(ApiFixture api)
{
    [Fact]
    public async Task Upload_ThenDownload_ReturnsTheSameBytesAsAnAttachment()
    {
        using var client = api.CreateClient();
        var session = await RegisterAsync(client);
        var bytes = Encoding.UTF8.GetBytes("Teklif taslağı — sürüm 3");

        using var upload = await UploadAsync(client, "teklif-v3.txt", bytes, "workspace", session.TenantId);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var file = await upload.Content.ReadFromJsonAsync<FileResponse>(TestContext.Current.CancellationToken);

        using var download = await client.GetAsync(ContentUri(file!.Id), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Upload_OfAnExecutable_IsRefused()
    {
        using var client = api.CreateClient();
        var session = await RegisterAsync(client);

        using var upload = await UploadAsync(client, "fatura.pdf.exe", [0x4D, 0x5A], "workspace", session.TenantId);

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
        Assert.Equal("files.file.type_refused", await upload.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Upload_ToAnotherWorkspace_IsRefused()
    {
        using var mine = api.CreateClient();
        await RegisterAsync(mine);
        using var theirs = api.CreateClient();
        var other = await RegisterAsync(theirs);

        using var upload = await UploadAsync(mine, "not.txt", [1, 2, 3], "workspace", other.TenantId);

        Assert.Equal("files.subject.unknown", await upload.ReadErrorCodeAsync());
    }

    [Fact]
    public async Task Download_FromAnotherTenant_IsNotFound()
    {
        using var owner = api.CreateClient();
        var session = await RegisterAsync(owner);
        using var upload = await UploadAsync(owner, "gizli.txt", [1, 2, 3], "workspace", session.TenantId);
        var file = await upload.Content.ReadFromJsonAsync<FileResponse>(TestContext.Current.CancellationToken);

        using var intruder = api.CreateClient();
        await RegisterAsync(intruder);
        using var download = await intruder.GetAsync(ContentUri(file!.Id), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
    }

    [Fact]
    public async Task Delete_HidesTheFileFromListAndDownload()
    {
        using var client = api.CreateClient();
        var session = await RegisterAsync(client);
        using var upload = await UploadAsync(client, "eski.txt", [1], "workspace", session.TenantId);
        var file = await upload.Content.ReadFromJsonAsync<FileResponse>(TestContext.Current.CancellationToken);

        using var delete = await client.DeleteAsync(new Uri($"/api/v1/files/{file!.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        var list = await client.GetFromJsonAsync<List<FileResponse>>($"/api/v1/files?subjectType=workspace&subjectId={session.TenantId}", TestContext.Current.CancellationToken);
        using var download = await client.GetAsync(ContentUri(file.Id), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty(list!);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
    }

    [Fact]
    public async Task Upload_AppearsOnTheTimeline()
    {
        using var client = api.CreateClient();
        var session = await RegisterAsync(client);
        using var _ = await UploadAsync(client, "brief.pdf", [37, 80, 68, 70], "workspace", session.TenantId);

        await api.DeliverOutboxAsync();
        var page = await client.GetFromJsonAsync<TimelinePageResponse>($"/api/v1/timeline/workspace/{session.TenantId}", TestContext.Current.CancellationToken);

        var entry = page!.Items[0];
        Assert.Equal("files.file.uploaded", entry.Type);
        Assert.Equal("brief.pdf", entry.Payload.GetProperty("fileName").GetString());
    }

    private static Uri ContentUri(Guid id) => new($"/api/v1/files/{id}/content", UriKind.Relative);

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string fileName, byte[] bytes, string subjectType, Guid subjectId)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new("application/octet-stream");
        form.Add(content, "file", fileName);
        form.Add(new StringContent(subjectType), "subjectType");
        form.Add(new StringContent(subjectId.ToString()), "subjectId");

        return await client.PostAsync(new Uri("/api/v1/files", UriKind.Relative), form, TestContext.Current.CancellationToken);
    }

    private static async Task<SessionResponse> RegisterAsync(HttpClient client)
    {
        using var response = await client.RegisterAsync();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(TestContext.Current.CancellationToken))!;
    }
}
