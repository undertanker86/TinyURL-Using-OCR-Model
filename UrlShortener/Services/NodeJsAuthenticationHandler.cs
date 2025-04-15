using System;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

namespace UrlShortener.Services
{
    public class NodeJsAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _authServiceUrl;

        public NodeJsAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
            : base(options, logger, encoder, clock)
        {
            _httpClientFactory = httpClientFactory;
            _authServiceUrl = configuration["AuthServiceUrl"] ?? "http://localhost:3000";
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Kiểm tra xem request có authorization header không
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                return AuthenticateResult.Fail("Authorization header không tồn tại");
            }

            var authHeader = Request.Headers["Authorization"].ToString();

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Authorization", authHeader);

                // Gọi API của NodeJS để xác thực token
                var response = await client.GetAsync($"{_authServiceUrl}/api/auth/validate");

                if (!response.IsSuccessStatusCode)
                {
                    return AuthenticateResult.Fail("Token không hợp lệ");
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<AuthResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Data?.User == null)
                {
                    return AuthenticateResult.Fail("Không thể lấy thông tin người dùng");
                }

                var user = result.Data.User;

                // Tạo claims từ thông tin user
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email)
                };

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return AuthenticateResult.Success(ticket);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Lỗi khi xác thực token");
                return AuthenticateResult.Fail("Lỗi xác thực: " + ex.Message);
            }
        }

        private class AuthResponse
        {
            public string Status { get; set; }
            public AuthData Data { get; set; }
        }

        private class AuthData
        {
            public UserData User { get; set; }
        }

        private class UserData
        {
            public string Id { get; set; }
            public string Email { get; set; }
        }
    }
}