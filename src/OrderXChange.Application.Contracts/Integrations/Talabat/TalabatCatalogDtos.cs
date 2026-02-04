using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OrderXChange.Application.Contracts.Integrations.Talabat;

#region Submit Catalog Request DTOs

/// <summary>
/// Root request for PUT /catalogs/stores/{vendor_code}/menu
/// As per Talabat Catalog Management API
/// Reference: https://integration.talabat.com/en/documentation/
/// </summary>
public class TalabatCatalogSubmitRequest
{
    /// <summary>
    /// Optional callback URL to receive catalog import status notification
    /// </summary>
    [JsonPropertyName("callbackUrl")]
    public string? CallbackUrl { get; set; }

    /// <summary>
    /// The menu/catalog to submit
    /// </summary>
    [JsonPropertyName("menu")]
    public TalabatMenu Menu { get; set; } = new();
}

/// <summary>
/// Menu structure containing categories, products, and vendor assignments
/// V2 API: https://integration-middleware.stg.restaurant-partners.com/apidocs/pos-middleware-api
/// </summary>
public class TalabatMenu
{
    /// <summary>
    /// List of menu categories
    /// </summary>
    [JsonPropertyName("categories")]
    public List<TalabatCategory> Categories { get; set; } = new();

    /// <summary>
    /// List of vendor codes (Platform Vendor IDs) that this menu applies to
    /// V2 API requires this field to specify which stores get this menu
    /// </summary>
    [JsonPropertyName("vendors")]
    public List<string>? Vendors { get; set; }
}

/// <summary>
/// Category containing products
/// </summary>
public class TalabatCategory
{
    /// <summary>
    /// Unique identifier of the category on your system (maps to Foodics Category ID)
    /// </summary>
    [JsonPropertyName("remoteCode")]
    public string RemoteCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the category
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Localized name (Arabic, etc.)
    /// </summary>
    [JsonPropertyName("nameTranslations")]
    public Dictionary<string, string>? NameTranslations { get; set; }

    /// <summary>
    /// Description of the category
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Sort order for display
    /// </summary>
    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether the category is available
    /// </summary>
    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Products in this category
    /// </summary>
    [JsonPropertyName("products")]
    public List<TalabatProduct> Products { get; set; } = new();
}

/// <summary>
/// Product/Menu item
/// </summary>
public class TalabatProduct
{
    /// <summary>
    /// Unique identifier of the product on your system (maps to Foodics Product ID)
    /// </summary>
    [JsonPropertyName("remoteCode")]
    public string RemoteCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the product
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Localized name translations
    /// </summary>
    [JsonPropertyName("nameTranslations")]
    public Dictionary<string, string>? NameTranslations { get; set; }

    /// <summary>
    /// Product description
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Localized description translations
    /// </summary>
    [JsonPropertyName("descriptionTranslations")]
    public Dictionary<string, string>? DescriptionTranslations { get; set; }

    /// <summary>
    /// Base price of the product (without modifiers)
    /// </summary>
    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    /// <summary>
    /// URL to product image
    /// Requirements: HTTPS, minimum 500x500 pixels, JPEG/PNG format
    /// </summary>
    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Whether the product is currently available
    /// </summary>
    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Sort order for display within category
    /// </summary>
    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    /// <summary>
    /// Tax rate percentage (e.g., 5 for 5% VAT)
    /// </summary>
    [JsonPropertyName("taxRate")]
    public decimal? TaxRate { get; set; }

    /// <summary>
    /// Product tags for filtering (e.g., "vegetarian", "spicy")
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    /// SKU or barcode
    /// </summary>
    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    /// <summary>
    /// Modifier groups (options like size, toppings, etc.)
    /// </summary>
    [JsonPropertyName("modifierGroups")]
    public List<TalabatModifierGroup>? ModifierGroups { get; set; }
}

