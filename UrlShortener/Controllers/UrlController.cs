using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using UrlShortener.Models;
using UrlShortener.Services;

namespace UrlShortener.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UrlController : ControllerBase
    {
        private readonly IUrlShortenerService _urlShortenerService;
        private readonly ILogger<UrlController> _logger;
        private readonly IQrCodeCacheService _qrCodeCacheService;
        private readonly IRabbitMQService _rabbitMQService; // Add this line
        public UrlController(
            IUrlShortenerService urlShortenerService,
            ILogger<UrlController> logger,
            IQrCodeCacheService qrCodeCacheService,
            IRabbitMQService rabbitMQService) // Add this parameter
        {
            _urlShortenerService = urlShortenerService;
            _logger = logger;
            _qrCodeCacheService = qrCodeCacheService;
            _rabbitMQService = rabbitMQService; // Add this line
        }

        [HttpGet]
        [Route("~/")]
        public IActionResult Index()
        {
            return Ok(new { message = "URL Shortener Service" });
        }

        //[HttpGet]
        //[Route("~/{shortUrl}")]
        //public async Task<ActionResult> RedirectToOriginalUrlFromRoot(string shortUrl)
        //{
        //    try
        //    {
        //        _logger.LogInformation($"Redirecting short URL: {shortUrl}");

        //        var urlInfo = await _urlShortenerService.GetUrlInfoAsync(shortUrl);

        //        if (urlInfo == null)
        //        {
        //            _logger.LogWarning($"Short URL not found: {shortUrl}");
        //            return NotFound(new { error = "URL doesn't exist or has expired" });
        //        }

        //        await _urlShortenerService.IncrementClickCountAsync(shortUrl);

        //        _logger.LogInformation($"Redirecting to: {urlInfo.DefaultUrl}");
        //        return Redirect(urlInfo.DefaultUrl); // Redirect ngay lập tức
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, $"Error redirecting short URL: {shortUrl}");
        //        return StatusCode(500, new { error = "An error occurred while redirecting" });
        //    }

        // In UrlController.cs - Update the RedirectToOriginalUrlFromRoot method
        [HttpGet]
        [Route("~/{shortUrl}")]
        public async Task<ActionResult> RedirectToOriginalUrlFromRoot(string shortUrl)
        {
            try
            {
                _logger.LogInformation($"Redirecting short URL: {shortUrl}");

                var urlInfo = await _urlShortenerService.GetUrlInfoAsync(shortUrl);

                if (urlInfo == null)
                {
                    _logger.LogWarning($"Short URL not found: {shortUrl}");
                    return NotFound(new { error = "URL doesn't exist or has expired" });
                }

                // Instead of directly incrementing, publish a message
                var clickEvent = new ClickEvent
                {
                    ShortUrl = shortUrl,
                    ClickedAt = DateTime.UtcNow,
                    UserAgent = Request.Headers["User-Agent"].ToString(),
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    ReferrerUrl = Request.Headers["Referer"].ToString()
                };

                _rabbitMQService.PublishClickEvent(clickEvent);
                _logger.LogInformation($"Click event published for: {shortUrl}");

                _logger.LogInformation($"Redirecting to: {urlInfo.DefaultUrl}");
                return Redirect(urlInfo.DefaultUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error redirecting short URL: {shortUrl}");
                return StatusCode(500, new { error = "An error occurred while redirecting" });
            }
        }

        [Authorize]
        [HttpPost]
        [Route("shorten")]
        public async Task<ActionResult<UrlResponse>> ShortenUrl([FromBody] CreateUrlRequest request)
        {
            try
            {
                // Log toàn bộ request để debug
                _logger.LogInformation("Received shorten URL request: {RequestUrl}, CustomAlias: {CustomAlias}, UserId: {UserId}, Email: {Email}",
                    request.Url,
                    request.CustomAlias,
                    request.UserId,
                    request.Email);

                // Các kiểm tra cơ bản
                if (string.IsNullOrWhiteSpace(request.Url))
                {
                    return BadRequest(new
                    {
                        status = "error",
                        message = "URL không được để trống"
                    });
                }

                // Kiểm tra URL hợp lệ
                if (!Uri.TryCreate(request.Url, UriKind.Absolute, out Uri uriResult) ||
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    return BadRequest(new
                    {
                        status = "error",
                        message = "URL không hợp lệ"
                    });
                }

                // Lấy thông tin user từ token nếu không có trong request
                string userId = request.UserId ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                string email = request.Email ?? User.FindFirst(ClaimTypes.Email)?.Value;

                _logger.LogInformation("User Info - ID: {UserId}, Email: {Email}", userId, email);

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning("Missing user information");
                    return Unauthorized(new
                    {
                        status = "error",
                        message = "Thông tin người dùng không hợp lệ"
                    });
                }

                var response = await _urlShortenerService.ShortenUrlAsync(
                    request.Url,
                    userId,
                    email,
                    request.CustomAlias,
                    request.ExpiryDate);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chi tiết lỗi khi rút gọn URL");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "Có lỗi xảy ra khi rút gọn URL",
                    details = ex.Message
                });
            }
        }

        [HttpGet]
        [Route("{shortUrl}")]
        public async Task<ActionResult> GetUrlInfo(string shortUrl)
        {
            var urlInfo = await _urlShortenerService.GetUrlInfoAsync(shortUrl);

            if (urlInfo == null)
            {
                return NotFound(new { error = "URL doesn't exist or has expired" });
            }

            return Ok(urlInfo);
        }

        [HttpGet]
        [Authorize]
        [Route("list")]
        public async Task<ActionResult<IEnumerable<UrlResponse>>> GetUserUrls()
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            _logger.LogInformation($"Getting URLs for user ID: {userId}");

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User ID not found in token" });
            }

            var urls = await _urlShortenerService.GetUserUrlsAsync(userId);
            return Ok(urls);
        }

        [HttpDelete]
        [Authorize]
        [Route("{shortUrl}")]
        public async Task<ActionResult> DeleteShortUrl(string shortUrl)
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            _logger.LogInformation($"Deleting URL {shortUrl} for user ID: {userId}");

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { error = "User ID not found in token" });
            }

            bool result = await _urlShortenerService.DeleteShortUrlAsync(shortUrl, userId);

            if (!result)
            {
                return NotFound(new { error = "URL doesn't exist or doesn't belong to you" });
            }

            //  XÓA luôn QR trong Redis nếu có
            await _qrCodeCacheService.DeleteQrAsync(shortUrl);
            _logger.LogInformation($"Deleted QR from Redis for short URL: {shortUrl}");

            return Ok(new { message = "URL and associated QR code deleted" });
        }


        [Authorize]
        [HttpGet]
        [Route("auth-test")]
        public IActionResult AuthTest()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = User.FindFirst(ClaimTypes.Email)?.Value;

            _logger.LogInformation($"Auth test called for user: ID={userId}, Email={email}");

            return Ok(new
            {
                message = "Authentication successful",
                userId,
                email,
                claims = User.Claims.Select(c => new { type = c.Type, value = c.Value }).ToList()
            });
        }
        [Authorize]
        [HttpPost]
        [Route("{shortUrl}/qr")]
        public async Task<IActionResult> SaveQrCode(string shortUrl, [FromBody] QrCodeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Base64Qr))
            {
                return BadRequest(new { message = "QR code base64 is missing" });
            }

            await _qrCodeCacheService.SaveQrAsync(shortUrl, dto.Base64Qr);
            _logger.LogInformation($"Saved QR for {shortUrl} in Redis.");
            return Ok(new { message = "QR saved to Redis" });
        }
        [Authorize]
        [HttpGet]
        [Route("{shortUrl}/qr")]
        public async Task<IActionResult> GetQrCode(string shortUrl)
        {
            var qrBase64 = await _qrCodeCacheService.GetQrAsync(shortUrl);

            if (string.IsNullOrEmpty(qrBase64))
            {
                return NotFound(new { message = "QR code not found in cache" });
            }

            return Ok(new { shortUrl, qrBase64 });
        }

    }
}