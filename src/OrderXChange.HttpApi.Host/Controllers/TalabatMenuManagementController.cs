using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Talabat;
using OrderXChange.Domain.Staging;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Domain.Repositories;

namespace OrderXChange.HttpApi.Host.Controllers;

/// <summary>
/// Talabat Menu Management Controller
/// Provides endpoints for clearing, hiding, and managing menu items on Talabat
/// </summary>
[ApiController]
[Route("api/talabat/menu")]
public class TalabatMenuManagementController : AbpControllerBase
{
    private readonly TalabatCatalogClient _talabatCatalogClient;
    private readonly IRepository<FoodicsProductStaging, Guid> _stagingRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TalabatMenuManagementController> _logger;

    public TalabatMenuManagementController(
        TalabatCatalogClient talabatCatalogClient,
        IRepository<FoodicsProductStaging, Guid> stagingRepository,
        IConfiguration configuration,
        ILogger<TalabatMenuManagementController> logger)
    {
        _talabatCatalogClient = talabatCatalogClient;
        _stagingRepository = stagingRepository;
        _configuration = configuration;
        _logger = logger;
    }

    #region Clear Menu Operations

    /// <summary>
    /// Clears all menu items for a specific vendor by submitting an empty catalog.
    /// This will completely remove all products, categories, and choices from Talabat.
    /// Use this when you want to start fresh or remove the menu entirely.
    /// </summary>
    /// <param name="vendorCode">Vendor code (e.g., PH-SIDDIQ-002)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with ImportId for tracking</returns>
    /// <response code="200">Menu cleared successfully, returns ImportId</response>
    /// <response code="400">Failed to clear menu</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("clear/{vendorCode}")]
    [ProducesResponseType(typeof(MenuClearResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ClearVendorMenu(
        string vendorCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var chainCode = _configuration["Talabat:ChainCode"] ?? "tlbt-pick";
            var callbackBaseUrl = _configuration["Talabat:CallbackBaseUrl"];
            
            _logger.LogInformation(
                "🗑️ [CLEAR MENU] Starting clear operation. VendorCode={VendorCode}, ChainCode={ChainCode}",
                vendorCode,
                chainCode);

            // Create catalog with ONE inactive dummy product to replace everything
            // Talabat may reject completely empty catalogs, so we send one inactive item
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var dummyProductId = $"clear-dummy-{timestamp}";
            var dummyCategoryId = $"clear-cat-{timestamp}";
            var dummyMenuId = $"clear-menu-{timestamp}";
            var dummyScheduleId = $"clear-schedule-{timestamp}";

            var emptyCatalog = new TalabatV2CatalogSubmitRequest
            {
                CallbackUrl = $"{callbackBaseUrl}/catalog-status",
                Vendors = new List<string> { vendorCode },
                Catalog = new TalabatV2Catalog
                {
                    Items = new Dictionary<string, TalabatV2CatalogItem>
                    {
                        // Schedule (24/7 availability)
                        [dummyScheduleId] = new TalabatV2CatalogItem
                        {
                            Id = dummyScheduleId,
                            Type = "ScheduleEntry",
                            StartTime = "00:00",
                            EndTime = "23:59",
                            WeekDays = new List<string> { "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY" }
                        },
                        // Menu
                        [dummyMenuId] = new TalabatV2CatalogItem
                        {
                            Id = dummyMenuId,
                            Type = "Menu",
                            Title = new TalabatV2Title { Default = "⚠️ Menu Clearing in Progress..." },
                            Schedule = new Dictionary<string, TalabatV2ItemReference>
                            {
                                [dummyScheduleId] = new TalabatV2ItemReference
                                {
                                    Id = dummyScheduleId,
                                    Type = "ScheduleEntry"
                                }
                            }
                        },
                        // Category (will contain the dummy product)
                        [dummyCategoryId] = new TalabatV2CatalogItem
                        {
                            Id = dummyCategoryId,
                            Type = "Category",
                            Title = new TalabatV2Title { Default = "System - Clearing Catalog" },
                            Order = 999,
                            Products = new Dictionary<string, TalabatV2ItemReference>
                            {
                                [dummyProductId] = new TalabatV2ItemReference
                                {
                                    Id = dummyProductId,
                                    Type = "Product",
                                    Order = 0
                                }
                            }
                        },
                        // Dummy Product (INACTIVE - won't show to customers)
                        [dummyProductId] = new TalabatV2CatalogItem
                        {
                            Id = dummyProductId,
                            Type = "Product",
                            Title = new TalabatV2Title 
                            { 
                                Default = "⚠️ Menu is being cleared - Please wait"
                            },
                            Description = new TalabatV2Title 
                            { 
                                Default = "This temporary item will be removed. The menu is being updated."
                            },
                            Price = "0.00",
                            Active = false  // INACTIVE - invisible to customers
                        }
                    }
                }
            };

            _logger.LogDebug(
                "📤 [CLEAR MENU] Submitting catalog with dummy inactive product to replace all items. " +
                "VendorCode={VendorCode}, DummyProductId={DummyProductId}",
                vendorCode,
                dummyProductId);

            // Submit empty catalog
            var result = await _talabatCatalogClient.SubmitV2CatalogAsync(
                chainCode,
                emptyCatalog,
                vendorCode: null,
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogError(
                    "❌ [CLEAR MENU] Failed to clear menu. VendorCode={VendorCode}, Error={Error}",
                    vendorCode,
                    result.Message);
                
                return BadRequest(new MenuClearResult
                {
                    Success = false,
                    Message = $"Failed to clear menu: {result.Message}",
                    VendorCode = vendorCode,
                    ChainCode = chainCode
                });
            }

            _logger.LogInformation(
                "✅ [CLEAR MENU] Menu cleared successfully. VendorCode={VendorCode}, ImportId={ImportId}",
                vendorCode,
                result.ImportId);

            return Ok(new MenuClearResult
            {
                Success = true,
                Message = "Menu cleared successfully. All old items replaced with inactive dummy product.",
                VendorCode = vendorCode,
                ChainCode = chainCode,
                ImportId = result.ImportId,
                SubmittedAt = DateTime.UtcNow,
                ExpectedCompletionMinutes = 5,
                Instructions = new[]
                {
                    "✅ Catalog submitted with inactive dummy product (replaces all existing items)",
                    "⏳ Wait 1-5 minutes for Talabat to process the request",
                    "🔔 Monitor webhook logs for confirmation (catalog-status)",
                    "🔄 Refresh the Talabat menu page - you should see NO items or empty menu",
                    $"🌐 Check: https://www.talabat.com/kuwait/restaurant/783216/pick-siddiq-tgo?aid=75",
                    "💡 The dummy product is INACTIVE so customers won't see anything"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "💥 [CLEAR MENU] Exception occurred while clearing menu. VendorCode={VendorCode}",
                vendorCode);
            
            return StatusCode(500, new MenuClearResult
            {
                Success = false,
                Message = $"Internal error: {ex.Message}",
                VendorCode = vendorCode,
                Error = ex.ToString()
            });
        }
    }

    /// <summary>
    /// Clears menu for the default vendor configured in appsettings.json
    /// </summary>
    [HttpPost("clear")]
    [ProducesResponseType(typeof(MenuClearResult), 200)]
    public async Task<IActionResult> ClearDefaultVendorMenu(CancellationToken cancellationToken = default)
    {
        var defaultVendorCode = _configuration["Talabat:DefaultVendorCode"] ?? "PH-SIDDIQ-002";
        return await ClearVendorMenu(defaultVendorCode, cancellationToken);
    }

    #endregion

    #region Hide/Show Operations

    /// <summary>
    /// Hides all items for a vendor without removing them from the catalog.
    /// Items remain in the menu structure but are marked as unavailable.
    /// This is useful for temporary closures or maintenance.
    /// </summary>
    /// <param name="vendorCode">Vendor code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with count of hidden items</returns>
    [HttpPost("hide-all/{vendorCode}")]
    [ProducesResponseType(typeof(MenuAvailabilityResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> HideAllItems(
        string vendorCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "🙈 [HIDE ALL] Starting hide operation. VendorCode={VendorCode}",
                vendorCode);

            // Get all staging products for this vendor
            var stagingProducts = await _stagingRepository.GetListAsync(
                x => x.TalabatVendorCode == vendorCode,
                cancellationToken: cancellationToken);

            if (!stagingProducts.Any())
            {
                _logger.LogWarning(
                    "⚠️ [HIDE ALL] No products found for vendor. VendorCode={VendorCode}",
                    vendorCode);
                
                return NotFound(new MenuAvailabilityResult
                {
                    Success = false,
                    Message = $"No products found for vendor {vendorCode}. Have you synced the menu first?",
                    VendorCode = vendorCode
                });
            }

            // Build availability request with all items set to unavailable
            var items = new List<TalabatBranchItemAvailability>();
            
            foreach (var product in stagingProducts)
            {
                // Hide main product
                items.Add(new TalabatBranchItemAvailability
                {
                    RemoteCode = product.FoodicsProductId,
                    IsAvailable = false,
                    Type = "product"
                });

                // Hide modifiers/choices if any
                if (!string.IsNullOrWhiteSpace(product.ModifiersJson))
                {
                    try
                    {
                        var modifiers = JsonSerializer.Deserialize<List<FoodicsModifierDto>>(
                            product.ModifiersJson,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (modifiers != null)
                        {
                            foreach (var modifier in modifiers)
                            {
                                if (modifier.Options != null)
                                {
                                    foreach (var option in modifier.Options)
                                    {
                                        items.Add(new TalabatBranchItemAvailability
                                        {
                                            RemoteCode = $"topping-{option.Id}",
                                            IsAvailable = false
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "⚠️ [HIDE ALL] Could not parse modifiers. ProductId={ProductId}",
                            product.FoodicsProductId);
                    }
                }
            }

            var request = new TalabatBranchItemAvailabilityRequest
            {
                Items = items
            };

            _logger.LogDebug(
                "📤 [HIDE ALL] Submitting availability update. VendorCode={VendorCode}, ItemsCount={Count}",
                vendorCode,
                items.Count);

            var result = await _talabatCatalogClient.UpdateBranchItemAvailabilityAsync(
                vendorCode,
                request,
                cancellationToken);

            _logger.LogInformation(
                "✅ [HIDE ALL] Hidden {Count} items for vendor {VendorCode}",
                items.Count,
                vendorCode);

            return Ok(new MenuAvailabilityResult
            {
                Success = result.Success,
                Message = $"Successfully hidden {items.Count} items (products + choices) for vendor {vendorCode}",
                VendorCode = vendorCode,
                ProductsCount = stagingProducts.Count,
                TotalItemsAffected = items.Count,
                UpdatedAt = DateTime.UtcNow,
                Note = "Items are hidden but not deleted. Use 'show-all' to make them available again."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "💥 [HIDE ALL] Error hiding items. VendorCode={VendorCode}",
                vendorCode);
            
            return StatusCode(500, new MenuAvailabilityResult
            {
                Success = false,
                Message = $"Internal error: {ex.Message}",
                VendorCode = vendorCode,
                Error = ex.ToString()
            });
        }
    }

    /// <summary>
    /// Shows (makes available) all items for a vendor.
    /// This reverses the hide-all operation.
    /// </summary>
    /// <param name="vendorCode">Vendor code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with count of shown items</returns>
    [HttpPost("show-all/{vendorCode}")]
    [ProducesResponseType(typeof(MenuAvailabilityResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ShowAllItems(
        string vendorCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "👁️ [SHOW ALL] Starting show operation. VendorCode={VendorCode}",
                vendorCode);

            // Get all staging products for this vendor
            var stagingProducts = await _stagingRepository.GetListAsync(
                x => x.TalabatVendorCode == vendorCode && x.IsActive,
                cancellationToken: cancellationToken);

            if (!stagingProducts.Any())
            {
                return NotFound(new MenuAvailabilityResult
                {
                    Success = false,
                    Message = $"No active products found for vendor {vendorCode}",
                    VendorCode = vendorCode
                });
            }

            // Build availability request with all items set to available
            var items = new List<TalabatBranchItemAvailability>();
            
            foreach (var product in stagingProducts)
            {
                // Show main product
                items.Add(new TalabatBranchItemAvailability
                {
                    RemoteCode = product.FoodicsProductId,
                    IsAvailable = true,
                    Type = "product"
                });

                // Show modifiers/choices if any
                if (!string.IsNullOrWhiteSpace(product.ModifiersJson))
                {
                    try
                    {
                        var modifiers = JsonSerializer.Deserialize<List<FoodicsModifierDto>>(
                            product.ModifiersJson,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (modifiers != null)
                        {
                            foreach (var modifier in modifiers)
                            {
                                if (modifier.Options != null)
                                {
                                    foreach (var option in modifier.Options)
                                    {
                                        items.Add(new TalabatBranchItemAvailability
                                        {
                                            RemoteCode = $"topping-{option.Id}",
                                            IsAvailable = true
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "⚠️ [SHOW ALL] Could not parse modifiers. ProductId={ProductId}",
                            product.FoodicsProductId);
                    }
                }
            }

            var request = new TalabatBranchItemAvailabilityRequest
            {
                Items = items
            };

            var result = await _talabatCatalogClient.UpdateBranchItemAvailabilityAsync(
                vendorCode,
                request,
                cancellationToken);

            _logger.LogInformation(
                "✅ [SHOW ALL] Shown {Count} items for vendor {VendorCode}",
                items.Count,
                vendorCode);

            return Ok(new MenuAvailabilityResult
            {
                Success = result.Success,
                Message = $"Successfully shown {items.Count} items (products + choices) for vendor {vendorCode}",
                VendorCode = vendorCode,
                ProductsCount = stagingProducts.Count,
                TotalItemsAffected = items.Count,
                UpdatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "💥 [SHOW ALL] Error showing items. VendorCode={VendorCode}",
                vendorCode);
            
            return StatusCode(500, new MenuAvailabilityResult
            {
                Success = false,
                Message = $"Internal error: {ex.Message}",
                VendorCode = vendorCode,
                Error = ex.ToString()
            });
        }
    }

    #endregion

    #region Status & Info

    /// <summary>
    /// Gets menu status and statistics for a vendor
    /// </summary>
    [HttpGet("status/{vendorCode}")]
    [ProducesResponseType(typeof(MenuStatusResult), 200)]
    public async Task<IActionResult> GetMenuStatus(
        string vendorCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stagingProducts = await _stagingRepository.GetListAsync(
                x => x.TalabatVendorCode == vendorCode,
                cancellationToken: cancellationToken);

            var activeProducts = stagingProducts.Count(x => x.IsActive);
            var syncedProducts = stagingProducts.Count(x => !string.IsNullOrEmpty(x.TalabatImportId));

            return Ok(new MenuStatusResult
            {
                VendorCode = vendorCode,
                TotalProducts = stagingProducts.Count,
                ActiveProducts = activeProducts,
                InactiveProducts = stagingProducts.Count - activeProducts,
                SyncedToTalabat = syncedProducts,
                LastSyncTime = stagingProducts
                    .Where(x => x.TalabatSyncCompletedAt.HasValue)
                    .Max(x => x.TalabatSyncCompletedAt),
                LastImportId = stagingProducts
                    .Where(x => !string.IsNullOrEmpty(x.TalabatImportId))
                    .OrderByDescending(x => x.TalabatSyncCompletedAt)
                    .FirstOrDefault()?.TalabatImportId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting menu status for {VendorCode}", vendorCode);
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    #endregion

    #region Branch Availability (Busy/Available)

    /// <summary>
    /// Get branch/vendor availability status from Talabat
    /// GET /api/talabat/menu/branch-availability/{vendorCode}
    /// </summary>
    [HttpGet("branch-availability/{vendorCode}")]
    [ProducesResponseType(typeof(BranchAvailabilityResult), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetBranchAvailability(
        string vendorCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "📋 Getting branch availability. VendorCode={VendorCode}",
                vendorCode);

            var result = await _talabatCatalogClient.GetVendorAvailabilityAsync(
                vendorCode,
                cancellationToken);

            if (result == null)
            {
                return NotFound(new BranchAvailabilityResult
                {
                    Success = false,
                    Message = $"Could not get availability for vendor {vendorCode}",
                    VendorCode = vendorCode
                });
            }

            return Ok(new BranchAvailabilityResult
            {
                Success = true,
                VendorCode = result.VendorCode,
                IsAvailable = result.IsAvailable,
                Status = result.IsAvailable ? "Available" : "Busy",
                Reason = result.Reason,
                AvailableAt = result.AvailableAt,
                LastUpdated = result.LastUpdated
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting branch availability. VendorCode={VendorCode}", vendorCode);
            return StatusCode(500, new BranchAvailabilityResult
            {
                Success = false,
                Message = ex.Message,
                VendorCode = vendorCode
            });
        }
    }

    /// <summary>
    /// Set branch status to BUSY (temporarily stop accepting orders)
    /// POST /api/talabat/menu/branch-busy/{vendorCode}
    /// </summary>
    /// <param name="vendorCode">Vendor code</param>
    /// <param name="reason">Reason for being busy (optional)</param>
    /// <param name="availableInMinutes">How many minutes until available again (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("branch-busy/{vendorCode}")]
    [ProducesResponseType(typeof(BranchAvailabilityResult), 200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SetBranchBusy(
        string vendorCode,
        [FromQuery] string? reason = null,
        [FromQuery] int? availableInMinutes = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "🔴 Setting branch to BUSY. VendorCode={VendorCode}, Reason={Reason}, AvailableIn={Minutes}min",
                vendorCode,
                reason ?? "Not specified",
                availableInMinutes ?? 0);

            DateTime? availableAt = availableInMinutes.HasValue 
                ? DateTime.UtcNow.AddMinutes(availableInMinutes.Value) 
                : null;

            var request = new TalabatUpdateVendorAvailabilityRequest
            {
                IsAvailable = false,
                Reason = reason ?? "Temporarily busy",
                AvailableAt = availableAt
            };

            var result = await _talabatCatalogClient.UpdateVendorAvailabilityAsync(
                vendorCode,
                request,
                cancellationToken);

            if (result == null)
            {
                return Ok(new BranchAvailabilityResult
                {
                    Success = false,
                    Message = $"Failed to set vendor {vendorCode} to busy",
                    VendorCode = vendorCode
                });
            }

            _logger.LogInformation(
                "✅ Branch set to BUSY. VendorCode={VendorCode}",
                vendorCode);

            return Ok(new BranchAvailabilityResult
            {
                Success = true,
                Message = $"Branch {vendorCode} is now BUSY",
                VendorCode = vendorCode,
                IsAvailable = false,
                Status = "Busy",
                Reason = reason,
                AvailableAt = availableAt,
                LastUpdated = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting branch to busy. VendorCode={VendorCode}", vendorCode);
            return StatusCode(500, new BranchAvailabilityResult
            {
                Success = false,
                Message = ex.Message,
                VendorCode = vendorCode
            });
        }
    }

    /// <summary>
    /// Set branch status to AVAILABLE (resume accepting orders)
    /// POST /api/talabat/menu/branch-available/{vendorCode}
    /// </summary>
    /// <param name="vendorCode">Vendor code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("branch-available/{vendorCode}")]
    [ProducesResponseType(typeof(BranchAvailabilityResult), 200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SetBranchAvailable(
        string vendorCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "🟢 Setting branch to AVAILABLE. VendorCode={VendorCode}",
                vendorCode);

            var request = new TalabatUpdateVendorAvailabilityRequest
            {
                IsAvailable = true,
                Reason = null,
                AvailableAt = null
            };

            var result = await _talabatCatalogClient.UpdateVendorAvailabilityAsync(
                vendorCode,
                request,
                cancellationToken);

            if (result == null)
            {
                return Ok(new BranchAvailabilityResult
                {
                    Success = false,
                    Message = $"Failed to set vendor {vendorCode} to available",
                    VendorCode = vendorCode
                });
            }

            _logger.LogInformation(
                "✅ Branch set to AVAILABLE. VendorCode={VendorCode}",
                vendorCode);

            return Ok(new BranchAvailabilityResult
            {
                Success = true,
                Message = $"Branch {vendorCode} is now AVAILABLE",
                VendorCode = vendorCode,
                IsAvailable = true,
                Status = "Available",
                LastUpdated = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting branch to available. VendorCode={VendorCode}", vendorCode);
            return StatusCode(500, new BranchAvailabilityResult
            {
                Success = false,
                Message = ex.Message,
                VendorCode = vendorCode
            });
        }
    }

    #endregion
}

#region Response Models

public class MenuClearResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string VendorCode { get; set; } = string.Empty;
    public string? ChainCode { get; set; }
    public string? ImportId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? ExpectedCompletionMinutes { get; set; }
    public string[]? Instructions { get; set; }
    public string? Error { get; set; }
}

public class MenuAvailabilityResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string VendorCode { get; set; } = string.Empty;
    public int ProductsCount { get; set; }
    public int TotalItemsAffected { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Note { get; set; }
    public string? Error { get; set; }
}

public class MenuStatusResult
{
    public string VendorCode { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int InactiveProducts { get; set; }
    public int SyncedToTalabat { get; set; }
    public DateTime? LastSyncTime { get; set; }
    public string? LastImportId { get; set; }
}

public class BranchAvailabilityResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime? AvailableAt { get; set; }
    public DateTime? LastUpdated { get; set; }
}

#endregion

