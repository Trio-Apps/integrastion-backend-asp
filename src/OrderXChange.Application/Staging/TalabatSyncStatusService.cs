using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Domain.Staging;
using OrderXChange.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace OrderXChange.Application.Staging;

/// <summary>
/// Service for managing Talabat catalog sync status.
/// Handles updates from webhooks and status tracking.
/// </summary>
public class TalabatSyncStatusService : ITalabatSyncStatusService, ITransientDependency
{
	private readonly IRepository<TalabatCatalogSyncLog, Guid> _syncLogRepository;
	private readonly IRepository<FoodicsProductStaging, Guid> _stagingRepository;
	private readonly IDbContextProvider<OrderXChangeDbContext> _dbContextProvider;
	private readonly ILogger<TalabatSyncStatusService> _logger;
	private readonly IDataFilter _dataFilter;

	public TalabatSyncStatusService(
		IRepository<TalabatCatalogSyncLog, Guid> syncLogRepository,
		IRepository<FoodicsProductStaging, Guid> stagingRepository,
		IDbContextProvider<OrderXChangeDbContext> dbContextProvider,
		ILogger<TalabatSyncStatusService> logger,
		IDataFilter dataFilter)
	{
		_syncLogRepository = syncLogRepository;
		_stagingRepository = stagingRepository;
		_dbContextProvider = dbContextProvider;
		_logger = logger;
		_dataFilter = dataFilter;
	}

	/// <summary>
	/// Records a new catalog submission to Talabat
	/// </summary>
	[UnitOfWork]
	public virtual async Task<TalabatCatalogSyncLog> RecordSubmissionAsync(
		Guid foodicsAccountId,
		string vendorCode,
		string? chainCode,
		string? importId,
		string? correlationId,
		int categoriesCount,
		int productsCount,
		string? callbackUrl,
		string apiVersion = "V1",
		CancellationToken cancellationToken = default)
	{
		var syncLog = new TalabatCatalogSyncLog
		{
			FoodicsAccountId = foodicsAccountId,
			VendorCode = vendorCode,
			ChainCode = chainCode,
			ImportId = importId,
			CorrelationId = correlationId,
			Status = TalabatSyncStatus.Submitted,
			ApiVersion = apiVersion,
			CategoriesCount = categoriesCount,
			ProductsCount = productsCount,
			CallbackUrl = callbackUrl,
			SubmittedAt = DateTime.UtcNow
		};

		await _syncLogRepository.InsertAsync(syncLog, autoSave: true, cancellationToken: cancellationToken);

		_logger.LogInformation(
			"Recorded Talabat catalog submission. SyncLogId={SyncLogId}, VendorCode={VendorCode}, ImportId={ImportId}",
			syncLog.Id,
			vendorCode,
			importId);

		// Update staging products status to Submitted
		await UpdateStagingProductsStatusAsync(
			foodicsAccountId,
			vendorCode,
			TalabatSyncStatus.Submitted,
			importId,
			null,
			cancellationToken);

		return syncLog;
	}

