using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Auth
{
    // Protects an endpoint for trusted server-to-server calls only (no user JWT).
    // Checks the X-Service-Key header against INTERNAL_SERVICE_KEY (env var) or
    // InternalApi:ServiceKey (config) — the same value must be configured on both
    // the calling service and this one.
    public class RequireServiceKeyAttribute : Attribute, IAsyncActionFilter
    {
        private const string HeaderName = "X-Service-Key";
        private const string EnvironmentVariableName = "INTERNAL_SERVICE_KEY";
        private const string ConfigurationKey = "InternalApi:ServiceKey";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();

            var expectedKey = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(expectedKey))
            {
                expectedKey = configuration[ConfigurationKey];
            }

            if (string.IsNullOrWhiteSpace(expectedKey))
            {
                context.Result = new ObjectResult("Service key is not configured on this server.")
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
                return;
            }

            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
                !FixedTimeEquals(providedKey.ToString(), expectedKey))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            await next();
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            return aBytes.Length == bBytes.Length && CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}
