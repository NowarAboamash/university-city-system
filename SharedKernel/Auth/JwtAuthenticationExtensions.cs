using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace SharedKernel.Auth
{
    // Validates JWTs locally against the AuthService's RS256 public key instead of
    // calling AuthService on every request. See JWT_ACCESS_PUBLIC_KEY / Jwt:AccessPublicKey.
    public static class JwtAuthenticationExtensions
    {
        private const string EnvironmentVariableName = "JWT_ACCESS_PUBLIC_KEY";
        private const string ConfigurationKey = "Jwt:AccessPublicKey";

        public static IServiceCollection AddSharedJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var publicKeyPem = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(publicKeyPem))
            {
                publicKeyPem = configuration[ConfigurationKey];
            }

            if (string.IsNullOrWhiteSpace(publicKeyPem))
            {
                throw new InvalidOperationException(
                    $"JWT public key is not configured. Set the '{EnvironmentVariableName}' environment variable or '{ConfigurationKey}' in configuration.");
            }

            var rsa = RSA.Create();
            rsa.ImportFromPem(NormalizePem(publicKeyPem));

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // Keep raw JWT claim names ("sub", "role", "email") instead of the
                    // long http://schemas... URIs ASP.NET Core maps them to by default.
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new RsaSecurityKey(rsa),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        RoleClaimType = "role",
                        NameClaimType = "sub"
                    };
                });

            services.AddAuthorization();

            return services;
        }

        private static string NormalizePem(string key)
        {
            var trimmed = key.Trim();
            if (trimmed.Contains("BEGIN PUBLIC KEY"))
            {
                return trimmed;
            }

            var base64 = trimmed.Replace("\r", "").Replace("\n", "");
            var lines = new List<string>();
            for (var i = 0; i < base64.Length; i += 64)
            {
                lines.Add(base64.Substring(i, Math.Min(64, base64.Length - i)));
            }

            return $"-----BEGIN PUBLIC KEY-----\n{string.Join("\n", lines)}\n-----END PUBLIC KEY-----";
        }
    }
}
