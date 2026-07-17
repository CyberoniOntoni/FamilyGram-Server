using Microsoft.Extensions.Options;
using MyTelegram.Core;
using MyTelegram.EventBus.RabbitMQ;
using MyTelegram.FileServer.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MyTelegram.FileServer.BackgroundServices;

/// <summary>
/// Consumes file-lane events from the private exchange used by FileDownloadLaneRouter
/// (mytelegram_file_server_exchange → MyTelegramFileServerRaw).
/// </summary>
public sealed class FileServerLaneConsumer(
    IOptionsMonitor<RabbitMqOptions> rabbitMqOptions,
    IRabbitMqSerializer rabbitMqSerializer,
    IMtpFileRequestHandler requestHandler,
    ILogger<FileServerLaneConsumer> logger) : BackgroundService
{
    private const string FileServerExchange = "mytelegram_file_server_exchange";
    private const string FileServerQueue = "MyTelegramFileServerRaw";
    private static readonly string[] RoutingKeys =
    [
        nameof(DownloadDataReceivedEvent),
        nameof(UploadDataReceivedEvent)
    ];

    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureConsumerAsync(stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FileServer lane consumer failed; retrying");
                await DisposeChannelAsync();
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task EnsureConsumerAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return;
        }

        await DisposeChannelAsync();

        var opts = rabbitMqOptions.CurrentValue;
        var factory = new ConnectionFactory
        {
            HostName = opts.HostName,
            Port = opts.Port,
            UserName = opts.UserName,
            Password = opts.Password,
            AutomaticRecoveryEnabled = true
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(
            exchange: FileServerExchange,
            type: "direct",
            durable: true,
            cancellationToken: cancellationToken);
        await _channel.QueueDeclareAsync(
            queue: FileServerQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await _channel.BasicQosAsync(0, 16, false, cancellationToken);

        foreach (var key in RoutingKeys)
        {
            await _channel.QueueBindAsync(
                queue: FileServerQueue,
                exchange: FileServerExchange,
                routingKey: key,
                cancellationToken: cancellationToken);
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnReceivedAsync;
        await _channel.BasicConsumeAsync(
            queue: FileServerQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);

        logger.LogInformation(
            "FileServer consuming queue {Queue} on exchange {Exchange}",
            FileServerQueue,
            FileServerExchange);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        try
        {
            await ProcessAsync(args.RoutingKey, args.Body);
            if (_channel is not null)
            {
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed processing file-lane message {RoutingKey}", args.RoutingKey);
            if (_channel is not null)
            {
                await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false);
            }
        }
    }

    private async Task ProcessAsync(string routingKey, ReadOnlyMemory<byte> body)
    {
        switch (routingKey)
        {
            case nameof(UploadDataReceivedEvent):
            {
                var evt = rabbitMqSerializer.Deserialize<UploadDataReceivedEvent>(body);
                await requestHandler.HandleUploadAsync(evt);
                return;
            }
            case nameof(DownloadDataReceivedEvent):
            {
                var evt = rabbitMqSerializer.Deserialize<DownloadDataReceivedEvent>(body);
                await requestHandler.HandleDownloadAsync(evt);
                return;
            }
            default:
            {
                // Unknown routing key: try both shapes
                try
                {
                    var upload = rabbitMqSerializer.Deserialize<UploadDataReceivedEvent>(body);
                    await requestHandler.HandleUploadAsync(upload);
                    return;
                }
                catch
                {
                    // fall through
                }

                var download = rabbitMqSerializer.Deserialize<DownloadDataReceivedEvent>(body);
                await requestHandler.HandleDownloadAsync(download);
                return;
            }
        }
    }

    private async Task DisposeChannelAsync()
    {
        try
        {
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }
        }
        catch
        {
            // ignore
        }

        _channel = null;
        _connection = null;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisposeChannelAsync();
        await base.StopAsync(cancellationToken);
    }
}
