using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Security;

public class TalabatWebhookSecurityValidator : ITransientDependency
{
    private const string SignatureMode = "signature";
    private const string SecretMode = "secret";
    private const string EitherMode = "either";

    private readonly IConfiguration _configuration;
    private readonly ILogger<TalabatWebhookSecurityValidator> _logger;

    public TalabatWebhookSecurityValidator(
        IConfiguration configuration,
        ILogger<TalabatWebhookSecurityValidator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public TalabatWebhookSecurityValidationResult Validate(HttpRequest request, string rawBody, string correlationId)
    {
        if (!IsEnabled())
        {
            return TalabatWebhookSecurityValidationResult.ValidResult();
        }

        var mode = GetMode();
        var secretKey = ResolveSecretKey();
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            _logger.LogWarning(
                "Talabat webhook security is enabled but no secret key is configured. CorrelationId={CorrelationId}",
                correlationId);
            return TalabatWebhookSecurityValidationResult.InvalidResult("Webhook security secret key is missing.");
        }

        var signatureValidation = ValidateSignature(request, rawBody, secretKey, correlationId);
        var secretValidation = ValidateSecretHeader(request, secretKey, correlationId);

        return mode switch
        {
            SignatureMode => signatureValidation,
            SecretMode => secretValidation,
            EitherMode => signatureValidation.IsValid ? signatureValidation : secretValidation,
            _ => TalabatWebhookSecurityValidationResult.InvalidResult(
                $"Invalid webhook security mode '{mode}'. Supported modes: signature, secret, either.")
        };
    }

    private TalabatWebhookSecurityValidationResult ValidateSignature(
        HttpRequest request,
        string rawBody,
        string secretKey,
        string correlationId)
    {
        var signatureHeaderName = GetSignatureHeaderName();
        var timestampHeaderName = GetTimestampHeaderName();

        var providedSignature = request.Headers[signatureHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(providedSignature))
        {
            return TalabatWebhookSecurityValidationResult.InvalidResult(
                $"Missing signature header '{signatureHeaderName}'.");
        }

        var timestamp = request.Headers[timestampHeaderName].ToString();
        var requireTimestamp = _configuration.GetValue<bool?>("Talabat:WebhookSecurity:RequireTimestamp") ?? false;
        var maxSkewSeconds = _configuration.GetValue<int?>("Talabat:WebhookSecurity:MaxSkewSeconds") ?? 300;

        if (string.IsNullOrWhiteSpace(timestamp))
        {
            if (requireTimestamp)
            {
                return TalabatWebhookSecurityValidationResult.InvalidResult(
                    $"Missing required timestamp header '{timestampHeaderName}'.");
            }

            var bodyOnlyValid = IsSignatureMatch(providedSignature, rawBody, secretKey);
            return bodyOnlyValid
                ? TalabatWebhookSecurityValidationResult.ValidResult()
                : TalabatWebhookSecurityValidationResult.InvalidResult("Invalid webhook signature.");
        }

        if (!long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestampSeconds))
        {
            return TalabatWebhookSecurityValidationResult.InvalidResult(
                $"Invalid timestamp header '{timestampHeaderName}'.");
        }

        if (maxSkewSeconds > 0)
        {
            var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var skew = Math.Abs(nowSeconds - timestampSeconds);
            if (skew > maxSkewSeconds)
            {
                return TalabatWebhookSecurityValidationResult.InvalidResult(
                    $"Webhook timestamp skew exceeded allowed limit ({maxSkewSeconds}s).");
            }
        }

        var timestampPayload = $"{timestamp}.{rawBody}";
        if (IsSignatureMatch(providedSignature, timestampPayload, secretKey))
        {
            return TalabatWebhookSecurityValidationResult.ValidResult();
        }

