// Services/ClickProcessorService.cs
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UrlShortener.Models;

namespace UrlShortener.Services
{
    public class ClickProcessorService : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly RabbitMQ.Client.IModel _channel;
        private readonly string _queueName;
        private readonly ILogger<ClickProcessorService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ClickProcessorService(
            ILogger<ClickProcessorService> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;

            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:Host"] ?? "localhost",
                Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
                UserName = configuration["RabbitMQ:Username"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest"
            };

            _queueName = configuration["RabbitMQ:ClicksQueue"] ?? "url_clicks";

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare the queue (same as in the publisher)
            _channel.QueueDeclare(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // Set prefetch count to 1 to ensure fair distribution
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Click Processor Service is starting.");

            stoppingToken.Register(() =>
            {
                _logger.LogInformation("Click Processor Service is stopping.");
                _channel?.Close();
                _connection?.Close();
            });

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (sender, eventArgs) =>
            {
                var content = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
                _logger.LogDebug($"Processing click event: {content}");

                try
                {
                    var clickEvent = JsonConvert.DeserializeObject<ClickEvent>(content);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var urlService = scope.ServiceProvider.GetRequiredService<IUrlShortenerService>();
                        await urlService.IncrementClickCountAsync(clickEvent.ShortUrl);

                        _logger.LogInformation($"Processed click for URL: {clickEvent.ShortUrl}");
                    }

                    // Acknowledge the message
                    _channel.BasicAck(deliveryTag: eventArgs.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing click event");

                    // Reject and requeue the message
                    _channel.BasicNack(
                        deliveryTag: eventArgs.DeliveryTag,
                        multiple: false,
                        requeue: true);
                }
            };

            _channel.BasicConsume(
                queue: _queueName,
                autoAck: false, // Manual acknowledgment
                consumer: consumer);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}