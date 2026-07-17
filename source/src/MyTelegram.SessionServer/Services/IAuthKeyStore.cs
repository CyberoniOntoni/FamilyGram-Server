namespace MyTelegram.SessionServer.Services;

public interface IAuthKeyStore
{
    Task<SessionState?> GetAsync(long authKeyId, CancellationToken cancellationToken = default);
    Task UpsertAsync(SessionState state, CancellationToken cancellationToken = default);
    Task BindUserAsync(long authKeyId, long userId, long permAuthKeyId, long accessHashKeyId, int layer, DeviceType deviceType, CancellationToken cancellationToken = default);
}
