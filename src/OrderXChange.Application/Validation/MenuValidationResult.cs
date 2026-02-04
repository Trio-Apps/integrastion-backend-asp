using System;
using System.Collections.Generic;
using System.Linq;

namespace OrderXChange.Application.Validation;

/// <summary>
/// Standardized validation result format for menu validation pipeline
/// Provides structured error reporting with severity levels and actionable information
/// </summary>
public class MenuValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
    public ValidationStatistics Statistics { get; set; } = new();
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan ValidationDuration { get; set; }

    /// <summary>
    /// Gets all critical errors that prevent Talabat submission
    /// </summary>
    public List<ValidationError> CriticalErrors => 
        Errors.Where(e => e.Severity == ValidationSeverity.Critical).ToList();

    /// <summary>
    /// Gets all blocking errors that should fail the validation
    /// </summary>
    public List<ValidationError> BlockingErrors => 
        Errors.Where(e => e.Severity == ValidationSeverity.Critical || e.Severity == ValidationSeverity.Error).ToList();

    /// <summary>
    /// Determines if the menu can be submitted to Talabat
    /// </summary>
    public bool CanSubmitToTalabat => IsValid && !CriticalErrors.Any();

    /// <summary>
    /// Gets a summary of validation issues
    /// </summary>
    public string GetSummary()
    {
        if (IsValid && !Warnings.Any())
            return "Validation passed with no issues";

        var parts = new List<string>();
        
        if (CriticalErrors.Any())
            parts.Add($"{CriticalErrors.Count} critical error(s)");
        
        var regularErrors = Errors.Where(e => e.Severity == ValidationSeverity.Error).Count();
        if (regularErrors > 0)
            parts.Add($"{regularErrors} error(s)");
        
        if (Warnings.Any())
            parts.Add($"{Warnings.Count} warning(s)");

        return string.Join(", ", parts);
    }
}

/// <summary>
/// Validation error with structured information
/// </summary>
public class ValidationError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ValidationSeverity Severity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? EntityName { get; set; }
    public string? FieldName { get; set; }
    public object? CurrentValue { get; set; }
    public object? ExpectedValue { get; set; }
    public string? SuggestedFix { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Creates a critical validation error
    /// </summary>
    public static ValidationError Critical(string code, string message, string category, 
        string? entityType = null, string? entityId = null, string? fieldName = null)
    {
        return new ValidationError
        {
            Code = code,
            Message = message,
            Severity = ValidationSeverity.Critical,
            Category = category,
            EntityType = entityType,
            EntityId = entityId,
            FieldName = fieldName
        };
    }

    /// <summary>
    /// Creates a regular validation error
    /// </summary>
    public static ValidationError Error(string code, string message, string category, 
        string? entityType = null, string? entityId = null, string? fieldName = null)
    {
        return new ValidationError
        {
            Code = code,
            Message = message,
            Severity = ValidationSeverity.Error,
            Category = category,
            EntityType = entityType,
            EntityId = entityId,
            FieldName = fieldName
        };
    }

    /// <summary>
    /// Creates a warning-level validation error
    /// </summary>
    public static ValidationError Create(string code, string message, string category, 
        string? entityType = null, string? entityId = null, string? suggestedFix = null)
    {
        return new ValidationError
        {
            Code = code,
            Message = message,
            Severity = ValidationSeverity.Warning,
            Category = category,
            EntityType = entityType,
            EntityId = entityId,
            SuggestedFix = suggestedFix
        };
    }
}

/// <summary>
/// Validation warning for non-blocking issues
/// </summary>
public class ValidationWarning
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? EntityName { get; set; }
    public string? SuggestedFix { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Creates a validation warning
    /// </summary>
    public static ValidationWarning Create(string code, string message, string category, 
        string? entityType = null, string? entityId = null, string? suggestedFix = null)
    {
        return new ValidationWarning
        {
            Code = code,
            Message = message,
            Category = category,
            EntityType = entityType,
            EntityId = entityId,
            SuggestedFix = suggestedFix
        };
    }
}

/// <summary>
/// Validation severity levels
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Critical errors that will cause Talabat API rejection
    /// </summary>
    Critical = 0,
    
    /// <summary>
    /// Errors that should block submission but might not cause API rejection
    /// </summary>
    Error = 1,
    
    /// <summary>
    /// Warnings that don't block submission but should be addressed
    /// </summary>
    Warning = 2,
    
    /// <summary>
    /// Informational messages
    /// </summary>
    Info = 3
}

