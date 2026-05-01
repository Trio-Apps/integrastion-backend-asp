using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace OrderXChange.BackgroundJobs;

public interface IMenuSyncDiagnosticsAppService : IApplicationService
{
    Task<PagedResultDto<MenuSyncRunSummaryDto>> GetRunsAsync(GetMenuSyncRunsInput input);
    Task<MenuSyncRunDetailsDto> GetRunDetailsAsync(Guid id);
    Task<List<MenuSyncVendorItemDto>> GetVendorItemsAsync(Guid id, string vendorCode);
}

public class GetMenuSyncRunsInput : PagedAndSortedResultRequestDto
{
    public Guid? FoodicsAccountId { get; set; }
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class MenuSyncRunSummaryDto
{
    public Guid Id { get; set; }
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string SyncType { get; set; } = string.Empty;
    public string TriggerSource { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Result { get; set; }
    public string? CurrentPhase { get; set; }
    public int ProgressPercentage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? DurationSeconds { get; set; }
    public int TotalProductsProcessed { get; set; }
    public int ProductsSucceeded { get; set; }
    public int ProductsFailed { get; set; }
    public int ProductsSkipped { get; set; }
    public int CategoriesProcessed { get; set; }
    public int ModifiersProcessed { get; set; }
    public int VendorSubmissionCount { get; set; }
    public int FailedVendorCount { get; set; }
    public int MissingVendorLogCount { get; set; }
}

public class MenuSyncRunDetailsDto : MenuSyncRunSummaryDto
{
    public string? TalabatVendorCode { get; set; }
    public string? TalabatImportId { get; set; }
    public string? TalabatSyncStatus { get; set; }
    public DateTime? TalabatSubmittedAt { get; set; }
    public DateTime? TalabatCompletedAt { get; set; }
    public string? ErrorsJson { get; set; }
    public string? WarningsJson { get; set; }
    public string? MetricsJson { get; set; }
    public string? ConfigurationJson { get; set; }
    public List<MenuSyncRunStepDto> Steps { get; set; } = new();
    public List<MenuSyncVendorSubmissionDto> Vendors { get; set; } = new();
}

public class MenuSyncRunStepDto
{
    public Guid Id { get; set; }
    public string StepType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Phase { get; set; }
    public DateTime Timestamp { get; set; }
    public int SequenceNumber { get; set; }
    public double? DurationSeconds { get; set; }
    public string? DataJson { get; set; }
}

public class MenuSyncVendorSubmissionDto
{
    public string VendorCode { get; set; } = string.Empty;
    public string? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? GroupId { get; set; }
    public string? GroupName { get; set; }
    public bool SyncAllBranches { get; set; }
    public bool IsActive { get; set; }
    public string? ImportId { get; set; }
    public string Status { get; set; } = "NotRecorded";
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ProductsCount { get; set; }
    public int CategoriesCount { get; set; }
    public int ProductsCreated { get; set; }
    public int ProductsUpdated { get; set; }
    public int CategoriesCreated { get; set; }
    public int CategoriesUpdated { get; set; }
    public int ErrorsCount { get; set; }
    public string? ResponseMessage { get; set; }
    public string? ErrorsJson { get; set; }
    public bool PayloadAvailable { get; set; }
    public int PayloadProducts { get; set; }
    public int PayloadToppings { get; set; }
    public int PayloadOptionProducts { get; set; }
    public int PayloadCategories { get; set; }
    public int StagedProducts { get; set; }
    public int StagedProductsWithModifiers { get; set; }
    public int StagedModifierGroups { get; set; }
    public int StagedRequiredModifierGroups { get; set; }
    public int StagedModifierOptions { get; set; }
    public DateTime? LatestStagingSyncDate { get; set; }
    public string? Diagnostic { get; set; }
}

public class MenuSyncVendorItemDto
{
    public string FoodicsProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameLocalized { get; set; }
    public string? CategoryName { get; set; }
    public decimal? Price { get; set; }
    public bool IsActive { get; set; }
    public DateTime SyncDate { get; set; }
    public string? TalabatSyncStatus { get; set; }
    public string? TalabatImportId { get; set; }
    public DateTime? TalabatSubmittedAt { get; set; }
    public int ModifierGroupsCount { get; set; }
    public int RequiredModifierGroupsCount { get; set; }
    public int ModifierOptionsCount { get; set; }
    public List<MenuSyncItemModifierDto> Modifiers { get; set; } = new();
}

public class MenuSyncItemModifierDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? NameLocalized { get; set; }
    public int Minimum { get; set; }
    public int Maximum { get; set; }
    public bool IsRequired { get; set; }
    public int OptionsCount { get; set; }
    public List<MenuSyncItemModifierOptionDto> Options { get; set; } = new();
}

public class MenuSyncItemModifierOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? NameLocalized { get; set; }
    public decimal? Price { get; set; }
    public bool? IsActive { get; set; }
}
