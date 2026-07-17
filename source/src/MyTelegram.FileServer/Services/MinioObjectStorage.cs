using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using MyTelegram.FileServer.Options;

namespace MyTelegram.FileServer.Services;

public sealed class MinioObjectStorage(
    IMinioClient minioClient,
    IOptions<MinioOptions> options,
    ILogger<MinioObjectStorage> logger) : IObjectStorage
{
    private readonly MinioOptions _options = options.Value;
    private int _bucketReady;

    public async Task EnsureBucketAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _bucketReady, 1, 0) == 1)
        {
            return;
        }

        try
        {
            var exists = await minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_options.BucketName),
                cancellationToken);
            if (!exists && _options.CreateBucketIfNotExists)
            {
                await minioClient.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_options.BucketName),
                    cancellationToken);
                logger.LogInformation("Created MinIO bucket {Bucket}", _options.BucketName);
            }

            _bucketReady = 1;
        }
        catch
        {
            Interlocked.Exchange(ref _bucketReady, 0);
            throw;
        }
    }

    public async Task PutAsync(string objectName, Stream data, long size, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);
        await minioClient.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(objectName)
                .WithStreamData(data)
                .WithObjectSize(size)
                .WithContentType(contentType),
            cancellationToken);
    }

    public Task PutBytesAsync(string objectName, ReadOnlyMemory<byte> data, string contentType, CancellationToken cancellationToken = default)
    {
        var bytes = data.ToArray();
        return PutAsync(objectName, new MemoryStream(bytes), bytes.Length, contentType, cancellationToken);
    }

    public async Task<byte[]?> GetRangeAsync(string objectName, long offset, int limit, CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);
        try
        {
            await using var ms = new MemoryStream();
            await minioClient.GetObjectAsync(
                new GetObjectArgs()
                    .WithBucket(_options.BucketName)
                    .WithObject(objectName)
                    .WithOffsetAndLength(offset, limit)
                    .WithCallbackStream(stream => stream.CopyTo(ms)),
                cancellationToken);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "GetRange failed for {Object} offset={Offset} limit={Limit}", objectName, offset, limit);
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string objectName, CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);
        try
        {
            await minioClient.StatObjectAsync(
                new StatObjectArgs().WithBucket(_options.BucketName).WithObject(objectName),
                cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<long?> GetSizeAsync(string objectName, CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);
        try
        {
            var stat = await minioClient.StatObjectAsync(
                new StatObjectArgs().WithBucket(_options.BucketName).WithObject(objectName),
                cancellationToken);
            return stat.Size;
        }
        catch
        {
            return null;
        }
    }
}
