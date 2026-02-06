using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Staging;
using OrderXChange.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Foodics;

namespace OrderXChange.Application.Staging;

/// <summary>
/// Service for managing Foodics product staging data.
/// Handles saving and updating product data from Foodics API to staging table.
/// </summary>
public class FoodicsProductStagingService : ITransientDependency
{
	private readonly IRepository<FoodicsProductStaging, Guid> _stagingRepository;
	private readonly IRepository<FoodicsAccount, Guid> _foodicsAccountRepository;
	private readonly ICurrentTenant _currentTenant;
	private readonly ILogger<FoodicsProductStagingService> _logger;
	private readonly IDbContextProvider<OrderXChangeDbContext> _dbContextProvider;
	private readonly IUnitOfWorkManager _unitOfWorkManager;

	public FoodicsProductStagingService(
		IRepository<FoodicsProductStaging, Guid> stagingRepository,
		IRepository<FoodicsAccount, Guid> foodicsAccountRepository,
		ICurrentTenant currentTenant,
		IDbContextProvider<OrderXChangeDbContext> dbContextProvider,
		IUnitOfWorkManager unitOfWorkManager,
		ILogger<FoodicsProductStagingService> logger)
	{
		_stagingRepository = stagingRepository;
		_foodicsAccountRepository = foodicsAccountRepository;
		_currentTenant = currentTenant;
		_dbContextProvider = dbContextProvider;
		_unitOfWorkManager = unitOfWorkManager;
		_logger = logger;
	}

