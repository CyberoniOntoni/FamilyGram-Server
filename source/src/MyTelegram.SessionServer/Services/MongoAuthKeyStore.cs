using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MyTelegram.SessionServer.Services;

public sealed class MongoAuthKeyStore(
    IMongoDatabase database,
    ILogger<MongoAuthKeyStore> logger) : IAuthKeyStore
{
    private readonly ConcurrentDictionary<long, SessionState> _cache = new();
    private IMongoCollection<BsonDocument> Collection =>
        database.GetCollection<BsonDocument>("eventflow-authkeyreadmodel");

    public async Task<SessionState?> GetAsync(long authKeyId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(authKeyId, out var cached))
        {
            return cached;
        }

        var doc = await Collection.Find(Builders<BsonDocument>.Filter.Eq("AuthKeyId", authKeyId))
            .FirstOrDefaultAsync(cancellationToken);
        if (doc is null)
        {
            return null;
        }

        var state = FromDocument(doc);
        _cache[authKeyId] = state;
        return state;
    }

    public async Task UpsertAsync(SessionState state, CancellationToken cancellationToken = default)
    {
        _cache[state.AuthKeyId] = state;

        var id = $"authkey-{Guid.NewGuid():D}";
        var existing = await Collection.Find(Builders<BsonDocument>.Filter.Eq("AuthKeyId", state.AuthKeyId))
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            id = existing["_id"].AsString;
        }

        var doc = new BsonDocument
        {
            ["_id"] = id,
            ["AuthKeyId"] = state.AuthKeyId,
            ["Data"] = state.AuthKey,
            ["IsActive"] = true,
            ["LastUpdateTime"] = DateTime.UtcNow,
            ["ServerSalt"] = state.ServerSalt,
            ["UserId"] = state.UserId,
            ["Layer"] = state.Layer,
            ["DeviceType"] = (int)state.DeviceType,
            ["AccessHashKeyId"] = state.AccessHashKeyId,
            ["MediaOnly"] = false,
            ["Version"] = 1L
        };

        await Collection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("AuthKeyId", state.AuthKeyId),
            doc,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        logger.LogDebug("Upserted auth key {AuthKeyId:x}", state.AuthKeyId);
    }

    public async Task BindUserAsync(
        long authKeyId,
        long userId,
        long permAuthKeyId,
        long accessHashKeyId,
        int layer,
        DeviceType deviceType,
        CancellationToken cancellationToken = default)
    {
        var state = await GetAsync(authKeyId, cancellationToken);
        if (state is null)
        {
            logger.LogWarning("BindUser: auth key {AuthKeyId:x} not found", authKeyId);
            return;
        }

        state.UserId = userId;
        state.PermAuthKeyId = permAuthKeyId != 0 ? permAuthKeyId : authKeyId;
        state.AccessHashKeyId = accessHashKeyId;
        state.Layer = layer > 0 ? layer : state.Layer;
        state.DeviceType = deviceType;
        await UpsertAsync(state, cancellationToken);
        logger.LogInformation("Bound user {UserId} to authKey {AuthKeyId:x} layer={Layer}", userId, authKeyId, state.Layer);
    }

    public async Task<IReadOnlyList<SessionState>> GetOnlineByUserIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        var list = new List<SessionState>();
        foreach (var s in _cache.Values)
        {
            if (s.UserId == userId && !string.IsNullOrEmpty(s.ConnectionId))
            {
                list.Add(s);
            }
        }

        if (list.Count > 0)
        {
            return list;
        }

        // Fallback: load active keys for user from Mongo (may lack ConnectionId until next client packet)
        var docs = await Collection.Find(Builders<BsonDocument>.Filter.Eq("UserId", userId))
            .ToListAsync(cancellationToken);
        foreach (var doc in docs)
        {
            var state = FromDocument(doc);
            _cache[state.AuthKeyId] = state;
            if (!string.IsNullOrEmpty(state.ConnectionId))
            {
                list.Add(state);
            }
        }

        return list;
    }

    private static SessionState FromDocument(BsonDocument doc)
    {
        var data = doc["Data"].AsBsonBinaryData.Bytes;
        return new SessionState
        {
            AuthKeyId = doc["AuthKeyId"].ToInt64(),
            AuthKey = data,
            ServerSalt = doc.GetValue("ServerSalt", 0L).ToInt64(),
            UserId = doc.GetValue("UserId", 0L).ToInt64(),
            PermAuthKeyId = doc.GetValue("AuthKeyId", 0L).ToInt64(),
            AccessHashKeyId = doc.GetValue("AccessHashKeyId", 0L).ToInt64(),
            Layer = doc.GetValue("Layer", Layers.LayerLatest).ToInt32(),
            DeviceType = (DeviceType)doc.GetValue("DeviceType", 0).ToInt32(),
            IsPermanent = true
        };
    }
}
