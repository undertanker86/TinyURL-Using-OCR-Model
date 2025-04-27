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
            _authServiceUrl = configuration["AuthServiceUrl"] ?? "http://localhost:5006";
            Logger.LogInformation($"NodeJsAuthenticationHandler initialized with AuthServiceUrl: {_authServiceUrl}");
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            try
            {
                // Check if Authorization header exists
                if (!Request.Headers.ContainsKey("Authorization"))
                {
                    Logger.LogWarning("Authorization header missing");
                    return AuthenticateResult.Fail("Authorization header missing");
                }

                var authHeader = Request.Headers["Authorization"].ToString();
                Logger.LogInformation($"Authorization header received: {authHeader.Substring(0, Math.Min(20, authHeader.Length))}...");

                // Check if it's a Bearer token
                if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning("Invalid Authorization header format");
                    return AuthenticateResult.Fail("Invalid Authorization header format");
                }

                try
                {
                    // Create HTTP client and add Authorization header
                    var client = _httpClientFactory.CreateClient();
                    client.DefaultRequestHeaders.Add("Authorization", authHeader);

                    // Call validation endpoint through the API Gateway
                    var validateUrl = $"{_authServiceUrl}/api/auth/validate";
                    Logger.LogInformation($"Calling validation endpoint: {validateUrl}");

                    var response = await client.GetAsync(validateUrl);
                    var responseContent = await response.Content.ReadAsStringAsync();

                    Logger.LogInformation($"Validation response: Status={response.StatusCode}, Content={responseContent}");

                    // Check if validation was successful
                    if (!response.IsSuccessStatusCode)
                    {
                        Logger.LogWarning($"Token validation failed: {response.StatusCode}");
                        return AuthenticateResult.Fail($"Token validation failed: {response.StatusCode}");
                    }

                    // Parse the response
                    var result = JsonSerializer.Deserialize<AuthResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result?.Status != "success" || result?.Data?.User == null)
                    {
                        Logger.LogWarning("Invalid response format from validation endpoint");
                        return AuthenticateResult.Fail("Invalid response format from validation endpoint");
                    }

                    var user = result.Data.User;
                    Logger.LogInformation($"User authenticated: ID={user.Id}, Email={user.Email}");

                    // Create claims for the authenticated user
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id),
                        new Claim(ClaimTypes.Name, user.Id),
                        new Claim(ClaimTypes.Email, user.Email)
                    };

                    var identity = new ClaimsIdentity(claims, Scheme.Name);
                    var principal = new ClaimsPrincipal(identity);
                    var ticket = new AuthenticationTicket(principal, Scheme.Name);

                    return AuthenticateResult.Success(ticket);
                }
                catch (HttpRequestException httpEx)
                {
                    Logger.LogError(httpEx, $"HTTP error during token validation: {httpEx.Message}");
                    return AuthenticateResult.Fail($"Token validation service error: {httpEx.Message}");
                }
                catch (JsonException jsonEx)
                {
                    Logger.LogError(jsonEx, "Error parsing validation response");
                    return AuthenticateResult.Fail("Error parsing validation response");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Authentication error: {ex.Message}");
                return AuthenticateResult.Fail($"Authentication error: {ex.Message}");
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