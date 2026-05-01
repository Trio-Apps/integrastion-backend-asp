using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Staging;
using OrderXChange.Domain.Versioning;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.TenantManagement.Talabat;
using System.Linq.Dynamic.Core;

namespace OrderXChange.BackgroundJobs;

public class MenuSyncDiagnosticsAppService : ApplicationService, IMenuSyncDiagnosticsAppService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IRepository<MenuSyncRun, Guid> _syncRunRepository;
    private readonly IRepository<MenuSyncRunStep, Guid> _syncRunStepRepository;
    private readonly IRepository<TalabatCatalogSyncLog, Guid> _catalogLogRepository;
    private readonly IRepository<FoodicsProductStaging, Guid> _productStagingRepository;
    private readonly IRepository<TalabatAccount, Guid> _talabatAccountRepository;

    public MenuSyncDiagnosticsAppService(
        IRepository<MenuSyncRun, Guid> syncRunRepository,
        IRepository<MenuSyncRunStep, Guid> syncRunStepRepository,
        IRepository<TalabatCatalogSyncLog, Guid> catalogLogRepository,
        IRepository<FoodicsProductStaging, Guid> productStagingRepository,
        IRepository<TalabatAccount, Guid> talabatAccountRepository)
    {
        _syncRunRepository = syncRunRepository;
        _syncRunStepRepository = syncRunStepRepository;
        _catalogLogRepository = catalogLogRepository;
        _productStagingRepository = productStagingRepository;
        _talabatAccountRepository = talabatAccountRepository;
    }

    public async Task<PagedResultDto<MenuSyncRunSummaryDto>> GetRunsAsync(GetMenuSyncRunsInput input)
    {
        var queryable = await _syncRunRepository.GetQueryableAsync();

        if (input.FoodicsAccountId.HasValue)
        {
            queryable = queryable.Where(x => x.FoodicsAccountId == input.FoodicsAccountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            var status = input.Status.Trim();
            queryable = queryable.Where(x => x.Status == status);
        }

        if (input.FromDate.HasValue)
        {
            queryable = queryable.Where(x => x.StartedAt >= input.FromDate.Value);
        }

        if (input.ToDate.HasValue)
        {
            queryable = queryable.Where(x => x.StartedAt <= input.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.SearchTerm))
        {
            var pattern = $"%{input.SearchTerm.Trim()}%";
            queryable = queryable.Where(x =>
                EF.Functions.Like(x.CorrelationId, pattern)
                || EF.Functions.Like(x.Status, pattern)
                || (x.Result != null && EF.Functions.Like(x.Result, pattern))
                || (x.CurrentPhase != null && EF.Functions.Like(x.CurrentPhase, pattern))
                || (x.TalabatVendorCode != null && EF.Functions.Like(x.TalabatVendorCode, pattern))
                || (x.TalabatImportId != null && EF.Functions.Like(x.TalabatImportId, pattern)));
        }

        var totalCount = await queryable.CountAsync();
        var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? "StartedAt desc" : input.Sorting;

        var runs = await queryable
            .OrderBy(sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        var summaries = new List<MenuSyncRunSummaryDto>();
        foreach (var run in runs)
        {
            summaries.Add(await MapRunSummaryAsync(run));
        }

        return new PagedResultDto<MenuSyncRunSummaryDto>(totalCount, summaries);
    }

    public async Task<MenuSyncRunDetailsDto> GetRunDetailsAsync(Guid id)
    {
        var run = await _syncRunRepository.GetAsync(id);
        var summary = await MapRunSummaryAsync(run);

        var stepsQueryable = await _syncRunStepRepository.GetQueryableAsync();
        var steps = await stepsQueryable
            .Where(x => x.MenuSyncRunId == id)
            .OrderBy(x => x.SequenceNumber)
            .ThenBy(x => x.Timestamp)
            .ToListAsync();

        var details = new MenuSyncRunDetailsDto
        {
            Id = summary.Id,
            FoodicsAccountId = summary.FoodicsAccountId,
            BranchId = summary.BranchId,
            CorrelationId = summary.CorrelationId,
            SyncType = summary.SyncType,
            TriggerSource = summary.TriggerSource,
            Status = summary.Status,
            Result = summary.Result,
            CurrentPhase = summary.CurrentPhase,
            ProgressPercentage = summary.ProgressPercentage,
            StartedAt = summary.StartedAt,
            CompletedAt = summary.CompletedAt,
            DurationSeconds = summary.DurationSeconds,
            TotalProductsProcessed = summary.TotalProductsProcessed,
            ProductsSucceeded = summary.ProductsSucceeded,
            ProductsFailed = summary.ProductsFailed,
            ProductsSkipped = summary.ProductsSkipped,
            CategoriesProcessed = summary.CategoriesProcessed,
            ModifiersProcessed = summary.ModifiersProcessed,
            VendorSubmissionCount = summary.VendorSubmissionCount,
            FailedVendorCount = summary.FailedVendorCount,
            MissingVendorLogCount = summary.MissingVendorLogCount,
            TalabatVendorCode = run.TalabatVendorCode,
            TalabatImportId = run.TalabatImportId,
            TalabatSyncStatus = run.TalabatSyncStatus,
            TalabatSubmittedAt = run.TalabatSubmittedAt,
            TalabatCompletedAt = run.TalabatCompletedAt,
            ErrorsJson = run.ErrorsJson,
            WarningsJson = run.WarningsJson,
            MetricsJson = run.MetricsJson,
            ConfigurationJson = run.ConfigurationJson,
            Steps = steps.Select(MapStep).ToList(),
            Vendors = await BuildVendorSubmissionsAsync(run)
        };

        return details;
    }

    public async Task<List<MenuSyncVendorItemDto>> GetVendorItemsAsync(Guid id, string vendorCode)
    {
        var run = await _syncRunRepository.GetAsync(id);
        var account = await FindTalabatAccountAsync(run.FoodicsAccountId, vendorCode);
        if (account == null)
        {
            return new List<MenuSyncVendorItemDto>();
        }

        var stagedProducts = await GetFilteredStagedProductsAsync(run.FoodicsAccountId, account);
        return stagedProducts
            .OrderBy(x => x.CategoryName)
            .ThenBy(x => x.Name)
            .Select(MapVendorItem)
            .ToList();
    }

    private async Task<MenuSyncRunSummaryDto> MapRunSummaryAsync(MenuSyncRun run)
    {
        var vendors = await BuildVendorSubmissionsAsync(run, includeStagingStats: false);
        return new MenuSyncRunSummaryDto
        {
            Id = run.Id,
            FoodicsAccountId = run.FoodicsAccountId,
            BranchId = run.BranchId,
            CorrelationId = run.CorrelationId,
            SyncType = run.SyncType,
            TriggerSource = run.TriggerSource,
            Status = run.Status,
            Result = run.Result,
            CurrentPhase = run.CurrentPhase,
            ProgressPercentage = run.ProgressPercentage,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            DurationSeconds = run.Duration?.TotalSeconds ?? (run.CompletedAt - run.StartedAt)?.TotalSeconds,
            TotalProductsProcessed = run.TotalProductsProcessed,
            ProductsSucceeded = run.ProductsSucceeded,
            ProductsFailed = run.ProductsFailed,
            ProductsSkipped = run.ProductsSkipped,
            CategoriesProcessed = run.CategoriesProcessed,
            ModifiersProcessed = run.ModifiersProcessed,
            VendorSubmissionCount = vendors.Count(x => !string.Equals(x.Status, "NotRecorded", StringComparison.OrdinalIgnoreCase)),
            FailedVendorCount = vendors.Count(x => IsFailedStatus(x.Status)),
            MissingVendorLogCount = vendors.Count(x => string.Equals(x.Status, "NotRecorded", StringComparison.OrdinalIgnoreCase))
        };
    }

    private async Task<List<MenuSyncVendorSubmissionDto>> BuildVendorSubmissionsAsync(
        MenuSyncRun run,
        bool includeStagingStats = true)
    {
        var accountsQueryable = await _talabatAccountRepository.GetQueryableAsync();
        var accounts = await accountsQueryable
            .Where(x => x.FoodicsAccountId == run.FoodicsAccountId && x.IsActive)
            .OrderBy(x => x.VendorCode)
            .ToListAsync();

        var logStart = run.StartedAt.AddMinutes(-5);
        var logEnd = (run.CompletedAt ?? DateTime.UtcNow).AddMinutes(30);
        var logsQueryable = await _catalogLogRepository.GetQueryableAsync();
        var logs = await logsQueryable
            .Where(x => x.FoodicsAccountId == run.FoodicsAccountId)
            .Where(x =>
                x.CorrelationId == run.CorrelationId
                || (x.SubmittedAt >= logStart && x.SubmittedAt <= logEnd))
            .OrderByDescending(x => x.SubmittedAt)
            .ToListAsync();

        var results = new List<MenuSyncVendorSubmissionDto>();
        foreach (var account in accounts)
        {
            var log = logs.FirstOrDefault(x => string.Equals(x.VendorCode, account.VendorCode, StringComparison.OrdinalIgnoreCase));
            var dto = new MenuSyncVendorSubmissionDto
            {
                VendorCode = account.VendorCode,
                BranchId = account.FoodicsBranchId,
                BranchName = account.FoodicsBranchName,
                GroupId = account.FoodicsGroupId,
                GroupName = account.FoodicsGroupName,
                SyncAllBranches = account.SyncAllBranches,
                IsActive = account.IsActive
            };

            if (log != null)
            {
                dto.ImportId = log.ImportId;
                dto.Status = log.Status;
                dto.SubmittedAt = log.SubmittedAt;
                dto.CompletedAt = log.CompletedAt;
                dto.ProductsCount = log.ProductsCount;
                dto.CategoriesCount = log.CategoriesCount;
                dto.ProductsCreated = log.ProductsCreated;
                dto.ProductsUpdated = log.ProductsUpdated;
                dto.CategoriesCreated = log.CategoriesCreated;
                dto.CategoriesUpdated = log.CategoriesUpdated;
                dto.ErrorsCount = log.ErrorsCount;
                dto.ResponseMessage = log.ResponseMessage;
                dto.ErrorsJson = log.ErrorsJson;

                var payloadStats = ReadPayloadStats(log.WebhookPayloadJson);
                dto.PayloadAvailable = payloadStats.PayloadAvailable;
                dto.PayloadProducts = payloadStats.Products;
                dto.PayloadToppings = payloadStats.Toppings;
                dto.PayloadOptionProducts = payloadStats.OptionProducts;
                dto.PayloadCategories = payloadStats.Categories;
            }
            else
            {
                dto.Diagnostic = "No catalog sync log was recorded for this vendor in the selected run window.";
            }

            if (includeStagingStats)
            {
                ApplyStagingStats(dto, await GetFilteredStagedProductsAsync(run.FoodicsAccountId, account));
            }

            results.Add(dto);
        }

        return results
            .OrderByDescending(x => IsFailedStatus(x.Status))
            .ThenByDescending(x => string.Equals(x.Status, "NotRecorded", StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.VendorCode)
            .ToList();
    }

    private async Task<TalabatAccount?> FindTalabatAccountAsync(Guid foodicsAccountId, string vendorCode)
    {
        var accountsQueryable = await _talabatAccountRepository.GetQueryableAsync();
        return await accountsQueryable
            .Where(x => x.FoodicsAccountId == foodicsAccountId)
            .FirstOrDefaultAsync(x => x.VendorCode == vendorCode);
    }

    private async Task<List<FoodicsProductStaging>> GetFilteredStagedProductsAsync(Guid foodicsAccountId, TalabatAccount account)
    {
        var queryable = await _productStagingRepository.GetQueryableAsync();
        var products = await queryable
            .Where(x => x.FoodicsAccountId == foodicsAccountId && x.IsActive && !x.IsDeleted)
            .ToListAsync();

        return products
            .Where(product => MatchesBranch(product, account))
            .Where(product => MatchesGroup(product, account))
            .ToList();
    }

    private static bool MatchesBranch(FoodicsProductStaging product, TalabatAccount account)
    {
        if (account.SyncAllBranches || string.IsNullOrWhiteSpace(account.FoodicsBranchId))
        {
            return true;
        }

        if (string.Equals(product.BranchId, account.FoodicsBranchId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(product.BranchesJson) || product.BranchesJson == "[]")
        {
            return true;
        }

        return product.BranchesJson.Contains(account.FoodicsBranchId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesGroup(FoodicsProductStaging product, TalabatAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.FoodicsGroupId))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(product.GroupsJson)
               && product.GroupsJson.Contains(account.FoodicsGroupId, StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyStagingStats(MenuSyncVendorSubmissionDto dto, List<FoodicsProductStaging> products)
    {
        var modifierStats = products.Select(ReadModifierStats).ToList();
        dto.StagedProducts = products.Count;
        dto.StagedProductsWithModifiers = modifierStats.Count(x => x.Groups > 0);
        dto.StagedModifierGroups = modifierStats.Sum(x => x.Groups);
        dto.StagedRequiredModifierGroups = modifierStats.Sum(x => x.RequiredGroups);
        dto.StagedModifierOptions = modifierStats.Sum(x => x.Options);
        dto.LatestStagingSyncDate = products.Count == 0 ? null : products.Max(x => x.SyncDate);

        if (dto.ProductsCount > 0 && dto.StagedProducts > 0 && Math.Abs(dto.ProductsCount - dto.StagedProducts) > 5)
        {
            dto.Diagnostic = $"Talabat payload product count ({dto.ProductsCount}) differs from current staging filter ({dto.StagedProducts}).";
        }
    }

    private static MenuSyncVendorItemDto MapVendorItem(FoodicsProductStaging product)
    {
        var modifiers = ReadModifiers(product.ModifiersJson);
        var dtoModifiers = modifiers.Select(modifier =>
        {
            var options = FoodicsModifierSanitizer.GetVisibleOptions(modifier).ToList();
            var min = modifier.MinAllowed ?? 0;
            var max = modifier.MaxAllowed ?? 0;
            return new MenuSyncItemModifierDto
            {
                Id = modifier.Id,
                Name = modifier.Name,
                NameLocalized = modifier.NameLocalized,
                Minimum = min,
                Maximum = max,
                IsRequired = min > 0,
                OptionsCount = options.Count,
                Options = options.Select(option => new MenuSyncItemModifierOptionDto
                {
                    Id = option.Id,
                    Name = option.Name,
                    NameLocalized = option.NameLocalized,
                    Price = option.Price,
                    IsActive = option.IsActive
                }).ToList()
            };
        }).ToList();

        return new MenuSyncVendorItemDto
        {
            FoodicsProductId = product.FoodicsProductId,
            Name = product.Name,
            NameLocalized = product.NameLocalized,
            CategoryName = product.CategoryName,
            Price = product.Price,
            IsActive = product.IsActive,
            SyncDate = product.SyncDate,
            TalabatSyncStatus = product.TalabatSyncStatus,
            TalabatImportId = product.TalabatImportId,
            TalabatSubmittedAt = product.TalabatSubmittedAt,
            ModifierGroupsCount = dtoModifiers.Count,
            RequiredModifierGroupsCount = dtoModifiers.Count(x => x.IsRequired),
            ModifierOptionsCount = dtoModifiers.Sum(x => x.OptionsCount),
            Modifiers = dtoModifiers
        };
    }

    private static MenuSyncRunStepDto MapStep(MenuSyncRunStep step)
    {
        return new MenuSyncRunStepDto
        {
            Id = step.Id,
            StepType = step.StepType,
            Message = step.Message,
            Phase = step.Phase,
            Timestamp = step.Timestamp,
            SequenceNumber = step.SequenceNumber,
            DurationSeconds = step.Duration?.TotalSeconds,
            DataJson = step.DataJson
        };
    }

    private static (int Groups, int RequiredGroups, int Options) ReadModifierStats(FoodicsProductStaging product)
    {
        var modifiers = ReadModifiers(product.ModifiersJson);
        return (
            modifiers.Count,
            modifiers.Count(x => (x.MinAllowed ?? 0) > 0),
            modifiers.Sum(x => FoodicsModifierSanitizer.GetVisibleOptions(x).Count())
        );
    }

    private static List<FoodicsModifierDto> ReadModifiers(string? modifiersJson)
    {
        if (string.IsNullOrWhiteSpace(modifiersJson))
        {
            return new List<FoodicsModifierDto>();
        }

        try
        {
            var modifiers = JsonSerializer.Deserialize<List<FoodicsModifierDto>>(modifiersJson, JsonOptions);
            return FoodicsModifierSanitizer.SanitizeForMenuProjection(modifiers) ?? new List<FoodicsModifierDto>();
        }
        catch
        {
            return new List<FoodicsModifierDto>();
        }
    }

    private static (bool PayloadAvailable, int Products, int Toppings, int OptionProducts, int Categories) ReadPayloadStats(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return (false, 0, 0, 0, 0);
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (!doc.RootElement.TryGetProperty("catalog", out var catalog)
                || !catalog.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Object)
            {
                return (true, 0, 0, 0, 0);
            }

            var products = 0;
            var optionProducts = 0;
            var toppings = 0;
            var categories = 0;

            foreach (var item in items.EnumerateObject())
            {
                var type = item.Value.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString()
                    : null;

                if (string.Equals(type, "Topping", StringComparison.OrdinalIgnoreCase))
                {
                    toppings++;
                }
                else if (string.Equals(type, "Category", StringComparison.OrdinalIgnoreCase))
                {
                    categories++;
                }
                else if (string.Equals(type, "Product", StringComparison.OrdinalIgnoreCase))
                {
                    if (item.Name.StartsWith("P_", StringComparison.OrdinalIgnoreCase))
                    {
                        products++;
                    }
                    else if (item.Name.StartsWith("tt-", StringComparison.OrdinalIgnoreCase))
                    {
                        optionProducts++;
                    }
                }
            }

            return (true, products, toppings, optionProducts, categories);
        }
        catch
        {
            return (false, 0, 0, 0, 0);
        }
    }

    private static bool IsFailedStatus(string? status)
    {
        return string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "Error", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "Partial", StringComparison.OrdinalIgnoreCase);
    }
}
