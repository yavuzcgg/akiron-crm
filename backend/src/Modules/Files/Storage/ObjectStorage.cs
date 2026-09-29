using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Akiron.Modules.Files.Storage;

/// <summary>Bound from <c>Storage:S3</c>. MinIO locally; R2 or AWS S3 in production (same API).</summary>
public sealed class S3StorageOptions
{
    public const string SectionName = "Storage:S3";

    public string ServiceUrl { get; set; } = "http://localhost:9010";

    public string Region { get; set; } = "us-east-1";

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = "akiron-files";
}

/// <summary>Where file bytes live (ADR-0005: a provider behind an interface).</summary>
internal interface IObjectStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);
}

internal sealed class S3ObjectStorage : IObjectStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly SemaphoreSlim _bucketCheck = new(1, 1);
    private bool _bucketReady;

    public S3ObjectStorage(IOptions<S3StorageOptions> options)
    {
        var settings = options.Value;
        _bucket = settings.Bucket;
        _client = new AmazonS3Client(
            new BasicAWSCredentials(settings.AccessKey, settings.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = settings.ServiceUrl,
                AuthenticationRegion = settings.Region,

                // MinIO and R2 address buckets by path, not by sub-domain.
                ForcePathStyle = true,
            });
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);
        await _client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var response = await _client.GetObjectAsync(_bucket, key, cancellationToken);
        return response.ResponseStream;
    }

    public void Dispose()
    {
        _client.Dispose();
        _bucketCheck.Dispose();
    }

    /// <summary>Creates the bucket on first use, so a fresh MinIO works without a set-up step.</summary>
    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketReady)
        {
            return;
        }

        await _bucketCheck.WaitAsync(cancellationToken);
        try
        {
            if (!_bucketReady)
            {
                try
                {
                    await _client.PutBucketAsync(_bucket, cancellationToken);
                }
                catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.Conflict)
                {
                    // Already exists (BucketAlreadyOwnedByYou).
                }

                _bucketReady = true;
            }
        }
        finally
        {
            _bucketCheck.Release();
        }
    }
}
