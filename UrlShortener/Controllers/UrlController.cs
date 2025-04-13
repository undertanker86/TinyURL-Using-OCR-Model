// Controllers/UrlController.cs
using System;
using System.Collections.Generic;
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

        public UrlController(IUrlShortenerService urlShortenerService, ILogger<UrlController> logger)
        {
            _urlShortenerService = urlShortenerService;
            _logger = logger;
        }

        // Thêm route gốc cho redirect - điều này cho phép cả hai đường dẫn đều hoạt động
        [HttpGet]
        [Route("~/")]  // Route gốc
        public IActionResult Index()
        {
            // Trang chủ tùy chọn hoặc chuyển hướng
            return Ok(new { message = "URL Shortener Service" });
        }

        // Điều chỉnh route cho ngắn URL - thêm đường dẫn gốc
        [HttpGet]
        [Route("~/{shortUrl}")]  // Quan trọng: Route này đặt ở mức gốc (~/) 
        public async Task<ActionResult> RedirectToOriginalUrlFromRoot(string shortUrl)
        {
            try
            {
                _logger.LogInformation($"Redirecting short URL: {shortUrl}");

                var urlInfo = await _urlShortenerService.GetUrlInfoAsync(shortUrl);

                if (urlInfo == null)
                {
                    _logger.LogWarning($"Short URL not found: {shortUrl}");
                    return NotFound(new { error = "URL không tồn tại hoặc đã hết hạn" });
                }

                // Tăng số lượt click
                await _urlShortenerService.IncrementClickCountAsync(shortUrl);

                _logger.LogInformation($"Redirecting to: {urlInfo.DefaultUrl}");
                return Redirect(urlInfo.DefaultUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error redirecting short URL: {shortUrl}");
                return StatusCode(500, new { error = "Đã xảy ra lỗi khi chuyển hướng" });
            }
        }



        //[Authorize]
        [HttpPost]
        [Route("shorten")]
        public async Task<ActionResult<UrlResponse>> ShortenUrl([FromBody] CreateUrlRequest request)
        {
            try
            {
                // Logic hiện tại
                string userId = "test-user";
                string email = "test@example.com";

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
                // Trả về chi tiết lỗi thay vì thông báo chung chung
                return StatusCode(500, new
                {
                    error = "Đã xảy ra lỗi khi rút gọn URL",
                    message = ex.Message,
                    stackTrace = ex.StackTrace,
                    innerException = ex.InnerException?.Message
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
                return NotFound(new { error = "URL không tồn tại hoặc đã hết hạn" });
            }

            return Ok(urlInfo);
        }

        [HttpGet]
        //[Authorize]
        [Route("list")]
        public async Task<ActionResult<IEnumerable<UrlResponse>>> GetUserUrls()
        {
            string userId = "test-user"; // Thay thế User.Identity.Name tạm thời
            var urls = await _urlShortenerService.GetUserUrlsAsync(userId);
            return Ok(urls);
        }

        [HttpDelete]
        //[Authorize]
        [Route("{shortUrl}")]
        public async Task<ActionResult> DeleteShortUrl(string shortUrl)
        {
            string userId = "test-user"; // Thay thế User.Identity.Name tạm thời
            bool result = await _urlShortenerService.DeleteShortUrlAsync(shortUrl, userId);

            if (!result)
            {
                return NotFound(new { error = "URL không tồn tại hoặc không thuộc về bạn" });
            }

            return Ok(new { message = "URL đã được xóa thành công" });
        }
    }
}