// Services/UrlShortenerService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using UrlShortener.Data;
using UrlShortener.Models;

namespace UrlShortener.Services
{
    public class UrlShortenerService : IUrlShortenerService
    {
        private readonly AppDbContext _context;
        private readonly string _baseUrl;

        public UrlShortenerService(AppDbContext context, string baseUrl)
        {
            _context = context;
            _baseUrl = baseUrl;
        }

        public async Task<UrlResponse> ShortenUrlAsync(string url, string userId, string email, string customAlias = null, DateTime? expiryDate = null)
        {
            string shortUrl;
            bool isCustomized = !string.IsNullOrEmpty(customAlias);

            if (isCustomized)
            {
                // Kiểm tra xem custom alias đã tồn tại chưa
                var existingUrl = await _context.InfURL.FirstOrDefaultAsync(u => u.NewUrl == customAlias);
                if (existingUrl != null)
                {
                    throw new InvalidOperationException("Custom alias này đã được sử dụng. Vui lòng chọn alias khác.");
                }
                shortUrl = customAlias;
            }
            else
            {
                // Tạo short URL ngẫu nhiên
                shortUrl = await GenerateUniqueShortUrlAsync(url);
            }

            // Nếu không cung cấp expiry date, đặt mặc định là 7 ngày
            if (!expiryDate.HasValue)
            {
                expiryDate = DateTime.UtcNow.AddDays(7);
            }

            var urlInfo = new UrlInfo
            {
                Email = email,
                UserId = userId,
                DefaultUrl = url,
                NewUrl = shortUrl,
                IsCustomized = isCustomized,
                CreatedAt = DateTime.UtcNow,
                ExpiryDate = expiryDate,
                ClickCount = 0,
                IsActive = true
            };

            _context.InfURL.Add(urlInfo);
            await _context.SaveChangesAsync();

            return new UrlResponse
            {
                OriginalUrl = url,
                ShortUrl = $"{_baseUrl}/{shortUrl}",
                CreatedAt = urlInfo.CreatedAt,
                ExpiryDate = urlInfo.ExpiryDate,
                ClickCount = urlInfo.ClickCount
            };
        }

        public async Task<UrlInfo> GetUrlInfoAsync(string shortUrl)
        {
            var urlInfo = await _context.InfURL.FirstOrDefaultAsync(u => u.NewUrl == shortUrl && u.IsActive);

            if (urlInfo == null)
                return null;

            // Kiểm tra URL đã hết hạn chưa
            if (urlInfo.ExpiryDate.HasValue && urlInfo.ExpiryDate.Value < DateTime.UtcNow)
            {
                urlInfo.IsActive = false;
                await _context.SaveChangesAsync();
                return null;
            }

            return urlInfo;
        }

        public async Task<bool> DeleteShortUrlAsync(string shortUrl, string userId)
        {
            var urlInfo = await _context.InfURL.FirstOrDefaultAsync(u => u.NewUrl == shortUrl && u.UserId == userId);

            if (urlInfo == null)
                return false;

            urlInfo.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<UrlResponse>> GetUserUrlsAsync(string userId)
        {
            var userUrls = await _context.InfURL
                .Where(u => u.UserId == userId && u.IsActive)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            return userUrls.Select(u => new UrlResponse
            {
                OriginalUrl = u.DefaultUrl,
                ShortUrl = $"{_baseUrl}/{u.NewUrl}",
                CreatedAt = u.CreatedAt,
                ExpiryDate = u.ExpiryDate,
                ClickCount = u.ClickCount
            });
        }

        public async Task<int> IncrementClickCountAsync(string shortUrl)
        {
            var urlInfo = await _context.InfURL.FirstOrDefaultAsync(u => u.NewUrl == shortUrl && u.IsActive);

            if (urlInfo == null)
                return 0;

            urlInfo.ClickCount++;
            await _context.SaveChangesAsync();
            return urlInfo.ClickCount;
        }

        private async Task<string> GenerateUniqueShortUrlAsync(string originalUrl)
        {
            string shortUrl;
            bool isUnique = false;

            do
            {
                shortUrl = GenerateShortUrl(originalUrl);
                isUnique = !await _context.InfURL.AnyAsync(u => u.NewUrl == shortUrl);
            } while (!isUnique);

            return shortUrl;
        }

        private string GenerateShortUrl(string url)
        {
            // Tạo một chuỗi ngẫu nhiên cho URL
            using var sha256 = SHA256.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(url + Guid.NewGuid().ToString());
            byte[] hashBytes = sha256.ComputeHash(inputBytes);

            // Chuyển đổi hash thành chuỗi Base64 và lấy 8 ký tự đầu
            return Convert.ToBase64String(hashBytes)
                .Replace("/", "_")
                .Replace("+", "-")
                .Replace("=", "")
                .Substring(0, 8);
        }
    }
}