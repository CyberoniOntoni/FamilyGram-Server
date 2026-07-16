using MongoDB.Driver;

namespace MyTelegram.Messenger.Services.Phone;

public static class CallSessionMaintenanceHelper
{
    public const int RequestedTimeoutSeconds = 90;
    public const int HandshakeTimeoutSeconds = 120;
    public const int ConfirmedStaleTimeoutSeconds = 6 * 60 * 60;

    private static readonly string[] HandshakeStates = ["received", "accepted"];
    private static readonly string[] OccupiedStates = ["received", "accepted", "confirmed"];

    public static async Task ExpireStaleSessionsAsync(
        IMongoCollection<CallSessionDocument> collection,
        CancellationToken cancellationToken = default)
    {
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var requestedCutoff = now - RequestedTimeoutSeconds;
        await collection.UpdateManyAsync(
            Builders<CallSessionDocument>.Filter.And(
                Builders<CallSessionDocument>.Filter.Eq(s => s.State, "requested"),
                Builders<CallSessionDocument>.Filter.Lt(s => s.Date, requestedCutoff)),
            Builders<CallSessionDocument>.Update
                .Set(s => s.State, "discarded")
                .Set(s => s.DiscardReason, "disconnect"),
            cancellationToken: cancellationToken);

        var handshakeCutoff = now - HandshakeTimeoutSeconds;
        await collection.UpdateManyAsync(
            Builders<CallSessionDocument>.Filter.And(
                Builders<CallSessionDocument>.Filter.In(s => s.State, HandshakeStates),
                Builders<CallSessionDocument>.Filter.Lt(s => s.Date, handshakeCutoff)),
            Builders<CallSessionDocument>.Update
                .Set(s => s.State, "discarded")
                .Set(s => s.DiscardReason, "disconnect"),
            cancellationToken: cancellationToken);

        var confirmedCutoff = now - ConfirmedStaleTimeoutSeconds;
        await collection.UpdateManyAsync(
            Builders<CallSessionDocument>.Filter.And(
                Builders<CallSessionDocument>.Filter.Eq(s => s.State, "confirmed"),
                Builders<CallSessionDocument>.Filter.Lt(s => s.Date, confirmedCutoff)),
            Builders<CallSessionDocument>.Update
                .Set(s => s.State, "discarded")
                .Set(s => s.DiscardReason, "disconnect"),
            cancellationToken: cancellationToken);
    }

    public static FilterDefinition<CallSessionDocument> BuildCalleeBusyFilter(long calleeId)
    {
        return Builders<CallSessionDocument>.Filter.And(
            Builders<CallSessionDocument>.Filter.Eq(s => s.CalleeId, calleeId),
            Builders<CallSessionDocument>.Filter.In(s => s.State, OccupiedStates));
    }
}