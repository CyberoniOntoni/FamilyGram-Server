using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MyTelegram.FileServer.Options;

namespace MyTelegram.FileServer.Services;

public sealed class MongoFilePartStore(
    IMongoDatabase database,
    ILogger<MongoFilePartStore> logger) : IFilePartStore
{
    private IMongoCollection<BsonDocument> Parts => database.GetCollection<BsonDocument>("file_parts");

    public async Task SavePartAsync(long userId, long fileId, int filePart, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        var id = $"{userId}_{fileId}_{filePart}";
        var doc = new BsonDocument
        {
            ["_id"] = id,
            ["UserId"] = userId,
            ["FileId"] = fileId,
            ["FilePart"] = filePart,
            ["Bytes"] = bytes.ToArray(),
            ["Size"] = bytes.Length,
            ["UploadedAt"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        await Parts.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            doc,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
        logger.LogDebug("Saved file part UserId={UserId} FileId={FileId} Part={Part} Size={Size}", userId, fileId, filePart, bytes.Length);
    }

    public Task SaveBigPartAsync(long userId, long fileId, int filePart, int fileTotalParts, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        => SavePartAsync(userId, fileId, filePart, bytes, cancellationToken);

    public async Task<byte[]?> AssembleAsync(long userId, long fileId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("UserId", userId),
            Builders<BsonDocument>.Filter.Eq("FileId", fileId));
        var parts = await Parts.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("FilePart"))
            .ToListAsync(cancellationToken);
        if (parts.Count == 0)
        {
            return null;
        }

        return ConcatParts(parts);
    }

    public async Task<byte[]?> AssembleByFileIdAsync(long fileId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("FileId", fileId);
        var parts = await Parts.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("FilePart"))
            .ToListAsync(cancellationToken);
        if (parts.Count == 0)
        {
            return null;
        }

        return ConcatParts(parts);
    }

    private static byte[] ConcatParts(List<BsonDocument> parts)
    {
        using var ms = new MemoryStream();
        foreach (var part in parts)
        {
            var bytes = part["Bytes"].AsByteArray;
            ms.Write(bytes, 0, bytes.Length);
        }

        return ms.ToArray();
    }
}