	/// <summary>
	/// Updates sync log and staging products when catalog import completes successfully
	/// </summary>
	[UnitOfWork]
	public virtual async Task HandleImportCompletedAsync(
		TalabatCatalogStatusWebhook webhook,
		string rawPayload,
		string correlationId,
		CancellationToken cancellationToken = default)
	{
		var syncLog = await FindSyncLogByImportIdAsync(webhook.ImportId, cancellationToken);
		
		if (syncLog != null)
		{
			syncLog.Status = TalabatSyncStatus.Done;
			syncLog.CompletedAt = DateTime.UtcNow;
			syncLog.CategoriesCreated = webhook.Summary?.CategoriesCreated ?? 0;
			syncLog.CategoriesUpdated = webhook.Summary?.CategoriesUpdated ?? 0;
			syncLog.ProductsCreated = webhook.Summary?.ProductsCreated ?? 0;
			syncLog.ProductsUpdated = webhook.Summary?.ProductsUpdated ?? 0;
			syncLog.WebhookPayloadJson = rawPayload;
			syncLog.DetailsJson = webhook.Details != null 
				? JsonSerializer.Serialize(webhook.Details)
				: null;
			
			if (syncLog.SubmittedAt != default)
			{
				syncLog.ProcessingDurationSeconds = (int)(DateTime.UtcNow - syncLog.SubmittedAt).TotalSeconds;
			}

			await _syncLogRepository.UpdateAsync(syncLog, autoSave: true, cancellationToken: cancellationToken);

			// Update staging products status
			await UpdateStagingProductsStatusAsync(
				syncLog.FoodicsAccountId,
				syncLog.VendorCode,
				TalabatSyncStatus.Success,
				webhook.ImportId,
				null,
				cancellationToken);

			_logger.LogInformation(
				"Updated sync log for completed import. SyncLogId={SyncLogId}, VendorCode={VendorCode}, ImportId={ImportId}",
				syncLog.Id,
				syncLog.VendorCode,
				webhook.ImportId);
		}
		else
		{
			// Create a new sync log if we don't have a matching record
			_logger.LogWarning(
				"No sync log found for ImportId={ImportId}. Creating new record from webhook.",
				webhook.ImportId);

			await CreateSyncLogFromWebhookAsync(webhook, rawPayload, TalabatSyncStatus.Done, cancellationToken);
		}
	}

	/// <summary>
	/// Updates sync log and staging products when catalog import fails
	/// </summary>
	[UnitOfWork]
	public virtual async Task HandleImportFailedAsync(
		TalabatCatalogStatusWebhook webhook,
		string rawPayload,
		string correlationId,
		CancellationToken cancellationToken = default)
	{
		var syncLog = await FindSyncLogByImportIdAsync(webhook.ImportId, cancellationToken);

		var errorsJson = webhook.Errors != null 
			? JsonSerializer.Serialize(webhook.Errors)
			: null;

		var errorMessage = webhook.Errors?.FirstOrDefault()?.Message ?? "Import failed";

		if (syncLog != null)
		{
			syncLog.Status = TalabatSyncStatus.Failed;
			syncLog.CompletedAt = DateTime.UtcNow;
			syncLog.ErrorsCount = webhook.Errors?.Count ?? 0;
			syncLog.ErrorsJson = errorsJson;
			syncLog.ResponseMessage = errorMessage;
			syncLog.WebhookPayloadJson = rawPayload;
			syncLog.DetailsJson = webhook.Details != null 
				? JsonSerializer.Serialize(webhook.Details)
				: null;

			if (syncLog.SubmittedAt != default)
			{
				syncLog.ProcessingDurationSeconds = (int)(DateTime.UtcNow - syncLog.SubmittedAt).TotalSeconds;
			}

			await _syncLogRepository.UpdateAsync(syncLog, autoSave: true, cancellationToken: cancellationToken);

			// Update staging products status
			await UpdateStagingProductsStatusAsync(
				syncLog.FoodicsAccountId,
				syncLog.VendorCode,
				TalabatSyncStatus.Failed,
				webhook.ImportId,
				errorMessage,
				cancellationToken);

			_logger.LogWarning(
				"Updated sync log for failed import. SyncLogId={SyncLogId}, VendorCode={VendorCode}, ImportId={ImportId}, ErrorCount={ErrorCount}",
				syncLog.Id,
				syncLog.VendorCode,
				webhook.ImportId,
				webhook.Errors?.Count ?? 0);
		}
		else
		{
			_logger.LogWarning(
				"No sync log found for ImportId={ImportId}. Creating new record from webhook.",
				webhook.ImportId);

			await CreateSyncLogFromWebhookAsync(webhook, rawPayload, TalabatSyncStatus.Failed, cancellationToken);
		}
	}

