using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;

namespace Khamsin;

/// <summary>
/// Fallback endpoint for Blazor Server apps that would rather serve the generated stylesheet from an
/// endpoint than from static web assets. The response carries a content hash and a one-year immutable
/// cache policy.
/// </summary>
public static class KhamsinCssEndpointExtensions
{
    /// <summary>Maps the generated Khamsin stylesheet.</summary>
    public static IEndpointRouteBuilder MapKhamsinCss(this IEndpointRouteBuilder endpoints, string pattern = "/khamsin.css")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        if (!KhamsinCssProvider.TryLoad(endpoints.ServiceProvider, out var css))
            throw new InvalidOperationException(
                "khamsin.css was not found. Build the app with KhamsinEmitCss enabled (the default for web projects), or pass the CSS to MapKhamsinCss(pattern, css).");

        return endpoints.MapKhamsinCss(pattern, css);
    }

    /// <summary>Maps an explicit stylesheet body, useful for tests or custom pipelines.</summary>
    public static IEndpointRouteBuilder MapKhamsinCss(this IEndpointRouteBuilder endpoints, string pattern, string css)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        if (string.IsNullOrEmpty(pattern))
            throw new ArgumentException("A route pattern is required.", nameof(pattern));

        var payload = Encoding.UTF8.GetBytes(css);
        var etag = "\"" + Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant() + "\"";

        endpoints.MapGet(pattern, async context =>
        {
            var response = context.Response;
            response.ContentType = "text/css; charset=utf-8";
            response.Headers.ETag = etag;
            response.Headers.CacheControl = "public,max-age=31536000,immutable";

            var ifNoneMatch = context.Request.Headers.IfNoneMatch;

            if (ifNoneMatch.Any(t
                    => string.Equals(t, etag, StringComparison.Ordinal) ||
                       string.Equals(t, "*", StringComparison.Ordinal)))
            {
                response.StatusCode = StatusCodes.Status304NotModified;
                return;
            }

            response.ContentLength = payload.Length;
            await response.Body.WriteAsync(payload.AsMemory(), context.RequestAborted).ConfigureAwait(false);
        });

        return endpoints;
    }
}

/// <summary>Locates the generated stylesheet for the endpoint fallback.</summary>
internal static class KhamsinCssProvider
{
    internal static bool TryLoad(IServiceProvider services, out string css)
    {
        css = string.Empty;

        var environment = services.GetService<IWebHostEnvironment>();
        if (environment != null)
        {
            if (TryReadEmbedded(typeof(KhamsinCssProvider).Assembly, out css) ||
                TryReadEmbedded(Assembly.GetEntryAssembly(), out css))
                return true;

            if (TryReadFile(environment.WebRootFileProvider, "khamsin.css", out css))
                return true;

            if (!string.IsNullOrEmpty(environment.WebRootPath) &&
                TryReadPath(Path.Combine(environment.WebRootPath, "khamsin.css"), out css))
                return true;

            if (!string.IsNullOrEmpty(environment.ContentRootPath))
            {
                var candidate = Path.Combine(environment.ContentRootPath, "wwwroot", "khamsin.css");
                if (TryReadPath(candidate, out css))
                    return true;
            }
        }

        if (TryReadEmbedded(typeof(KhamsinCssProvider).Assembly, out css) ||
            TryReadEmbedded(Assembly.GetEntryAssembly(), out css))
            return true;

        return TryReadPath(Path.Combine(AppContext.BaseDirectory, "khamsin.css"), out css);
    }

    private static bool TryReadEmbedded(Assembly? assembly, out string css)
    {
        css = string.Empty;
        if (assembly is null)
            return false;

        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.EndsWith("khamsin.css", StringComparison.OrdinalIgnoreCase))
                continue;

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null)
                continue;

            using var reader = new StreamReader(stream, Encoding.UTF8);
            css = reader.ReadToEnd();
            return true;
        }

        return false;
    }

    private static bool TryReadFile(IFileProvider? provider, string path, out string css)
    {
        css = string.Empty;
        if (provider is null)
            return false;

        var file = provider.GetFileInfo(path);
        if (!file.Exists || file.IsDirectory)
            return false;

        using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        css = reader.ReadToEnd();
        return true;
    }

    private static bool TryReadPath(string path, out string css)
    {
        css = string.Empty;
        if (!File.Exists(path))
            return false;

        css = File.ReadAllText(path, Encoding.UTF8);
        return true;
    }
}
