using System.Text;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TNRD.Zeepkist.GTR.DTOs.Rabbit;

namespace TNRD.Zeepkist.GTR.Backend.PersonalBestProcessor.Rabbit;

internal class RabbitWorker : IHostedService
{
    private readonly RabbitOptions options;
    private readonly ItemQueue itemQueue;

    private IConnection connection = null!;
    private IModel channel = null!;

    public RabbitWorker(IOptions<RabbitOptions> options, ItemQueue itemQueue)
    {
        this.itemQueue = itemQueue;
        this.options = options.Value;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ConnectionFactory factory = new()
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.Username,
            Password = options.Password
        };

        connection = factory.CreateConnection();
        channel = connection.CreateModel();

        channel.ExchangeDeclare("records", ExchangeType.Fanout);
        channel.ExchangeDeclare("pb", ExchangeType.Fanout);

        string? queueName = channel.QueueDeclare().QueueName;
        channel.QueueBind(queueName,
            "pb",
            string.Empty);

        EventingBasicConsumer consumer = new(channel);
        consumer.Received += OnReceived;
        channel.BasicConsume(queueName,
            true,
            consumer);

        return Task.CompletedTask;
    }

    private void OnReceived(object? sender, BasicDeliverEventArgs e)
    {
        byte[] body = e.Body.ToArray();
        string message = Encoding.UTF8.GetString(body);
        ProcessPersonalBestRequest request = JsonConvert.DeserializeObject<ProcessPersonalBestRequest>(message)!;
        itemQueue.AddToQueue(request);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        channel.Close();
        connection.Close();
        return Task.CompletedTask;
    }
}