/// <summary>
/// Modifier group (e.g., "Choose your size", "Add toppings")
/// </summary>
public class TalabatModifierGroup
{
    /// <summary>
    /// Unique identifier of the modifier group
    /// </summary>
    [JsonPropertyName("remoteCode")]
    public string RemoteCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the modifier group
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Localized name translations
    /// </summary>
    [JsonPropertyName("nameTranslations")]
    public Dictionary<string, string>? NameTranslations { get; set; }

    /// <summary>
    /// Minimum number of modifiers that must be selected
    /// </summary>
    [JsonPropertyName("minSelection")]
    public int MinSelection { get; set; }

    /// <summary>
    /// Maximum number of modifiers that can be selected
    /// </summary>
    [JsonPropertyName("maxSelection")]
    public int MaxSelection { get; set; }

    /// <summary>
    /// Sort order for display
    /// </summary>
    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }

    /// <summary>
    /// Available modifiers/options in this group
    /// </summary>
    [JsonPropertyName("modifiers")]
    public List<TalabatModifier> Modifiers { get; set; } = new();
}

/// <summary>
/// Individual modifier option
/// </summary>
public class TalabatModifier
{
    /// <summary>
    /// Unique identifier of the modifier
    /// </summary>
    [JsonPropertyName("remoteCode")]
    public string RemoteCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the modifier
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Localized name translations
    /// </summary>
    [JsonPropertyName("nameTranslations")]
    public Dictionary<string, string>? NameTranslations { get; set; }

    /// <summary>
    /// Additional price for this modifier (can be 0)
    /// </summary>
    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    /// <summary>
    /// Whether this modifier is available
    /// </summary>
    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Whether this is the default selection
    /// </summary>
    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    /// <summary>
    /// Sort order for display
    /// </summary>
    [JsonPropertyName("sortOrder")]
    public int SortOrder { get; set; }
}

#endregion

#region V2 Catalog Format (Items-based structure)

/// <summary>
/// V2 Catalog Submit Request - Items-based structure
/// Format: catalog.items.{itemId} = { id, type, ... }
/// Reference: https://talabat.stoplight.io/docs/POSMW/ce2a790feb2c8-introduction
/// </summary>
public class TalabatV2CatalogSubmitRequest
{
    [JsonPropertyName("catalog")]
    public TalabatV2Catalog Catalog { get; set; } = new();

    [JsonPropertyName("vendors")]
    public List<string> Vendors { get; set; } = new();

