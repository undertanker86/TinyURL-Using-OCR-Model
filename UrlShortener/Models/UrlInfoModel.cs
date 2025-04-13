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
        [Url]
        public string Url { get; set; }

        public string CustomAlias { get; set; }

        public DateTime? ExpiryDate { get; set; }
    }

    public class UrlResponse
    {
        public string OriginalUrl { get; set; }
        public string ShortUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int ClickCount { get; set; }
    }
}
