namespace MyTelegram.Messenger.CommandServer.DomainEventHandlers;

public class SendAppCodeEventHandler(
    ILogger<SendAppCodeEventHandler> logger,
    IEventBus eventBus,
    IMessageAppService messageAppService,
    IRandomHelper randomHelper)
    :
        ISubscribeSynchronousTo<AppCodeAggregate, AppCodeId, AppCodeCreatedEvent>,
        ISubscribeSynchronousTo<AppCodeAggregate, AppCodeId, AppCodeResentEvent>
{
    public Task HandleAsync(IDomainEvent<AppCodeAggregate, AppCodeId, AppCodeCreatedEvent> domainEvent,
        CancellationToken cancellationToken)
    {
        return SendCodeAsync(domainEvent.AggregateEvent.UserId, domainEvent.AggregateEvent.PhoneNumber,
            domainEvent.AggregateEvent.Code, domainEvent.AggregateEvent.Expire);
    }

    public Task HandleAsync(IDomainEvent<AppCodeAggregate, AppCodeId, AppCodeResentEvent> domainEvent,
        CancellationToken cancellationToken)
    {
        return SendCodeAsync(domainEvent.AggregateEvent.UserId, domainEvent.AggregateEvent.PhoneNumber,
            domainEvent.AggregateEvent.Code, domainEvent.AggregateEvent.Expire);
    }

    private async Task SendCodeAsync(long userId, string phoneNumber, string code, int expire)
    {
        logger.LogInformation("### Send app code: phoneNumber: {PhoneNumber}, code: {Code}", phoneNumber, code);
        await eventBus.PublishAsync(new AppCodeCreatedIntegrationEvent(userId, phoneNumber, code, expire));

        if (userId != 0)
        {
            var message =
                $"Login code: {code}. Do not give this code to anyone, even if they say they are from Telegram!\n\nThis code can be used to log in to your Telegram account. We never ask it for anything else.\n\nIf you didn't request this code by trying to log in on another device, simply ignore this message.\n\nPowered by Testgram\nhttps://github.com/glebxdlolreal/testgram";
            var entities = new TVector<IMessageEntity>
            {
                new TMessageEntityBold { Offset = 0, Length = 11 },
                new TMessageEntitySpoiler{Offset = 12,Length = code.Length},
                new TMessageEntityBold { Offset = 22, Length = 3 },
                new TMessageEntityBold { Offset = message.Length-48, Length = 11 },

            };

            var sendMessageInput = new SendMessageInput(
                RequestInfo.Empty with
                {
                    UserId = MyTelegramConsts.NotificationServiceUserId,
                    Layer = MyTelegramConsts.Layer,
                    Date = DateTime.UtcNow.ToTimestamp(),
                    RequestId = Guid.NewGuid()
                },
                MyTelegramConsts.NotificationServiceUserId,
                new Peer(PeerType.User, userId),
                message,
                randomHelper.NextInt64(),
                entities: entities
            );

            await messageAppService.SendMessageAsync([sendMessageInput]);
        }
    }
}
