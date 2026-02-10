using System;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Integrations.Talabat;

public class TalabatRequestSigner : ITransientDependency
{
    private const string SignatureHeaderName = "X-Signature";
    private const string TimestampHeaderName = "X-Timestamp";

    private readonly IConfiguration _configuration;
    private readonly ILogger<TalabatRequestSigner> _logger;

    public TalabatRequestSigner(IConfiguration configuration, ILogger<TalabatRequestSigner> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SignAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        if (!IsSignatureEnabled())
        {
            return;
        }

        var secret = _configuration["Talabat:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            _logger.LogWarning(
                "Talabat request signing is enabled but Talabat:Secret is missing. Skipping X-Signature header.");
            return;
        }

        var timestamp = DateTimeOffset.UtcNow
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);

        var body = request.Content == null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        var method = request.Method.Method.ToUpperInvariant();
        var pathAndQuery = request.RequestUri?.PathAndQuery ?? "/";
        var payload = $"{method}\n{pathAndQuery}\n{timestamp}\n{body}";
        var signature = ComputeHmacSha256Base64(payload, secret);

        request.Headers.Remove(TimestampHeaderName);
        request.Headers.Remove(SignatureHeaderName);
        request.Headers.TryAddWithoutValidation(TimestampHeaderName, timestamp);
        request.Headers.TryAddWithoutValidation(SignatureHeaderName, signature);
    }

    private bool IsSignatureEnabled()
    {
        return _configuration.GetValue<bool?>("Talabat:EnableRequestSignature") ?? false;
    }

    private static string ComputeHmacSha256Base64(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        return Convert.ToBase64String(hashBytes);
    }
}
