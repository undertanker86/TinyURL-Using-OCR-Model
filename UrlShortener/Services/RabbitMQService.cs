// Services/RabbitMQService.cs
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System;
using System.Text;
using UrlShortener.Models;

namespace UrlShortener.Services
{
    public interface IRabbitMQService
    {
        void PublishClickEvent(ClickEvent clickEvent);
    }

    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly IConnection _connection;
        private readonly RabbitMQ.Client.IModel _channel;
        private readonly string _queueName;

        public RabbitMQService(IConfiguration configuration)
        {
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

            // Declare the queue
            _channel.QueueDeclare(
                queue: _queueName,
                durable: true,  // Make queue persistent
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }

        public void PublishClickEvent(ClickEvent clickEvent)
        {
            var message = JsonConvert.SerializeObject(clickEvent);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;  // Make message persistent

            _channel.BasicPublish(
                exchange: "",
                routingKey: _queueName,
                basicProperties: properties,
                body: body);
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}