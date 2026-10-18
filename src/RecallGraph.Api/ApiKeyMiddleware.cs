using System.Security.Cryptography;
using System.Text;

namespace RecallGraph.Api;

public sealed class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private static readonly string[] PublicPaths =
    [
        "/api/health",
        "/api/extraction/status",
        "/api/capabilities"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var configuredKey = configuration["RecallGraph:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) || PublicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue("X-Api-Key", out var provided) && FixedTimeEquals(provided.ToString(), configuredKey))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"error\":\"Unauthorized. Provide a valid X-Api-Key header.\"}");
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