/// <summary>
/// Validation statistics for monitoring and reporting
/// </summary>
public class ValidationStatistics
{
    public int TotalProducts { get; set; }
    public int TotalCategories { get; set; }
    public int TotalModifiers { get; set; }
    public int TotalModifierOptions { get; set; }
    public int ValidProducts { get; set; }
    public int InvalidProducts { get; set; }
    public int ProductsWithWarnings { get; set; }
    public int OrphanedProducts { get; set; }
    public int EmptyCategories { get; set; }
    public int ModifiersWithoutOptions { get; set; }
    public decimal AverageProductPrice { get; set; }
    public decimal MinProductPrice { get; set; }
    public decimal MaxProductPrice { get; set; }
    public int ProductsWithImages { get; set; }
    public int ProductsWithoutImages { get; set; }

    /// <summary>
    /// Calculates success rate as percentage
    /// </summary>
    public double SuccessRate => TotalProducts > 0 ? (double)ValidProducts / TotalProducts * 100 : 100;
}

/// <summary>
/// Validation categories for error classification
/// </summary>
public static class ValidationCategory
{
    public const string RequiredFields = "RequiredFields";
    public const string PriceConsistency = "PriceConsistency";
    public const string ModifierCorrectness = "ModifierCorrectness";
    public const string DataIntegrity = "DataIntegrity";
    public const string TalabatCompliance = "TalabatCompliance";
    public const string BusinessRules = "BusinessRules";
    public const string Performance = "Performance";
    public const string ImageValidation = "ImageValidation";
    public const string LocalizationValidation = "LocalizationValidation";
}

/// <summary>
/// Standard validation error codes
/// </summary>
public static class ValidationErrorCode
{
    // Required Fields
    public const string MissingProductName = "MISSING_PRODUCT_NAME";
    public const string MissingProductId = "MISSING_PRODUCT_ID";
    public const string MissingCategoryName = "MISSING_CATEGORY_NAME";
    public const string MissingModifierName = "MISSING_MODIFIER_NAME";
    public const string MissingRemoteCode = "MISSING_REMOTE_CODE";

    // Price Consistency
    public const string NegativePrice = "NEGATIVE_PRICE";
    public const string ZeroPrice = "ZERO_PRICE";
    public const string ExcessivePrice = "EXCESSIVE_PRICE";
    public const string InvalidPriceFormat = "INVALID_PRICE_FORMAT";
    public const string ModifierPriceInconsistency = "MODIFIER_PRICE_INCONSISTENCY";

    // Modifier Correctness
    public const string ModifierWithoutOptions = "MODIFIER_WITHOUT_OPTIONS";
    public const string InvalidModifierSelection = "INVALID_MODIFIER_SELECTION";
    public const string OrphanedModifierOption = "ORPHANED_MODIFIER_OPTION";
    public const string DuplicateModifierOption = "DUPLICATE_MODIFIER_OPTION";

    // Data Integrity
    public const string DuplicateRemoteCode = "DUPLICATE_REMOTE_CODE";
    public const string OrphanedProduct = "ORPHANED_PRODUCT";
    public const string EmptyCategory = "EMPTY_CATEGORY";
    public const string CircularReference = "CIRCULAR_REFERENCE";
    public const string InvalidCharacters = "INVALID_CHARACTERS";

    // Talabat Compliance
    public const string InvalidImageUrl = "INVALID_IMAGE_URL";
    public const string ExcessiveNameLength = "EXCESSIVE_NAME_LENGTH";
    public const string ExcessiveDescriptionLength = "EXCESSIVE_DESCRIPTION_LENGTH";
    public const string UnsupportedCharacters = "UNSUPPORTED_CHARACTERS";
    public const string InvalidCallbackUrl = "INVALID_CALLBACK_URL";

    // Business Rules
    public const string InactiveProductInActiveCategory = "INACTIVE_PRODUCT_IN_ACTIVE_CATEGORY";
    public const string RequiredModifierNotSelected = "REQUIRED_MODIFIER_NOT_SELECTED";
    public const string ExcessiveModifierOptions = "EXCESSIVE_MODIFIER_OPTIONS";
}

/// <summary>
/// Delta validation result for validating delta payloads
/// </summary>
public class DeltaValidationResult
{
    public bool IsValid { get; set; } = true;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> MissingDependencies { get; set; } = new();
}

/// <summary>
/// Mapping validation result for ID-based mapping validation
/// </summary>
public class MappingValidationResult
{
    public int ValidMappings { get; set; }
    public int FixedMappings { get; set; }
    public List<string> Issues { get; set; } = new();
    public bool IsValid => !Issues.Any();
}

/// <summary>
/// Mapping statistics for monitoring and reporting
/// </summary>
public class MappingStatistics
{
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public int TotalMappings { get; set; }
    public int ActiveMappings { get; set; }
    public int InactiveMappings { get; set; }
    public int ProductMappings { get; set; }
    public int CategoryMappings { get; set; }
    public int ModifierMappings { get; set; }
    public int ModifierOptionMappings { get; set; }
    public DateTime LastUpdated { get; set; }
    public Dictionary<string, int> MappingsByEntityType { get; set; } = new();
}