	/// <summary>
	/// Saves or updates products from Foodics to staging table.
	/// Uses batch processing for better performance.
	/// </summary>
	/// <param name="foodicsAccountId">FoodicsAccount ID</param>
	/// <param name="products">Products to save</param>
	/// <param name="branchId">Optional branch ID if syncing for specific branch</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Sync result with counts</returns>
	[UnitOfWork]
    public virtual async Task<FoodicsProductSyncResult> SaveProductsToStagingAsync(
    Guid foodicsAccountId,
    IEnumerable<FoodicsProductDetailDto> products,
    string? branchId = null,
    CancellationToken cancellationToken = default)
    {
        // Verify FoodicsAccount exists
        var account = await _foodicsAccountRepository.GetAsync(foodicsAccountId, cancellationToken: cancellationToken);
        if (account == null)
        {
            throw new InvalidOperationException($"FoodicsAccount with Id {foodicsAccountId} not found.");
        }

        // Detach the account and any related entities from the tracker to prevent them from being saved
        var mainDbContext = await _dbContextProvider.GetDbContextAsync();
        mainDbContext.Entry(account).State = EntityState.Detached;

        // Dedupe by ProductId defensively (Foodics APIs may occasionally return duplicates across pages/joins)
        var productsList = products
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        var syncDate = DateTime.UtcNow;

        var result = new FoodicsProductSyncResult
        {
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            SyncDate = syncDate
        };

        _logger.LogInformation(
            "Starting to save {Count} products to staging for FoodicsAccount {AccountId}, Tenant {TenantId}, Branch {BranchId}",
            productsList.Count,
            foodicsAccountId,
            _currentTenant.Id ?? Guid.Empty,
            branchId ?? "<all>");

        // Process in batches for better performance
        const int batchSize = 100;
        var batches = productsList
            .Select((product, index) => new { product, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.product).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            foreach (var product in batch)
            {
                try
                {
                    // Get DbContext to use AsNoTracking for reads
                    var dbContext = await _dbContextProvider.GetDbContextAsync();

                    // ✅ FIXED: Check only by (FoodicsAccountId, FoodicsProductId)
                    // BranchId is NOT part of the unique key - one record per product per account
                    // Branch availability is stored in BranchesJson field
                    var existingProduct = await dbContext.Set<FoodicsProductStaging>()
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x => x.FoodicsAccountId == foodicsAccountId &&
                                 x.FoodicsProductId == product.Id,
                            cancellationToken);

                    if (existingProduct != null)
                    {
                        // Update existing - reload with tracking to get latest version (including soft-deleted rows)
                        var trackedProduct = await dbContext.Set<FoodicsProductStaging>()
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(x => x.Id == existingProduct.Id, cancellationToken);

                        if (trackedProduct == null)
                        {
                            // Fallback: repository (should be rare)
                            trackedProduct = await _stagingRepository.GetAsync(existingProduct.Id, cancellationToken: cancellationToken);
                        }

                        // If the existing row is soft-deleted, revive it
                        if (trackedProduct.IsDeleted)
                        {
                            trackedProduct.IsDeleted = false;
                            trackedProduct.DeletionTime = null;
                            trackedProduct.DeleterId = null;
                        }

                        // Update properties with latest data from Foodics
                        UpdateStagingProduct(trackedProduct, product, branchId, syncDate, account.TenantId);

                        await _stagingRepository.UpdateAsync(trackedProduct, autoSave: true, cancellationToken: cancellationToken);
                        result.UpdatedCount++;
                    }
                    else
                    {
                        // Create new product staging entry
                        var stagingProduct = CreateStagingProduct(foodicsAccountId, product, branchId, syncDate, account.TenantId);

                        try
                        {
                            await _stagingRepository.InsertAsync(stagingProduct, autoSave: true, cancellationToken: cancellationToken);
                            result.SavedCount++;
                        }
                        catch (DbUpdateException ex) when (
                            ex.InnerException?.Message?.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true ||
                            ex.InnerException?.Message?.Contains("IX_FoodicsProductStaging_Account_Product", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            // Race condition protection:
                            // Another job/UoW inserted the same (FoodicsAccountId, FoodicsProductId) between our read and insert.
                            // Recover by updating the existing row in a fresh (requiresNew) transaction so we can see the committed insert.
                            _logger.LogWarning(
                                ex,
                                "Duplicate staging row detected for FoodicsAccountId={AccountId}, FoodicsProductId={ProductId}. Falling back to update. TenantId={TenantId}",
                                foodicsAccountId,
                                product.Id,
                                _currentTenant.Id ?? Guid.Empty);

                            using (var retryUow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
                            {
                                var retryDbContext = await _dbContextProvider.GetDbContextAsync();
                                var existingId = await retryDbContext.Set<FoodicsProductStaging>()
                                    .IgnoreQueryFilters()
                                    .AsNoTracking()
                                    .Where(x => x.FoodicsAccountId == foodicsAccountId && x.FoodicsProductId == product.Id)
                                    .Select(x => x.Id)
                                    .FirstOrDefaultAsync(cancellationToken);

                                if (existingId == Guid.Empty)
                                {
                                    // If we still can't find it, rethrow - something else is wrong (rollback, different tenant, etc.)
                                    throw;
                                }

                                var trackedProduct = await retryDbContext.Set<FoodicsProductStaging>()
                                    .IgnoreQueryFilters()
                                    .FirstOrDefaultAsync(x => x.Id == existingId, cancellationToken);

                                if (trackedProduct == null)
                                {
                                    trackedProduct = await _stagingRepository.GetAsync(existingId, cancellationToken: cancellationToken);
                                }

                                if (trackedProduct.IsDeleted)
                                {
                                    trackedProduct.IsDeleted = false;
                                    trackedProduct.DeletionTime = null;
                                    trackedProduct.DeleterId = null;
                                }
                                UpdateStagingProduct(trackedProduct, product, branchId, syncDate, account.TenantId);
                                await _stagingRepository.UpdateAsync(trackedProduct, autoSave: true, cancellationToken: cancellationToken);
                                result.UpdatedCount++;

                                await retryUow.CompleteAsync(cancellationToken);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorCount++;
                    _logger.LogError(
                        ex,
                        "Error saving product {ProductId} (Name: {ProductName}) to staging for FoodicsAccount {AccountId}, Branch {BranchId}. Error: {Error}",
                        product.Id,
                        product.Name,
                        foodicsAccountId,
                        branchId ?? "<all>",
                        ex.Message);
                    // Continue with next product - don't let one product failure stop the entire sync
                }
            }
        }

        result.TotalProcessed = productsList.Count;
        result.SuccessCount = result.SavedCount + result.UpdatedCount;

        // Ensure we don't accidentally try to update AbpTenants in this UoW (can cause concurrency issues)
        await ClearModifiedTenantEntriesAsync();

        _logger.LogInformation(
            "Finished saving products to staging. Saved: {Saved}, Updated: {Updated}, Errors: {Errors}, Total: {Total} for FoodicsAccount {AccountId}, Branch {BranchId}",
            result.SavedCount,
            result.UpdatedCount,
            result.ErrorCount,
            result.TotalProcessed,
            foodicsAccountId,
            branchId ?? "<all>");

        return result;
    }

    private async Task ClearModifiedTenantEntriesAsync()
	{
		try
		{
			var dbContext = await _dbContextProvider.GetDbContextAsync();
			
			// Detach all Tenant entities to prevent them from being saved
			foreach (var entry in dbContext.ChangeTracker.Entries<Tenant>().ToList())
			{
				_logger.LogWarning(
					"Found Tenant entity in change tracker with state {State}. Detaching to prevent save. Tenant Id: {TenantId}",
					entry.State,
					entry.Entity.Id);
				entry.State = EntityState.Detached;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Could not clear tenant entries from change tracker");
		}
	}

	/// <summary>
	/// Gets all products from staging for a specific FoodicsAccount
	/// </summary>
	public async Task<List<FoodicsProductStaging>> GetProductsByAccountAsync(
		Guid foodicsAccountId,
		string? branchId = null,
		bool? isActive = null,
		CancellationToken cancellationToken = default)
	{
		// Get all products for the account and filter in memory
		// For better performance with large datasets, consider using repository filters
		var allProducts = await _stagingRepository.GetListAsync(
			x => x.FoodicsAccountId == foodicsAccountId,
			cancellationToken: cancellationToken);

		var filtered = allProducts.AsQueryable();

		if (!string.IsNullOrWhiteSpace(branchId))
		{
			filtered = filtered.Where(x => x.BranchId == branchId);
		}

		if (isActive.HasValue)
		{
			filtered = filtered.Where(x => x.IsActive == isActive.Value);
		}

		return filtered.ToList();
	}

		private static FoodicsProductStaging CreateStagingProduct(
		Guid foodicsAccountId,
		FoodicsProductDetailDto product,
		string? branchId,
		DateTime syncDate,
		Guid? tenantId)
		{
			var isDeletedInFoodics = !string.IsNullOrWhiteSpace(product.DeletedAt);

			return new FoodicsProductStaging
			{
				FoodicsAccountId = foodicsAccountId,
				FoodicsProductId = product.Id,
				Name = product.Name ?? string.Empty,
				NameLocalized = product.NameLocalized,
				Description = product.Description,
				DescriptionLocalized = product.DescriptionLocalized,
				Image = product.Image,
				IsActive = product.IsActive ?? false,
				Sku = product.Sku,
				Barcode = product.Barcode,
				CategoryId = product.Category?.Id ?? product.CategoryId,
				CategoryName = product.Category?.Name,
				TaxGroupId = product.TaxGroupId,
				TaxGroupName = product.TaxGroup?.Name,
				TaxRate = product.TaxGroup?.Rate,
				Price = product.Price,
				ProductDetailsJson = SerializeProductDetails(product),
				BranchesJson = SerializeToJson(product.Branches),
				ModifiersJson = SerializeToJson(product.Modifiers),
				GroupsJson = SerializeToJson(product.Groups),
				PriceTagsJson = SerializeToJson(product.PriceTags),
				TagsJson = SerializeToJson(product.Tags),
				IngredientsJson = SerializeToJson(product.Ingredients),
				DiscountsJson = SerializeToJson(product.Discounts),
				TimedEventsJson = SerializeToJson(product.TimedEvents),
				SyncDate = syncDate,
				BranchId = branchId,
				TenantId = tenantId,

				// Soft delete flags based on Foodics deleted_at
				IsDeleted = isDeletedInFoodics,
				DeletedAt = isDeletedInFoodics ? ParseFoodicsDate(product.DeletedAt) : null,
				DeletionReason = isDeletedInFoodics ? "RemovedFromFoodics" : null,
				// Deletion sync flags will be handled by Talabat deletion sync pipeline
				IsDeletionSyncedToTalabat = false,
				DeletionSyncedAt = null,
				DeletionSyncError = null
			};
		}

		private static void UpdateStagingProduct(
		FoodicsProductStaging staging,
		FoodicsProductDetailDto product,
		string? branchId,
		DateTime syncDate,
		Guid? tenantId)
		{
			var isDeletedInFoodics = !string.IsNullOrWhiteSpace(product.DeletedAt);

			staging.Name = product.Name ?? string.Empty;
			staging.NameLocalized = product.NameLocalized;
			staging.Description = product.Description;
			staging.DescriptionLocalized = product.DescriptionLocalized;
			staging.Image = product.Image;
			staging.IsActive = product.IsActive ?? false;
			staging.Sku = product.Sku;
			staging.Barcode = product.Barcode;
			staging.CategoryId = product.Category?.Id ?? product.CategoryId;
			staging.CategoryName = product.Category?.Name;
			staging.TaxGroupId = product.TaxGroupId;
			staging.TaxGroupName = product.TaxGroup?.Name;
			staging.TaxRate = product.TaxGroup?.Rate;
			staging.Price = product.Price;
			staging.ProductDetailsJson = SerializeProductDetails(product);
			staging.BranchesJson = SerializeToJson(product.Branches);
			staging.ModifiersJson = SerializeToJson(product.Modifiers);
			staging.GroupsJson = SerializeToJson(product.Groups);
			staging.PriceTagsJson = SerializeToJson(product.PriceTags);
			staging.TagsJson = SerializeToJson(product.Tags);
			staging.IngredientsJson = SerializeToJson(product.Ingredients);
			staging.DiscountsJson = SerializeToJson(product.Discounts);
			staging.TimedEventsJson = SerializeToJson(product.TimedEvents);
			staging.SyncDate = syncDate;
			staging.BranchId = branchId;
			staging.TenantId = tenantId;

			// Update soft delete flags based on latest Foodics state
			staging.IsDeleted = isDeletedInFoodics;
			staging.DeletedAt = isDeletedInFoodics ? ParseFoodicsDate(product.DeletedAt) : null;
			staging.DeletionReason = isDeletedInFoodics ? "RemovedFromFoodics" : null;

			if (!isDeletedInFoodics)
			{
				// If the product became active again in Foodics, clear deletion sync flags
				staging.IsDeletionSyncedToTalabat = false;
				staging.DeletionSyncedAt = null;
				staging.DeletionSyncError = null;
			}
		}

	private static string? SerializeProductDetails(FoodicsProductDetailDto product)
	{
		try
		{
			var details = new
			{
				product.TaxGroup,
				product.Tags,
				product.Ingredients,
				product.Discounts,
				product.TimedEvents,
				product.Category
			};
			return JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = false });
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static string? SerializeToJson<T>(T? data)
	{
		if (data == null) return null;
		try
		{
			return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = false });
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static DateTime? ParseFoodicsDate(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		// Foodics dates are typically in "yyyy-MM-dd HH:mm:ss" format (server time)
		// We parse using invariant culture and treat them as UTC for consistency.
		if (DateTime.TryParse(
				value,
				CultureInfo.InvariantCulture,
				DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
				out var parsed))
		{
			return parsed;
		}

		return null;
	}
}

/// <summary>
/// Result of product sync operation
/// </summary>
public class FoodicsProductSyncResult
{
	public Guid FoodicsAccountId { get; set; }
	public string? BranchId { get; set; }
	public DateTime SyncDate { get; set; }
	public int TotalProcessed { get; set; }
	public int SavedCount { get; set; }
	public int UpdatedCount { get; set; }
	public int ErrorCount { get; set; }
	public int SuccessCount { get; set; }
}

