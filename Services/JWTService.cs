using System.Security.Claims;
using System.Text;
using BeTendyBE.Domain;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace BeTendlyBE.Auth
{

    public sealed class JwtOptions
    {
        public string Issuer { get; init; } = string.Empty;
        public string Audience { get; init; } = string.Empty;
        public string Key { get; init; } = string.Empty;
        public int ExpiresMinutes { get; init; } = 60;
        public int RefreshExpiresDays { get; init; } = 14;
        public string RefreshPepper { get; init; } = string.Empty;
    }

    public sealed class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
    }

    public interface IJwtProvider
    {
        AuthResponse Generate(User user);
    }

    public sealed class JwtProvider : IJwtProvider
    {
        private readonly JwtOptions _opts;

        public JwtProvider(IOptions<JwtOptions> options)
        {
            _opts = options.Value ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(_opts.Key))
                throw new InvalidOperationException("JWT Key is not configured.");
        }

        public AuthResponse Generate(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new(ClaimTypes.GivenName, user.FirstName ?? string.Empty),
                new(ClaimTypes.Surname, user.LastName ?? string.Empty),
            };

            var expires = DateTime.UtcNow.AddMinutes(_opts.ExpiresMinutes);

            var token = new JwtSecurityToken(
                issuer: _opts.Issuer,
                audience: _opts.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expires,
                signingCredentials: creds
            );

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return new AuthResponse
            {
                AccessToken = jwt,
                ExpiresAtUtc = expires
            };
        }
    }
}