	/// <summary>
	/// Updates sync log and staging products when catalog import partially succeeds
	/// </summary>
	[UnitOfWork]
	public virtual async Task HandleImportPartialAsync(
		TalabatCatalogStatusWebhook webhook,
		string rawPayload,
		string correlationId,
		CancellationToken cancellationToken = default)
	{
		var syncLog = await FindSyncLogByImportIdAsync(webhook.ImportId, cancellationToken);

		var errorsJson = webhook.Errors != null
			? JsonSerializer.Serialize(webhook.Errors)
			: null;

		if (syncLog != null)
		{
			syncLog.Status = TalabatSyncStatus.Partial;
			syncLog.CompletedAt = DateTime.UtcNow;
			syncLog.CategoriesCreated = webhook.Summary?.CategoriesCreated ?? 0;
			syncLog.CategoriesUpdated = webhook.Summary?.CategoriesUpdated ?? 0;
			syncLog.ProductsCreated = webhook.Summary?.ProductsCreated ?? 0;
			syncLog.ProductsUpdated = webhook.Summary?.ProductsUpdated ?? 0;
			syncLog.ErrorsCount = webhook.Errors?.Count ?? 0;
			syncLog.ErrorsJson = errorsJson;
			syncLog.WebhookPayloadJson = rawPayload;
			syncLog.DetailsJson = webhook.Details != null 
				? JsonSerializer.Serialize(webhook.Details)
				: null;

			if (syncLog.SubmittedAt != default)
			{
				syncLog.ProcessingDurationSeconds = (int)(DateTime.UtcNow - syncLog.SubmittedAt).TotalSeconds;
			}

			await _syncLogRepository.UpdateAsync(syncLog, autoSave: true, cancellationToken: cancellationToken);

			// Update staging products status
			await UpdateStagingProductsStatusAsync(
				syncLog.FoodicsAccountId,
				syncLog.VendorCode,
				TalabatSyncStatus.Partial,
				webhook.ImportId,
				$"Partial success with {webhook.Errors?.Count ?? 0} errors",
				cancellationToken);

			_logger.LogWarning(
				"Updated sync log for partial import. SyncLogId={SyncLogId}, VendorCode={VendorCode}, ImportId={ImportId}",
				syncLog.Id,
				syncLog.VendorCode,
				webhook.ImportId);
		}
		else
		{
			_logger.LogWarning(
				"No sync log found for ImportId={ImportId}. Creating new record from webhook.",
				webhook.ImportId);

			await CreateSyncLogFromWebhookAsync(webhook, rawPayload, TalabatSyncStatus.Partial, cancellationToken);
		}
	}

	/// <summary>
	/// Gets sync history for an account
	/// </summary>
	public async Task<List<TalabatCatalogSyncLog>> GetSyncHistoryAsync(
		Guid foodicsAccountId,
		int maxRecords = 50,
		CancellationToken cancellationToken = default)
	{
		var dbContext = await _dbContextProvider.GetDbContextAsync();
		
		return await dbContext.Set<TalabatCatalogSyncLog>()
			.Where(x => x.FoodicsAccountId == foodicsAccountId)
			.OrderByDescending(x => x.SubmittedAt)
			.Take(maxRecords)
			.ToListAsync(cancellationToken);
	}

	/// <summary>
	/// Gets the latest sync status for a vendor
	/// </summary>
	public async Task<TalabatCatalogSyncLog?> GetLatestSyncStatusAsync(
		string vendorCode,
		CancellationToken cancellationToken = default)
	{
		var dbContext = await _dbContextProvider.GetDbContextAsync();

		return await dbContext.Set<TalabatCatalogSyncLog>()
			.Where(x => x.VendorCode == vendorCode)
			.OrderByDescending(x => x.SubmittedAt)
			.FirstOrDefaultAsync(cancellationToken);
	}

	#region Private Methods

	private async Task<TalabatCatalogSyncLog?> FindSyncLogByImportIdAsync(
		string? importId,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(importId))
		{
			_logger.LogWarning("FindSyncLogByImportIdAsync called with null/empty ImportId");
			return null;
		}

		_logger.LogInformation("🔍 Searching for SyncLog with ImportId={ImportId}", importId);

