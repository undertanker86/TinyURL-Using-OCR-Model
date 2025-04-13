// Services/ExpiredUrlCleanupService.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UrlShortener.Data;

namespace UrlShortener.Services
{
    public class ExpiredUrlCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ExpiredUrlCleanupService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Kiểm tra mỗi giờ

        public ExpiredUrlCleanupService(
            IServiceProvider serviceProvider,
            ILogger<ExpiredUrlCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Expired URL Cleanup Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Checking for expired URLs");

                try
                {
                    await CleanupExpiredUrls();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cleaning up expired URLs");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task CleanupExpiredUrls()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;
            var expiredUrls = await dbContext.InfURL
                .Where(u => u.IsActive && u.ExpiryDate.HasValue && u.ExpiryDate.Value < now)
                .ToListAsync();

            if (expiredUrls.Any())
            {
                _logger.LogInformation($"Found {expiredUrls.Count} expired URLs to deactivate");

                foreach (var url in expiredUrls)
                {
                    url.IsActive = false;
                    _logger.LogInformation($"Deactivating URL: {url.NewUrl}");
                }

                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Expired URLs have been deactivated");
            }
            else
            {
                _logger.LogInformation("No expired URLs found");
            }
        }
    }
}