using System.ComponentModel.DataAnnotations;
using System;

namespace UrlShortener.Models
{
    public class UrlInfo
    {
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        [Url]
        public string DefaultUrl { get; set; }

        public string NewUrl { get; set; }

        public bool IsCustomized { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public int ClickCount { get; set; }

        public bool IsActive { get; set; }
    }
    // DTO models
    public class CreateUrlRequest
    {
        [Required]
        [Url(ErrorMessage = "Vui lòng nhập URL hợp lệ")]
        public string Url { get; set; }

        [StringLength(50, ErrorMessage = "Alias không được vượt quá 50 ký tự")]
        public string CustomAlias { get; set; }

        public DateTime? ExpiryDate { get; set; }

        // Cho phép các trường này null
        public string UserId { get; set; }

        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
        public string Email { get; set; }
    }

    public class UrlResponse
    {
        public string OriginalUrl { get; set; }
        public string ShortUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int ClickCount { get; set; }
    }
    public class QrCodeDto
    {
        public string Base64Qr { get; set; }
    }

}
