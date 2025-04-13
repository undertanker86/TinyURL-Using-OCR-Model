// Services/IUrlShortenerService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UrlShortener.Models;

namespace UrlShortener.Services
{
    public interface IUrlShortenerService
    {
        Task<UrlResponse> ShortenUrlAsync(string url, string userId, string email, string customAlias = null, DateTime? expiryDate = null);
        Task<UrlInfo> GetUrlInfoAsync(string shortUrl);
        Task<bool> DeleteShortUrlAsync(string shortUrl, string userId);
        Task<IEnumerable<UrlResponse>> GetUserUrlsAsync(string userId);
        Task<int> IncrementClickCountAsync(string shortUrl);
    }
}
