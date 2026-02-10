using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Talabat;
using OrderXChange.Domain.Staging;
using OrderXChange.Integrations.Talabat;
using OrderXChange.Security;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;

namespace OrderXChange.Controllers;

/// <summary>
/// Webhook controller for receiving notifications from Talabat Integration Middleware
/// Implements Plugin API endpoints as per Talabat documentation
/// Reference: https://integration.talabat.com/en/documentation/
/// 
/// IP Whitelist (Staging): 34.246.34.27, 18.202.142.208, 54.72.10.41
/// IP Whitelist (Middle East + Turkey): 63.32.225.161, 18.202.96.85, 52.208.41.152
/// </summary>
[Route("api/talabat/webhooks")]
[AllowAnonymous] // Webhooks are authenticated via signature/IP whitelist
public class TalabatWebhookController : AbpController
{
    private readonly IConfiguration _configuration;
    private readonly ITalabatSyncStatusService _syncStatusService;
    private readonly TalabatAccountService _talabatAccountService;
    private readonly IRepository<TalabatOrderSyncLog, Guid> _orderSyncLogRepository;
    private readonly IDistributedEventBus _eventBus;
    private readonly ICurrentTenant _currentTenant;
    private readonly TalabatWebhookSecurityValidator _webhookSecurityValidator;
    private readonly ILogger<TalabatWebhookController> _logger;

    public TalabatWebhookController(
        IConfiguration configuration,
        ITalabatSyncStatusService syncStatusService,
        TalabatAccountService talabatAccountService,
        IRepository<TalabatOrderSyncLog, Guid> orderSyncLogRepository,
        IDistributedEventBus eventBus,
        ICurrentTenant currentTenant,
        TalabatWebhookSecurityValidator webhookSecurityValidator,
        ILogger<TalabatWebhookController> logger)
    {
        _configuration = configuration;
        _syncStatusService = syncStatusService;
        _talabatAccountService = talabatAccountService;
        _orderSyncLogRepository = orderSyncLogRepository;
        _eventBus = eventBus;
        _currentTenant = currentTenant;
        _webhookSecurityValidator = webhookSecurityValidator;
        _logger = logger;
    }

