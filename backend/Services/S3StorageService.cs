using Amazon.S3;
using Amazon.S3.Model;

namespace Chatbot.Api.Services;

public interface IStorageService
{
    string BuildKey(string userId, string fileName);
    Task<string> PresignPutAsync(string key, string contentType, TimeSpan ttl);
    Task<string> PresignGetAsync(string key, TimeSpan ttl);
    Task<Stream> GetObjectAsync(string key, CancellationToken ct);
    Task PutObjectAsync(string key, Stream content, string contentType, CancellationToken ct);
}

public class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public S3StorageService(IAmazonS3 s3, IConfiguration config)
    {
        _s3 = s3;
        _bucket = config["Storage:S3Bucket"] ?? throw new InvalidOperationException("Storage:S3Bucket not set");
    }

    public string BuildKey(string userId, string fileName)
        => $"users/{userId}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}/{fileName}";

    public Task<string> PresignPutAsync(string key, string contentType, TimeSpan ttl)
        => Task.FromResult(_s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.Add(ttl)
        }));

    public Task<string> PresignGetAsync(string key, TimeSpan ttl)
        => Task.FromResult(_s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(ttl)
        }));

    public async Task<Stream> GetObjectAsync(string key, CancellationToken ct)
    {
        var resp = await _s3.GetObjectAsync(_bucket, key, ct);
        return resp.ResponseStream;
    }

    public Task PutObjectAsync(string key, Stream content, string contentType, CancellationToken ct)
        => _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType
        }, ct);
}