		// Disable multi-tenancy filter since webhooks are received without tenant context
		using (_dataFilter.Disable<IMultiTenant>())
		{
			var queryable = await _syncLogRepository.GetQueryableAsync();
			
			// Try exact match first
			var syncLog = await queryable
				.FirstOrDefaultAsync(x => x.ImportId == importId, cancellationToken);

			if (syncLog != null)
			{
				_logger.LogInformation(
					"✅ Found SyncLog by ImportId. Id={Id}, VendorCode={VendorCode}, Status={Status}, TenantId={TenantId}",
					syncLog.Id, syncLog.VendorCode, syncLog.Status, syncLog.TenantId);
				return syncLog;
			}

			// Try case-insensitive match
			syncLog = await queryable
				.FirstOrDefaultAsync(x => x.ImportId != null && x.ImportId.ToLower() == importId.ToLower(), cancellationToken);

			if (syncLog != null)
			{
				_logger.LogInformation(
					"✅ Found SyncLog by ImportId (case-insensitive). Id={Id}, VendorCode={VendorCode}, Status={Status}",
					syncLog.Id, syncLog.VendorCode, syncLog.Status);
				return syncLog;
			}

			// Log available ImportIds for debugging
			var recentImportIds = await queryable
				.OrderByDescending(x => x.SubmittedAt)
				.Take(5)
				.Select(x => new { x.ImportId, x.Status, x.SubmittedAt })
				.ToListAsync(cancellationToken);

			_logger.LogWarning(
				"❌ No SyncLog found for ImportId={ImportId}. Recent ImportIds: {RecentIds}",
				importId,
				string.Join(", ", recentImportIds.Select(x => $"{x.ImportId}({x.Status})")));

			return null;
		}
	}

	private async Task CreateSyncLogFromWebhookAsync(
		TalabatCatalogStatusWebhook webhook,
		string rawPayload,
		string status,
		CancellationToken cancellationToken)
	{
		// Try to find FoodicsAccountId from staging products using the ImportId
		Guid? foodicsAccountId = null;
		
		try
		{
			var dbContext = await _dbContextProvider.GetDbContextAsync();
			var stagingProduct = await dbContext.Set<FoodicsProductStaging>()
				.Where(x => x.TalabatImportId == webhook.ImportId)
				.FirstOrDefaultAsync(cancellationToken);
			
			if (stagingProduct != null)
			{
				foodicsAccountId = stagingProduct.FoodicsAccountId;
				_logger.LogDebug(
					"Found FoodicsAccountId={AccountId} from staging products for ImportId={ImportId}",
					foodicsAccountId,
					webhook.ImportId);
			}
			else
			{
				_logger.LogWarning(
					"No staging products found with ImportId={ImportId}. Cannot determine FoodicsAccountId.",
					webhook.ImportId);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error finding FoodicsAccountId for ImportId={ImportId}", webhook.ImportId);
		}

		// If we still don't have FoodicsAccountId, skip creating the sync log to avoid foreign key constraint
		if (!foodicsAccountId.HasValue)
		{
			_logger.LogWarning(
				"Skipping sync log creation for ImportId={ImportId} - FoodicsAccountId not found. " +
				"The sync log should have been created during submission.",
				webhook.ImportId);
			return;
		}

	var errorsJson = webhook.Errors != null
		? JsonSerializer.Serialize(webhook.Errors)
		: null;

	var detailsJson = webhook.Details != null
		? JsonSerializer.Serialize(webhook.Details)
		: null;

	var syncLog = new TalabatCatalogSyncLog
	{
		FoodicsAccountId = foodicsAccountId.Value,
		VendorCode = webhook.VendorCode ?? "unknown",
		ImportId = webhook.ImportId,
		Status = status,
		CategoriesCreated = webhook.Summary?.CategoriesCreated ?? 0,
		CategoriesUpdated = webhook.Summary?.CategoriesUpdated ?? 0,
		ProductsCreated = webhook.Summary?.ProductsCreated ?? 0,
		ProductsUpdated = webhook.Summary?.ProductsUpdated ?? 0,
		ErrorsCount = webhook.Errors?.Count ?? 0,
		ErrorsJson = errorsJson,
		DetailsJson = detailsJson,
		WebhookPayloadJson = rawPayload,
		SubmittedAt = DateTime.UtcNow.AddSeconds(-30), // Approximate - assume 30 seconds ago
		CompletedAt = DateTime.UtcNow
	};

		await _syncLogRepository.InsertAsync(syncLog, autoSave: true, cancellationToken: cancellationToken);

		_logger.LogInformation(
			"Created sync log from webhook. SyncLogId={SyncLogId}, FoodicsAccountId={AccountId}, VendorCode={VendorCode}, ImportId={ImportId}, Status={Status}",
			syncLog.Id,
			foodicsAccountId,
			webhook.VendorCode,
			webhook.ImportId,
			status);
	}

	private async Task UpdateStagingProductsStatusAsync(
		Guid foodicsAccountId,
		string vendorCode,
		string status,
		string? importId,
		string? errorMessage,
		CancellationToken cancellationToken)
	{
		if (foodicsAccountId == Guid.Empty)
		{
			_logger.LogWarning("Cannot update staging products - FoodicsAccountId is empty");
			return;
		}

		var dbContext = await _dbContextProvider.GetDbContextAsync();
		var now = DateTime.UtcNow;

		int affectedRows;
		
		if (status == TalabatSyncStatus.Submitted)
		{
			// When submitting - set TalabatSubmittedAt and clear completion
			affectedRows = await dbContext.Set<FoodicsProductStaging>()
				.Where(x => x.FoodicsAccountId == foodicsAccountId)
				.ExecuteUpdateAsync(setters => setters
					.SetProperty(x => x.TalabatSyncStatus, status)
					.SetProperty(x => x.TalabatImportId, importId)
					.SetProperty(x => x.TalabatVendorCode, vendorCode)
					.SetProperty(x => x.TalabatSubmittedAt, now)
					.SetProperty(x => x.TalabatSyncCompletedAt, (DateTime?)null)
					.SetProperty(x => x.TalabatLastError, (string?)null),
					cancellationToken);
		}
		else
		{
			// When completing (Success/Done/Failed/Partial) - set completion date and update SyncDate for successful syncs
			var isSuccessful = status == TalabatSyncStatus.Success || status == TalabatSyncStatus.Done;
			
			if (isSuccessful)
			{
				affectedRows = await dbContext.Set<FoodicsProductStaging>()
					.Where(x => x.FoodicsAccountId == foodicsAccountId)
					.ExecuteUpdateAsync(setters => setters
						.SetProperty(x => x.TalabatSyncStatus, status)
						.SetProperty(x => x.TalabatSyncCompletedAt, now)
						.SetProperty(x => x.SyncDate, now)
						.SetProperty(x => x.TalabatLastError, (string?)null),
						cancellationToken);
			}
			else
			{
				affectedRows = await dbContext.Set<FoodicsProductStaging>()
					.Where(x => x.FoodicsAccountId == foodicsAccountId)
					.ExecuteUpdateAsync(setters => setters
						.SetProperty(x => x.TalabatSyncStatus, status)
						.SetProperty(x => x.TalabatSyncCompletedAt, now)
						.SetProperty(x => x.TalabatLastError, errorMessage),
						cancellationToken);
			}
		}

		_logger.LogInformation(
			"Updated {Count} staging products to status {Status}. FoodicsAccountId={AccountId}, VendorCode={VendorCode}",
			affectedRows,
			status,
			foodicsAccountId,
			vendorCode);
	}

	#endregion
}

/// <summary>
/// Constants for Talabat sync status
/// </summary>
public static class TalabatSyncStatus
{
	public const string Pending = "Pending";
	public const string Submitted = "Submitted";
	public const string Processing = "Processing";
	public const string Done = "Done";
	public const string Success = "Success";
	public const string Failed = "Failed";
	public const string Partial = "Partial";
}