    /// <summary>
    /// Receives catalog import status notifications from Talabat
    /// Webhook: Status of Catalog Import
    /// Called after PUT Submit Catalog is processed asynchronously
    /// </summary>
    [HttpPost("catalog-status")]
    public async Task<IActionResult> CatalogImportStatusAsync()
    {
        var correlationId = Guid.NewGuid().ToString();
        
        try
        {
            // Enable buffering so the stream can be read multiple times
            Request.EnableBuffering();
            
            // Read raw body for logging and signature verification
            Request.Body.Position = 0;
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0; // Reset for potential re-reading
            
            var clientIp = GetClientIpAddress();
            
            _logger.LogInformation(
                "Received Talabat catalog status webhook. CorrelationId={CorrelationId}, ClientIP={ClientIP}, BodyLength={BodyLength}",
                correlationId,
                clientIp,
                rawBody.Length);
            
            var webhookSecurityFailure = ValidateWebhookSecurityOrUnauthorized(rawBody, correlationId);
            if (webhookSecurityFailure != null)
            {
                return webhookSecurityFailure;
            }

            // Validate IP whitelist (optional but recommended)
            if (!ValidateIpWhitelist(clientIp))
            {
                _logger.LogWarning(
                    "Talabat webhook rejected - IP not in whitelist. CorrelationId={CorrelationId}, ClientIP={ClientIP}",
                    correlationId,
                    clientIp);
                
                // Return 200 to avoid Talabat retrying to unauthorized IPs
                // In production, you might want to return 403
            }

            // Parse the webhook payload
            var webhook = JsonSerializer.Deserialize<TalabatCatalogStatusWebhook>(rawBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (webhook == null)
            {
                _logger.LogWarning(
                    "Failed to parse Talabat catalog status webhook. CorrelationId={CorrelationId}, Body={Body}",
                    correlationId,
                    rawBody);
                
                return Ok(new { success = true, message = "Webhook received but could not be parsed" });
            }

            // Normalize fields: some payloads send catalogImportId + details with posVendorId only
            if (string.IsNullOrWhiteSpace(webhook.ImportId) && !string.IsNullOrWhiteSpace(webhook.CatalogImportId))
            {
                webhook.ImportId = webhook.CatalogImportId;
            }

            if (string.IsNullOrWhiteSpace(webhook.VendorCode) && webhook.Details != null && webhook.Details.Any())
            {
                webhook.VendorCode = webhook.Details.First().PosVendorId;
            }

            // Log the status
            _logger.LogInformation(
                "Talabat catalog import status received. " +
                "CorrelationId={CorrelationId}, VendorCode={VendorCode}, ImportId={ImportId}, Status={Status}",
                correlationId,
                webhook.VendorCode,
                webhook.ImportId,
                webhook.Status);

            // Process based on status
            switch (webhook.Status?.ToLowerInvariant())
            {
                case "completed":
                case "done":
                    await HandleCatalogImportCompletedAsync(webhook, rawBody, correlationId);
                    break;

                case "failed":
                    await HandleCatalogImportFailedAsync(webhook, rawBody, correlationId);
                    break;

                case "partial":
                    await HandleCatalogImportPartialAsync(webhook, rawBody, correlationId);
                    break;

                default:
                    _logger.LogWarning(
                        "Unknown catalog import status. CorrelationId={CorrelationId}, Status={Status}",
                        correlationId,
                        webhook.Status);
                    break;
            }

            return Ok(new { success = true, correlationId, message = $"Status '{webhook.Status}' processed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing Talabat catalog status webhook. CorrelationId={CorrelationId}",
                correlationId);

            // Return 200 to acknowledge receipt even on error
            // Talabat should not retry on our processing errors
            return Ok(new { success = false, correlationId, error = "Internal processing error" });
        }
    }

    /// <summary>
    /// Receives menu import request from Talabat
    /// Webhook: Menu Import Request
    /// Talabat can request a fresh menu push via this endpoint
    /// </summary>
    [HttpPost("menu-import-request")]
    public async Task<IActionResult> MenuImportRequestAsync()
    {
        var correlationId = Guid.NewGuid().ToString();

        try
        {
            // Enable buffering so the stream can be read multiple times
            Request.EnableBuffering();
            
            Request.Body.Position = 0;
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            var clientIp = GetClientIpAddress();

            _logger.LogInformation(
                "Received Talabat menu import request webhook. CorrelationId={CorrelationId}, ClientIP={ClientIP}",
                correlationId,
                clientIp);
            
            var webhookSecurityFailure = ValidateWebhookSecurityOrUnauthorized(rawBody, correlationId);
            if (webhookSecurityFailure != null)
            {
                return webhookSecurityFailure;
            }

            var webhook = JsonSerializer.Deserialize<TalabatMenuImportRequestWebhook>(rawBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (webhook != null)
            {
                _logger.LogInformation(
                    "Talabat requesting menu import. CorrelationId={CorrelationId}, VendorCode={VendorCode}, Reason={Reason}",
                    correlationId,
                    webhook.VendorCode,
                    webhook.Reason);

                // TODO: Trigger menu sync for this vendor
                // Could publish a Kafka event or call MenuSyncScheduler directly
                // await _menuSyncScheduler.PublishMenuSyncEventForVendorAsync(webhook.VendorCode);
            }

            return Ok(new { success = true, correlationId, message = "Menu import request received" });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing Talabat menu import request webhook. CorrelationId={CorrelationId}",
                correlationId);

            return Ok(new { success = false, correlationId, error = "Internal processing error" });
        }
    }

    /// <summary>
    /// Health check endpoint for Talabat to verify plugin availability
    /// </summary>  
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "OrderXChange-TalabatPlugin"
        });
    }

    /// <summary>
    /// Receives order webhook from Talabat Integration Middleware
    /// </summary>
    [HttpPost("order/{vendorCode?}")]
    public async Task<IActionResult> OrderWebhookAsync(string? vendorCode, CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString();

        try
        {
            Request.EnableBuffering();
            Request.Body.Position = 0;
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            var clientIp = GetClientIpAddress();

            _logger.LogInformation(
                "Received Talabat order webhook. CorrelationId={CorrelationId}, ClientIP={ClientIP}, BodyLength={BodyLength}",
                correlationId,
                clientIp,
                rawBody.Length);
            
            var webhookSecurityFailure = ValidateWebhookSecurityOrUnauthorized(rawBody, correlationId);
            if (webhookSecurityFailure != null)
            {
                return webhookSecurityFailure;
            }

            if (!ValidateIpWhitelist(clientIp))
            {
                _logger.LogWarning(
                    "Talabat order webhook rejected - IP not in whitelist. CorrelationId={CorrelationId}, ClientIP={ClientIP}",
                    correlationId,
                    clientIp);
            }

            var webhook = JsonSerializer.Deserialize<TalabatOrderWebhook>(rawBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (webhook == null)
            {
                _logger.LogWarning(
                    "Failed to parse Talabat order webhook. CorrelationId={CorrelationId}, Body={Body}",
                    correlationId,
                    rawBody);

                return Ok(new { success = true, message = "Webhook received but could not be parsed" });
            }

            var resolvedVendorCode = ResolveVendorCode(vendorCode, webhook);
            if (string.IsNullOrWhiteSpace(resolvedVendorCode))
            {
                _logger.LogWarning(
                    "Unable to resolve vendor code for Talabat order webhook. CorrelationId={CorrelationId}, PlatformRestaurantId={PlatformRestaurantId}",
                    correlationId,
                    webhook.PlatformRestaurant?.Id);
                return Ok(new { success = true, correlationId, message = "Vendor code not resolved" });
            }

            var account = await _talabatAccountService.GetAccountByVendorCodeAsync(resolvedVendorCode, cancellationToken);
            if (account == null && !string.IsNullOrWhiteSpace(webhook.PlatformRestaurant?.Id))
            {
                account = await _talabatAccountService.GetAccountByPlatformRestaurantIdAsync(webhook.PlatformRestaurant.Id, cancellationToken);
            }

            if (account == null)
            {
                _logger.LogWarning(
                    "No TalabatAccount found for vendor. CorrelationId={CorrelationId}, VendorCode={VendorCode}, PlatformRestaurantId={PlatformRestaurantId}",
                    correlationId,
                    resolvedVendorCode,
                    webhook.PlatformRestaurant?.Id);
                return Ok(new { success = true, correlationId, message = "Account not configured" });
            }

            if (!account.FoodicsAccountId.HasValue)
            {
                _logger.LogWarning(
                    "TalabatAccount missing FoodicsAccountId. CorrelationId={CorrelationId}, VendorCode={VendorCode}, TalabatAccountId={TalabatAccountId}",
                    correlationId,
                    account.VendorCode,
                    account.Id);
                return Ok(new { success = true, correlationId, message = "Foodics account not linked" });
            }

            var logPayload = ShouldLogOrderPayload();
            var productsCount = webhook.Products?.Count ?? 0;
            var categoriesCount = webhook.Products == null
                ? 0
                : webhook.Products
                    .Select(x => x.CategoryName)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

            using (_currentTenant.Change(account.TenantId))
            {
                var orderLog = new TalabatOrderSyncLog
                {
                    FoodicsAccountId = account.FoodicsAccountId.Value,
                    VendorCode = account.VendorCode,
                    PlatformRestaurantId = webhook.PlatformRestaurant?.Id,
                    OrderToken = webhook.Token,
                    OrderCode = webhook.Code,
                    ShortCode = webhook.ShortCode,
                    CorrelationId = correlationId,
                    Status = "Enqueued",
                    IsTestOrder = webhook.Test ?? false,
                    ProductsCount = productsCount,
                    CategoriesCount = categoriesCount,
                    OrderCreatedAt = webhook.CreatedAt,
                    ReceivedAt = DateTime.UtcNow,
                    WebhookPayloadJson = logPayload ? rawBody : null,
                    Attempts = 0
                };

                await _orderSyncLogRepository.InsertAsync(orderLog, autoSave: true, cancellationToken: cancellationToken);

                var idempotencyKey = BuildOrderIdempotencyKey(account.VendorCode, webhook);

                var dispatchEvent = new OrderDispatchEto
                {
                    CorrelationId = correlationId,
                    AccountId = account.FoodicsAccountId.Value,
                    FoodicsAccountId = account.FoodicsAccountId.Value,
                    VendorCode = account.VendorCode,
                    TenantId = account.TenantId,
                    OrderLogId = orderLog.Id,
                    IdempotencyKey = idempotencyKey,
                    OccurredAt = DateTime.UtcNow
                };

                await _eventBus.PublishAsync(dispatchEvent);

                _logger.LogInformation(
                    "Talabat order webhook enqueued. CorrelationId={CorrelationId}, VendorCode={VendorCode}, OrderLogId={OrderLogId}, Products={Products}",
                    correlationId,
                    account.VendorCode,
                    orderLog.Id,
                    productsCount);
            }

            return Ok(new { success = true, correlationId, message = "Order webhook processed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing Talabat order webhook. CorrelationId={CorrelationId}",
                correlationId);

            return Ok(new { success = false, correlationId, error = "Internal processing error" });
        }
    }

    #region Private Methods
    
    private IActionResult? ValidateWebhookSecurityOrUnauthorized(string rawBody, string correlationId)
    {
        var validation = _webhookSecurityValidator.Validate(Request, rawBody, correlationId);
        if (validation.IsValid)
        {
            return null;
        }

        _logger.LogWarning(
            "Talabat webhook security validation failed. CorrelationId={CorrelationId}, Error={Error}",
            correlationId,
            validation.Error ?? "<unknown>");

        return Unauthorized(new
        {
            success = false,
            correlationId,
            error = validation.Error ?? "Invalid webhook authentication."
        });
    }

    private async Task HandleCatalogImportCompletedAsync(
        TalabatCatalogStatusWebhook webhook, 
        string rawPayload,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Catalog import completed successfully. " +
            "CorrelationId={CorrelationId}, VendorCode={VendorCode}, ImportId={ImportId}, " +
            "CategoriesCreated={CategoriesCreated}, CategoriesUpdated={CategoriesUpdated}, " +
            "ProductsCreated={ProductsCreated}, ProductsUpdated={ProductsUpdated}",
            correlationId,
            webhook.VendorCode,
            webhook.ImportId,
            webhook.Summary?.CategoriesCreated ?? 0,
            webhook.Summary?.CategoriesUpdated ?? 0,
            webhook.Summary?.ProductsCreated ?? 0,
            webhook.Summary?.ProductsUpdated ?? 0);

        // Update sync log and staging products in database
        await _syncStatusService.HandleImportCompletedAsync(
            webhook,
            rawPayload,
            correlationId,
            cancellationToken);
    }

    private async Task HandleCatalogImportFailedAsync(
        TalabatCatalogStatusWebhook webhook, 
        string rawPayload,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var errorMessages = webhook.Errors?
            .Select(e => $"{e.Type}:{e.RemoteCode} - {e.Message}")
            .ToList() ?? new System.Collections.Generic.List<string>();

        _logger.LogError(
            "Catalog import FAILED. CorrelationId={CorrelationId}, VendorCode={VendorCode}, ImportId={ImportId}, " +
            "ErrorCount={ErrorCount}, Errors={Errors}",
            correlationId,
            webhook.VendorCode,
            webhook.ImportId,
            errorMessages.Count,
            string.Join("; ", errorMessages.Take(10)));

        // Update sync log and staging products in database
        await _syncStatusService.HandleImportFailedAsync(
            webhook,
            rawPayload,
            correlationId,
            cancellationToken);
    }

    private async Task HandleCatalogImportPartialAsync(
        TalabatCatalogStatusWebhook webhook, 
        string rawPayload,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Catalog import completed with PARTIAL success. " +
            "CorrelationId={CorrelationId}, VendorCode={VendorCode}, ImportId={ImportId}, " +
            "ProductsCreated={ProductsCreated}, ProductsUpdated={ProductsUpdated}, ErrorCount={ErrorCount}",
            correlationId,
            webhook.VendorCode,
            webhook.ImportId,
            webhook.Summary?.ProductsCreated ?? 0,
            webhook.Summary?.ProductsUpdated ?? 0,
            webhook.Errors?.Count ?? 0);

        // Log specific errors for investigation
        if (webhook.Errors != null)
        {
            foreach (var error in webhook.Errors.Take(5))
            {
                _logger.LogWarning(
                    "Catalog import error: Type={Type}, RemoteCode={RemoteCode}, Message={Message}",
                    error.Type,
                    error.RemoteCode,
                    error.Message);
            }
        }

        // Update sync log and staging products in database
        await _syncStatusService.HandleImportPartialAsync(
            webhook,
            rawPayload,
            correlationId,
            cancellationToken);
    }

    private string GetClientIpAddress()
    {
        // Check X-Forwarded-For header (for load balancer/proxy scenarios)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP (original client)
            return forwardedFor.Split(',')[0].Trim();
        }

        // Fall back to remote IP
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private bool ValidateIpWhitelist(string clientIp)
    {
        // IP whitelist from Talabat documentation
        // Staging: 34.246.34.27, 18.202.142.208, 54.72.10.41
        // Middle East + Turkey: 63.32.225.161, 18.202.96.85, 52.208.41.152

        var allowedIps = _configuration.GetSection("Talabat:AllowedIps").Get<string[]>();
        
        if (allowedIps == null || allowedIps.Length == 0)
        {
            // If no whitelist configured, use default Talabat IPs
            allowedIps = new[]
            {
                // Staging
                "34.246.34.27", "18.202.142.208", "54.72.10.41",
                // Middle East + Turkey
                "63.32.225.161", "18.202.96.85", "52.208.41.152",
                // Local development
                "127.0.0.1", "::1"
            };
        }

        return allowedIps.Contains(clientIp);
    }

    private bool ShouldLogOrderPayload()
    {
        return _configuration.GetValue<bool?>("Talabat:LogOrderPayload")
               ?? _configuration.GetValue<bool?>("Talabat:LogCatalogPayload")
               ?? true;
    }

    private string? ResolveVendorCode(string? vendorCode, TalabatOrderWebhook webhook)
    {
        if (!string.IsNullOrWhiteSpace(vendorCode))
        {
            return vendorCode;
        }

        var headerVendor = Request.Headers["X-Vendor-Code"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerVendor))
        {
            return headerVendor;
        }

        var platformRestaurantId = webhook.PlatformRestaurant?.Id;
        if (string.IsNullOrWhiteSpace(platformRestaurantId))
        {
            return null;
        }

        var vendorConfig = _configuration.GetSection("Talabat:VendorConfig").GetChildren();
        foreach (var vendorSection in vendorConfig)
        {
            var configuredRestaurantId = vendorSection.GetValue<string>("PlatformRestaurantId");
            if (string.Equals(configuredRestaurantId, platformRestaurantId, StringComparison.OrdinalIgnoreCase))
            {
                return vendorSection.Key;
            }
        }

        return null;
    }

    private static string BuildOrderIdempotencyKey(string vendorCode, TalabatOrderWebhook webhook)
    {
        var keyPart = !string.IsNullOrWhiteSpace(webhook.Token)
            ? webhook.Token
            : !string.IsNullOrWhiteSpace(webhook.Code)
                ? webhook.Code
                : Guid.NewGuid().ToString("N");

        return $"order:{vendorCode}:{keyPart}";
    }

    #endregion
}

