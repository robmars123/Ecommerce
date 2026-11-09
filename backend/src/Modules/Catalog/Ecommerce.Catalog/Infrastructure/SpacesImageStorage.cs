using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Ecommerce.Catalog.Application.Admin;
using Microsoft.Extensions.Options;

namespace Ecommerce.Catalog.Infrastructure;

/// <summary>
/// The only network edge of the image upload (S3-compatible, DigitalOcean Spaces). With blank config it throws before
/// any client exists, so dev and CI never open a connection. Failures otherwise surface as 500 (D12).
/// </summary>
internal sealed class SpacesImageStorage : IImageStorage, IDisposable
{
    public const string CacheControl = "public, max-age=31536000, immutable";

    private readonly CatalogImageStorageOptions _options;
    private readonly Lazy<IAmazonS3> _client;

    public SpacesImageStorage(IOptions<CatalogImageStorageOptions> options)
    {
        _options = options.Value;
        _client = new Lazy<IAmazonS3>(() => new AmazonS3Client(
            new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey),
            new AmazonS3Config
            {
                ServiceURL = _options.ServiceUrl,
                AuthenticationRegion = _options.Region,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            }));
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        if (!_options.IsConfigured)
        {
            throw new ImageStorageNotConfiguredException();
        }
        await _client.Value.PutObjectAsync(BuildRequest(_options.Bucket, key, content, contentType), ct);
    }

    internal static PutObjectRequest BuildRequest(string bucket, string key, Stream content, string contentType)
    {
        PutObjectRequest request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            CannedACL = S3CannedACL.PublicRead,
            UseChunkEncoding = false,
        };
        request.Headers.CacheControl = CacheControl;
        return request;
    }

    public void Dispose()
    {
        if (_client.IsValueCreated)
        {
            _client.Value.Dispose();
        }
    }
}