    [JsonPropertyName("callbackUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CallbackUrl { get; set; }
}

/// <summary>
/// V2 Catalog structure with items dictionary
/// </summary>
public class TalabatV2Catalog
{
    [JsonPropertyName("items")]
    public Dictionary<string, TalabatV2CatalogItem> Items { get; set; } = new();
}

/// <summary>
/// Base catalog item (Product, Category, Topping, Image, ScheduleEntry, Menu)
/// All nullable fields use JsonIgnore to prevent sending null values in JSON
/// Reference: https://talabat.stoplight.io/docs/POSMW/ce2a790feb2c8-introduction
/// </summary>
public class TalabatV2CatalogItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // "Product", "Category", "Topping", "Image", "ScheduleEntry", "Menu"

    // Common fields for all types
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TalabatV2Title? Title { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TalabatV2Title? Description { get; set; }

    // Order field for sorting items within categories/toppings
    [JsonPropertyName("order")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Order { get; set; }

    // Product-specific fields
    [JsonPropertyName("price")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Price { get; set; }

    [JsonPropertyName("active")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Active { get; set; }

    [JsonPropertyName("isPrepackedItem")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsPrepackedItem { get; set; }

    [JsonPropertyName("isExpressItem")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsExpressItem { get; set; }

    [JsonPropertyName("excludeDishInformation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ExcludeDishInformation { get; set; }

    // Category-specific fields
    [JsonPropertyName("products")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, TalabatV2ItemReference>? Products { get; set; }

    // Product variants
    [JsonPropertyName("variants")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, TalabatV2ItemReference>? Variants { get; set; }

    // Product images
    [JsonPropertyName("images")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, TalabatV2ItemReference>? Images { get; set; }

    // Product toppings (modifier groups)
    [JsonPropertyName("toppings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, TalabatV2ItemReference>? Toppings { get; set; }

    // Topping-specific fields
    [JsonPropertyName("quantity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TalabatV2Quantity? Quantity { get; set; }

    // Image-specific fields
    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; set; }

    // Image alt text
    [JsonPropertyName("alt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TalabatV2Title? Alt { get; set; }

    // Product parent reference (for variants)
    [JsonPropertyName("parent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TalabatV2ItemReference? Parent { get; set; }

    // ScheduleEntry-specific fields
    [JsonPropertyName("startTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EndTime { get; set; }

    // ScheduleEntry weekDays - Required by Talabat
    [JsonPropertyName("weekDays")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? WeekDays { get; set; }

    // Menu-specific fields
    [JsonPropertyName("menuType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MenuType { get; set; } // "DELIVERY", "PICKUP", etc.

    [JsonPropertyName("schedule")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, TalabatV2ItemReference>? Schedule { get; set; }
}

/// <summary>
/// Title with default and localized translations
/// </summary>
public class TalabatV2Title
{
    [JsonPropertyName("default")]
    public string Default { get; set; } = string.Empty;

    // Additional localized fields can be added here if needed
    // e.g., "ar", "en", etc.
}

/// <summary>
/// Reference to another catalog item
/// Used when linking products to categories, toppings, etc.
/// </summary>
public class TalabatV2ItemReference
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Price { get; set; }

    [JsonPropertyName("order")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Order { get; set; }
}

/// <summary>
/// Quantity constraints for toppings
/// </summary>
public class TalabatV2Quantity
{
    [JsonPropertyName("minimum")]
    public int Minimum { get; set; }

    [JsonPropertyName("maximum")]
    public int Maximum { get; set; }
}

#endregion

#region API Response DTOs

/// <summary>
/// Response from PUT Submit Catalog
/// </summary>
public class TalabatCatalogSubmitResponse
{
    /// <summary>
    /// Whether the request was accepted for processing
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Message from the API
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Import job ID for tracking
    /// Supports both "importId" and "catalogImportId" from V2 API
    /// </summary>
    [JsonPropertyName("importId")]
    public string? ImportId { get; set; }

    /// <summary>
    /// V2 API returns catalogImportId instead of importId
    /// This property maps to the same field for compatibility
    /// </summary>
    [JsonPropertyName("catalogImportId")]
    public string? CatalogImportId 
    { 
        get => ImportId;
        set => ImportId = value;
    }

    /// <summary>
    /// Validation errors if any
    /// </summary>
    [JsonPropertyName("errors")]
    public List<TalabatValidationError>? Errors { get; set; }
}

/// <summary>
/// V2 submit response shape observed from Talabat (status + catalogImportId)
/// </summary>
public class TalabatV2CatalogSubmitResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("catalogImportId")]
    public string? CatalogImportId { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Validation error detail
/// </summary>
public class TalabatValidationError
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

/// <summary>
/// Response from GET Catalog Import Log
/// </summary>
public class TalabatCatalogImportLogResponse
{
    [JsonPropertyName("importId")]
    public string? ImportId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; } // "pending", "processing", "completed", "failed"

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("errors")]
    public List<TalabatImportError>? Errors { get; set; }

    [JsonPropertyName("summary")]
    public TalabatImportSummary? Summary { get; set; }
}

/// <summary>
/// Import error detail
/// </summary>
public class TalabatImportError
{
    [JsonPropertyName("remoteCode")]
    public string? RemoteCode { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; } // "category", "product", "modifier"

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Import summary statistics
/// </summary>
public class TalabatImportSummary
{
    [JsonPropertyName("categoriesCreated")]
    public int CategoriesCreated { get; set; }

    [JsonPropertyName("categoriesUpdated")]
    public int CategoriesUpdated { get; set; }

    [JsonPropertyName("categoriesDeleted")]
    public int CategoriesDeleted { get; set; }

    [JsonPropertyName("productsCreated")]
    public int ProductsCreated { get; set; }

    [JsonPropertyName("productsUpdated")]
    public int ProductsUpdated { get; set; }

    [JsonPropertyName("productsDeleted")]
    public int ProductsDeleted { get; set; }
}

#endregion

#region Authentication DTOs

/// <summary>
/// Login request for Talabat API
/// </summary>
public class TalabatLoginRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Login response with access token
/// </summary>
public class TalabatLoginResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }
}

#endregion

#region Webhook DTOs

/// <summary>
/// Base webhook payload from Talabat
/// All webhooks extend this base structure
/// </summary>
public class TalabatWebhookPayload
{
    /// <summary>
    /// Event type identifier
    /// Examples: "catalog.import.completed", "catalog.import.failed", "menu.import.requested"
    /// </summary>
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Vendor/store code that this event relates to
    /// </summary>
    [JsonPropertyName("vendorCode")]
    public string? VendorCode { get; set; }

    /// <summary>
    /// Chain code (for multi-brand setups)
    /// </summary>
    [JsonPropertyName("chainCode")]
    public string? ChainCode { get; set; }

    /// <summary>
    /// Catalog import ID for tracking
    /// </summary>
    [JsonPropertyName("catalogImportId")]
    public string? CatalogImportId { get; set; }

    /// <summary>
    /// Legacy import ID field
    /// </summary>
    [JsonPropertyName("importId")]
    public string? ImportId { get; set; }

    /// <summary>
    /// Status of the operation
    /// Values: "completed", "failed", "partial", "processing"
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Timestamp when the event occurred
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }

    /// <summary>
    /// Human-readable message
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Reason for the event (e.g., reason for failure or menu request)
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Import summary statistics
    /// </summary>
    [JsonPropertyName("summary")]
    public TalabatImportSummary? Summary { get; set; }

    /// <summary>
    /// List of errors if any
    /// </summary>
    [JsonPropertyName("errors")]
    public List<TalabatImportError>? Errors { get; set; }

    /// <summary>
    /// Additional data/metadata
    /// </summary>
    [JsonPropertyName("data")]
    public Dictionary<string, object>? Data { get; set; }

    /// <summary>
    /// V2 API: Detailed status per vendor (when submitting to multiple vendors)
    /// </summary>
    [JsonPropertyName("details")]
    public List<TalabatCatalogStatusDetail>? Details { get; set; }
}

/// <summary>
/// V2 Catalog import detail for individual vendor
/// </summary>
public class TalabatCatalogStatusDetail
{
    [JsonPropertyName("posVendorId")]
    public string? PosVendorId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("globalEntityId")]
    public string? GlobalEntityId { get; set; }

    [JsonPropertyName("platformVendorId")]
    public string? PlatformVendorId { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("errors")]
    public List<TalabatImportError>? Errors { get; set; }
}

/// <summary>
/// Webhook payload for catalog import status notification
/// </summary>
public class TalabatCatalogStatusWebhook : TalabatWebhookPayload
{
}

/// <summary>
/// Webhook payload for menu import request (Talabat requests a menu push)
/// </summary>
public class TalabatMenuImportRequestWebhook : TalabatWebhookPayload
{
}

/// <summary>
/// Webhook acknowledgment response
/// </summary>
public class TalabatWebhookAcknowledgment
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("receivedAt")]
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Known Talabat webhook event types
/// </summary>
public static class TalabatWebhookEventTypes
{
    public const string CatalogImportCompleted = "catalog.import.completed";
    public const string CatalogImportFailed = "catalog.import.failed";
    public const string CatalogImportPartial = "catalog.import.partial";
    public const string MenuImportRequested = "menu.import.requested";
    public const string ItemAvailabilityUpdated = "item.availability.updated";
    public const string VendorAvailabilityChanged = "vendor.availability.changed";
}

#endregion

#region Item Availability DTOs

/// <summary>
/// Request for POST Update Item Availability
/// </summary>
public class TalabatUpdateItemAvailabilityRequest
{
    [JsonPropertyName("items")]
    public List<TalabatItemAvailability> Items { get; set; } = new();
}

/// <summary>
/// Item availability update
/// </summary>
public class TalabatItemAvailability
{
    /// <summary>
    /// Remote code of the product
    /// </summary>
    [JsonPropertyName("remoteCode")]
    public string RemoteCode { get; set; } = string.Empty;

    /// <summary>
    /// Whether the item is available
    /// </summary>
    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Optional: When the item will become available again
    /// </summary>
    [JsonPropertyName("availableAt")]
    public DateTime? AvailableAt { get; set; }
}

/// <summary>
/// Response from Update Item Availability
/// </summary>
public class TalabatUpdateItemAvailabilityResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("updatedCount")]
    public int UpdatedCount { get; set; }

    [JsonPropertyName("errors")]
    public List<TalabatValidationError>? Errors { get; set; }
}

#endregion

#region Branch-Specific Availability DTOs

/// <summary>
/// Request for updating item availability for a specific branch/vendor
/// POST /catalogs/stores/{vendorCode}/items/availability
/// </summary>
public class TalabatBranchItemAvailabilityRequest
{
    /// <summary>
    /// List of items to update availability for
    /// </summary>
    [JsonPropertyName("items")]
    public List<TalabatBranchItemAvailability> Items { get; set; } = new();
}

/// <summary>
/// Item availability with branch-specific pricing option
/// </summary>
public class TalabatBranchItemAvailability
{
    /// <summary>
    /// Remote code of the product (Foodics product ID)
    /// </summary>
    [JsonPropertyName("remoteCode")]
    public string RemoteCode { get; set; } = string.Empty;

    /// <summary>
    /// Item type: "product", "modifier", "category"
    /// </summary>
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    /// <summary>
    /// Whether the item is available at this branch
    /// </summary>
    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Optional branch-specific price override
    /// </summary>
    [JsonPropertyName("price")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? Price { get; set; }

    /// <summary>
    /// Optional: When the item will become available again
    /// </summary>
    [JsonPropertyName("availableAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? AvailableAt { get; set; }

    /// <summary>
    /// Optional: Reason for unavailability
    /// </summary>
    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }
}

/// <summary>
/// Response from branch-specific item availability update
/// </summary>
public class TalabatBranchItemAvailabilityResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("vendorCode")]
    public string? VendorCode { get; set; }

    [JsonPropertyName("updatedCount")]
    public int UpdatedCount { get; set; }

    [JsonPropertyName("failedCount")]
    public int FailedCount { get; set; }

    [JsonPropertyName("errors")]
    public List<TalabatItemAvailabilityError>? Errors { get; set; }
}

/// <summary>
/// Error detail for item availability update
/// </summary>
public class TalabatItemAvailabilityError
{
    [JsonPropertyName("remoteCode")]
    public string? RemoteCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

/// <summary>
/// Bulk branch availability update request
/// Updates availability across multiple branches at once
/// </summary>
public class TalabatMultiBranchAvailabilityRequest
{
    /// <summary>
    /// List of branch-specific updates
    /// </summary>
    [JsonPropertyName("branches")]
    public List<TalabatBranchAvailabilityUpdate> Branches { get; set; } = new();
}

/// <summary>
/// Single branch availability update
/// </summary>
public class TalabatBranchAvailabilityUpdate
{
    /// <summary>
    /// Talabat vendor code for this branch
    /// </summary>
    [JsonPropertyName("vendorCode")]
    public string VendorCode { get; set; } = string.Empty;

    /// <summary>
    /// Items to update for this branch
    /// </summary>
    [JsonPropertyName("items")]
    public List<TalabatBranchItemAvailability> Items { get; set; } = new();
}

/// <summary>
/// Response from multi-branch availability update
/// </summary>
public class TalabatMultiBranchAvailabilityResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("results")]
    public List<TalabatBranchUpdateResult>? Results { get; set; }
}

/// <summary>
/// Result for a single branch update
/// </summary>
public class TalabatBranchUpdateResult
{
    [JsonPropertyName("vendorCode")]
    public string VendorCode { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("updatedCount")]
    public int UpdatedCount { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Response from GET branch item availability
/// </summary>
public class TalabatBranchItemAvailabilityGetResponse
{
    [JsonPropertyName("vendorCode")]
    public string? VendorCode { get; set; }

    [JsonPropertyName("items")]
    public List<TalabatBranchItemAvailability>? Items { get; set; }
}

#endregion

#region Vendor Availability DTOs

/// <summary>
/// Request for POST Update Vendor Availability (V1 - Legacy)
/// </summary>
public class TalabatUpdateVendorAvailabilityRequest
{
    /// <summary>
    /// Whether the vendor/store is available to receive orders
    /// </summary>
    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Reason for unavailability (required when isAvailable = false)
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// When the vendor will become available again
    /// </summary>
    [JsonPropertyName("availableAt")]
    public DateTime? AvailableAt { get; set; }
}

/// <summary>
/// Request for POST Update Vendor Availability (V2)
/// POST /v2/chains/{chainCode}/remoteVendors/{chainVendorId}/availability
/// Reference: https://talabat.stoplight.io/docs/POSMW/c2ab8856764e5-update-availability-status
/// </summary>
public class TalabatUpdateVendorAvailabilityV2Request
{
    /// <summary>
    /// Availability state of the restaurant
    /// Allowed values: OPEN, CLOSED, CLOSED_UNTIL, CLOSED_TODAY, INACTIVE, UNKNOWN
    /// </summary>
    [JsonPropertyName("availabilityState")]
    public string AvailabilityState { get; set; } = string.Empty;

    /// <summary>
    /// Platform key - e.g., "TB" for Talabat Brand, "TB_KW" for Kuwait, etc.
    /// </summary>
    [JsonPropertyName("platformKey")]
    public string PlatformKey { get; set; } = string.Empty;

    /// <summary>
    /// Platform's internal restaurant ID (from Talabat)
    /// </summary>
    [JsonPropertyName("platformRestaurantId")]
    public string PlatformRestaurantId { get; set; } = string.Empty;

    /// <summary>
    /// Number of minutes until the restaurant will reopen (required when availabilityState is CLOSED_UNTIL)
    /// </summary>
    [JsonPropertyName("closingMinutes")]
    public int? ClosingMinutes { get; set; }

    /// <summary>
    /// Reason for closing (Talabat expects specific values e.g. TOO_BUSY_NO_DRIVERS)
    /// </summary>
    [JsonPropertyName("closedReason")]
    public string? ClosedReason { get; set; }
}

/// <summary>
/// Response from Vendor Availability APIs
/// </summary>
public class TalabatVendorAvailabilityResponse
{
    [JsonPropertyName("vendorCode")]
    public string VendorCode { get; set; } = string.Empty;

    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("availableAt")]
    public DateTime? AvailableAt { get; set; }

    [JsonPropertyName("lastUpdated")]
    public DateTime? LastUpdated { get; set; }
}

/// <summary>
/// Availability states for Talabat V2 API
/// </summary>
public static class TalabatAvailabilityState
{
    public const string Open = "OPEN";
    public const string Closed = "CLOSED";
    public const string ClosedUntil = "CLOSED_UNTIL";
    public const string ClosedToday = "CLOSED_TODAY";
    public const string Inactive = "INACTIVE";
    public const string Unknown = "UNKNOWN";
}

#endregion

