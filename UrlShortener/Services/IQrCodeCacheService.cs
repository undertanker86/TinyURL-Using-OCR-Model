using System.Threading.Tasks;

namespace UrlShortener.Services
{
    public interface IQrCodeCacheService
    {
        Task SaveQrAsync(string shortUrl, string base64Qr);
        Task<string> GetQrAsync(string shortUrl);
        Task DeleteQrAsync(string shortUrl);
    }

}
