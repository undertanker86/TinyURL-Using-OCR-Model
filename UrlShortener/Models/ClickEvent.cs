// Models/ClickEvent.cs
using System;

namespace UrlShortener.Models
{
    public class ClickEvent
    {
        public string ShortUrl { get; set; }
        public DateTime ClickedAt { get; set; }
        public string UserAgent { get; set; }
        public string IpAddress { get; set; }
        public string ReferrerUrl { get; set; }
    }
}