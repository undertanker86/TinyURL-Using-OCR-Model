namespace UrlShortener.Services
{
    using StackExchange.Redis;
    using System.Threading.Tasks;
    using System;

    public class QrCodeCacheService : IQrCodeCacheService
    {
        private readonly IDatabase _redis;

        public QrCodeCacheService(IConnectionMultiplexer redis)
        {
            _redis = redis.GetDatabase();
        }

        public async Task SaveQrAsync(string shortUrl, string base64Qr)
        {
            string key = $"qr:{shortUrl}";
            await _redis.StringSetAsync(key, base64Qr, TimeSpan.FromDays(1));
        }

        public async Task<string> GetQrAsync(string shortUrl)
        {
            string key = $"qr:{shortUrl}";
            return await _redis.StringGetAsync(key);
        }
        public async Task DeleteQrAsync(string shortUrl)
        {
            string key = $"qr:{shortUrl}";
            await _redis.KeyDeleteAsync(key);
        }
    }
}