        // Compatibility fallback for integrations signing body-only while still sending timestamp.
        if (IsSignatureMatch(providedSignature, rawBody, secretKey))
        {
            _logger.LogDebug(
                "Talabat webhook signature matched body-only payload fallback. CorrelationId={CorrelationId}",
                correlationId);
            return TalabatWebhookSecurityValidationResult.ValidResult();
        }

        return TalabatWebhookSecurityValidationResult.InvalidResult("Invalid webhook signature.");
    }

    private TalabatWebhookSecurityValidationResult ValidateSecretHeader(
        HttpRequest request,
        string secretKey,
        string correlationId)
    {
        var secretHeaderName = GetSecretHeaderName();
        var providedSecret = request.Headers[secretHeaderName].ToString();

        if (string.IsNullOrWhiteSpace(providedSecret))
        {
            return TalabatWebhookSecurityValidationResult.InvalidResult(
                $"Missing secret header '{secretHeaderName}'.");
        }

        if (!SecureEquals(providedSecret, secretKey, ignoreCase: false))
        {
            _logger.LogWarning(
                "Talabat webhook secret header mismatch. CorrelationId={CorrelationId}, HeaderName={HeaderName}",
                correlationId,
                secretHeaderName);
            return TalabatWebhookSecurityValidationResult.InvalidResult("Invalid webhook secret.");
        }

        return TalabatWebhookSecurityValidationResult.ValidResult();
    }

    private static bool IsSignatureMatch(string providedSignature, string payload, string secretKey)
    {
        var expectedBase64 = ComputeHmacSha256Base64(payload, secretKey);
        if (SecureEquals(providedSignature, expectedBase64, ignoreCase: false))
        {
            return true;
        }

        var expectedHex = ComputeHmacSha256Hex(payload, secretKey);
        return SecureEquals(providedSignature, expectedHex, ignoreCase: true);
    }

    private static string ComputeHmacSha256Base64(string payload, string secretKey)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var secretBytes = Encoding.UTF8.GetBytes(secretKey);

        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToBase64String(hash);
    }

    private static string ComputeHmacSha256Hex(string payload, string secretKey)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var secretBytes = Encoding.UTF8.GetBytes(secretKey);

        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool SecureEquals(string provided, string expected, bool ignoreCase)
    {
        if (provided == null || expected == null)
        {
            return false;
        }

        var left = ignoreCase ? provided.Trim().ToLowerInvariant() : provided.Trim();
        var right = ignoreCase ? expected.Trim().ToLowerInvariant() : expected.Trim();

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private bool IsEnabled()
    {
        return _configuration.GetValue<bool?>("Talabat:WebhookSecurity:Enabled") ?? false;
    }

    private string GetMode()
    {
        return (_configuration["Talabat:WebhookSecurity:Mode"] ?? SignatureMode)
            .Trim()
            .ToLowerInvariant();
    }

    private string? ResolveSecretKey()
    {
        return _configuration["Talabat:WebhookSecurity:SecretKey"]
               ?? _configuration["Talabat:Secret"];
    }

    private string GetSignatureHeaderName()
    {
        return _configuration["Talabat:WebhookSecurity:SignatureHeader"] ?? "X-Signature";
    }

    private string GetTimestampHeaderName()
    {
        return _configuration["Talabat:WebhookSecurity:TimestampHeader"] ?? "X-Timestamp";
    }

    private string GetSecretHeaderName()
    {
        return _configuration["Talabat:WebhookSecurity:SecretHeader"] ?? "X-Webhook-Secret";
    }
}

public sealed class TalabatWebhookSecurityValidationResult
{
    public bool IsValid { get; private init; }

    public string? Error { get; private init; }

    public static TalabatWebhookSecurityValidationResult ValidResult()
    {
        return new TalabatWebhookSecurityValidationResult
        {
            IsValid = true
        };
    }

    public static TalabatWebhookSecurityValidationResult InvalidResult(string error)
    {
        return new TalabatWebhookSecurityValidationResult
        {
            IsValid = false,
            Error = error
        };
    }
}
