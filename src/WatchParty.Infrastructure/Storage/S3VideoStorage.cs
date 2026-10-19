using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WatchParty.Application.Abstractions.Services;

namespace WatchParty.Infrastructure.Storage;

/// <summary>
/// Local disk for work in progress, S3-compatible bucket for finished files. A file that is still on disk is
/// served from disk; once offloaded it is served by a signed URL. No network call is needed to build a URL.
/// </summary>
internal sealed class S3VideoStorage : IVideoStorage, IDisposable
{
    private readonly LocalVideoStorage _local;
    private readonly S3Options _options;
    private readonly IAmazonS3 _s3;
    private readonly ILogger<S3VideoStorage> _logger;

    public S3VideoStorage(LocalVideoStorage local, IOptions<S3Options> options, ILogger<S3VideoStorage> logger)
    {
        _local = local;
        _options = options.Value;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ForcePathStyle = _options.ForcePathStyle,
            // R2/MinIO don't support the newer default checksums of SDK v4.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        if (string.IsNullOrWhiteSpace(_options.ServiceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region);
        }
        else
        {
            config.ServiceURL = _options.ServiceUrl;
            config.AuthenticationRegion = _options.Region;
        }
        _s3 = new AmazonS3Client(new BasicAWSCredentials(_options.AccessKey, _options.SecretKey), config);
    }

    // ----- local work area -----

    public Task<StoredFile> SaveAsync(Stream content, string extension, long? maxBytes, CancellationToken cancellationToken) =>
        _local.SaveAsync(content, extension, maxBytes, cancellationToken);

    public string CreateKey(string extension) => _local.CreateKey(extension);
    public string GetPhysicalPath(string key) => _local.GetPhysicalPath(key);
    public bool Exists(string key) => _local.Exists(key) || Locate(key) is not null;
    public string CreateFolderKey(string prefix) => _local.CreateFolderKey(prefix);
    public string GetFolderPath(string folderKey) => _local.GetFolderPath(folderKey);
    public string? ResolveFolderFile(string folderKey, string relativePath) => _local.ResolveFolderFile(folderKey, relativePath);
    public long? GetAvailableFreeBytes() => _local.GetAvailableFreeBytes();

    // ----- reading -----

    public MediaLocation? Locate(string key) =>
        _local.Locate(key) ?? MediaLocation.Remote(SignedUrl(ObjectKey(key)));

    public MediaLocation? LocateInFolder(string folderKey, string relativePath)
    {
        if (_local.LocateInFolder(folderKey, relativePath) is { } local) return local;
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains("..") || Path.IsPathRooted(relativePath)) return null;
        return MediaLocation.Remote(SignedUrl(ObjectKey($"{folderKey}/{relativePath.Replace('\\', '/')}")));
    }

    // ----- moving finished files to the bucket -----

    public async Task OffloadAsync(string key, string contentType, CancellationToken cancellationToken)
    {
        var path = _local.GetPhysicalPath(key);
        if (!File.Exists(path)) return;

        await UploadAsync(path, ObjectKey(key), contentType, cancellationToken);
        File.Delete(path);
    }

    public async Task OffloadFolderAsync(string folderKey, Func<string, bool> include, CancellationToken cancellationToken)
    {
        var folder = _local.GetFolderPath(folderKey);
        if (!Directory.Exists(folder)) return;

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(folder, file).Replace('\\', '/');
            if (!include(relative)) continue;

            var contentType = Path.GetExtension(file) == ".ts" ? "video/mp2t" : "application/octet-stream";
            await UploadAsync(file, ObjectKey($"{folderKey}/{relative}"), contentType, cancellationToken);
            File.Delete(file);
        }
    }

    // ----- deleting (local and remote) -----

    public void Delete(string key)
    {
        _local.Delete(key);
        RunInBackground(() => _s3.DeleteObjectAsync(_options.Bucket, ObjectKey(key)), key);
    }

    public void DeleteFolder(string folderKey)
    {
        _local.DeleteFolder(folderKey);
        RunInBackground(async () =>
        {
            var prefix = ObjectKey(folderKey + "/");
            ListObjectsV2Response page;
            string? token = null;
            do
            {
                page = await _s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _options.Bucket, Prefix = prefix, ContinuationToken = token });
                if (page.S3Objects is { Count: > 0 } objects)
                {
                    await _s3.DeleteObjectsAsync(new DeleteObjectsRequest
                    {
                        BucketName = _options.Bucket,
                        Objects = objects.Select(o => new KeyVersion { Key = o.Key }).ToList()
                    });
                }
                token = page.NextContinuationToken;
            }
            while (page.IsTruncated == true);
        }, folderKey);
    }

    public void Dispose() => _s3.Dispose();

    private async Task UploadAsync(string path, string objectKey, string contentType, CancellationToken cancellationToken)
    {
        using var transfer = new TransferUtility(_s3); // multipart for big movies
        await transfer.UploadAsync(new TransferUtilityUploadRequest
        {
            FilePath = path,
            BucketName = _options.Bucket,
            Key = objectKey,
            ContentType = contentType
        }, cancellationToken);
    }

    private string SignedUrl(string objectKey)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(_options.UrlExpiryMinutes),
        };
        if (_options.ServiceUrl?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true) request.Protocol = Protocol.HTTP;
        return _s3.GetPreSignedURL(request);
    }

    private string ObjectKey(string key) => _options.KeyPrefix + key;

    private void RunInBackground(Func<Task> action, string what) => _ = Task.Run(async () =>
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete {What} from remote storage", what);
        }
    });
}
