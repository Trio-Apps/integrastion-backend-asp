using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Talabat;
using OrderXChange.Domain.Staging;
using OrderXChange.EntityFrameworkCore;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.Talabat;

namespace OrderXChange.Talabat;

/// <summary>
/// UPDATED: Now uses TalabatAccountService for multi-tenant platform configuration
/// </summary>
public class TalabatDashboardAppService : ApplicationService, ITalabatDashboardAppService
{
    private readonly IRepository<TalabatCatalogSyncLog, Guid> _syncLogRepository;
    private readonly IRepository<FoodicsProductStaging, Guid> _stagingRepository;
    private readonly TalabatCatalogClient _talabatCatalogClient;
    private readonly TalabatAccountService _talabatAccountService;
    private readonly IDbContextProvider<OrderXChangeDbContext> _dbContextProvider;
    private readonly IConfiguration _configuration;
    private readonly IDataFilter _dataFilter;
    private readonly ITenantRepository _tenantRepository;

    public TalabatDashboardAppService(
        IRepository<TalabatCatalogSyncLog, Guid> syncLogRepository,
        IRepository<FoodicsProductStaging, Guid> stagingRepository,
        TalabatCatalogClient talabatCatalogClient,
        TalabatAccountService talabatAccountService,
        IDbContextProvider<OrderXChangeDbContext> dbContextProvider,
        IConfiguration configuration,
        IDataFilter dataFilter,
        ITenantRepository tenantRepository)
    {
        _syncLogRepository = syncLogRepository;
        _stagingRepository = stagingRepository;
        _talabatCatalogClient = talabatCatalogClient;
        _talabatAccountService = talabatAccountService;
        _dbContextProvider = dbContextProvider;
        _configuration = configuration;
        _dataFilter = dataFilter;
        _tenantRepository = tenantRepository;
    }

    public async Task<List<TalabatVendorLookupDto>> GetVendorsAsync()
    {
        var isHost = !CurrentTenant.IsAvailable;

        // Host: show all active accounts across tenants.
        if (isHost)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var dbContext = await _dbContextProvider.GetDbContextAsync();

                var vendors = await dbContext.Set<TalabatAccount>()
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Name)
                    .Select(x => new TalabatVendorLookupDto
                    {
                        VendorCode = x.VendorCode ?? string.Empty,
                        Name = x.Name ?? string.Empty,
                        PlatformRestaurantId = x.PlatformRestaurantId
                    })
                    .ToListAsync();

                return vendors
                    .Where(x => !string.IsNullOrWhiteSpace(x.VendorCode))
                    .ToList();
            }
        }

        // Tenant: show only current tenant accounts.
        var accounts = await _talabatAccountService.GetCurrentTenantAccountsAsync();
        return accounts
            .Where(x => !string.IsNullOrWhiteSpace(x.VendorCode))
            .OrderBy(x => x.Name)
            .Select(x => new TalabatVendorLookupDto
            {
                VendorCode = x.VendorCode!,
                Name = x.Name ?? x.VendorCode!,
                PlatformRestaurantId = x.PlatformRestaurantId
            })
            .ToList();
    }

    /// <summary>
    /// Gets dashboard data - ALWAYS shows all data across all tenants
    /// This is required because sync logs are created with TenantId=NULL (host data)
    /// </summary>
    public async Task<TalabatDashboardDto> GetDashboardAsync(string? vendorCode = null)
    {
        var isHost = !CurrentTenant.IsAvailable;
        
        Logger.LogInformation(
            "📊 GetDashboardAsync called. VendorCode={VendorCode}, TenantId={TenantId}, IsHost={IsHost}",
            vendorCode ?? "(none)",
            CurrentTenant.Id,
            isHost);

        // ALWAYS disable multi-tenancy filter because all Talabat data is stored with TenantId=NULL
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await GetDashboardInternalAsync(vendorCode, isHost);
        }
    }

    private async Task<TalabatDashboardDto> GetDashboardInternalAsync(string? vendorCode, bool isHost)
    {
        var syncLogsQueryable = await _syncLogRepository.GetQueryableAsync();
        var stagingQueryable = await _stagingRepository.GetQueryableAsync();

        Logger.LogInformation(
            "📊 GetDashboardInternalAsync: IsHost={IsHost}, VendorCode={VendorCode}",
            isHost,
            vendorCode ?? "(ALL)");

        // Filter by vendorCode ONLY if provided
        if (!string.IsNullOrWhiteSpace(vendorCode))
        {
            var vendorCodeLower = vendorCode.ToLower();
            syncLogsQueryable = syncLogsQueryable.Where(x => 
                x.VendorCode != null && x.VendorCode.ToLower() == vendorCodeLower);
        }

        // Get total count for ALL matching records
        var totalCount = await syncLogsQueryable.CountAsync();

        Logger.LogInformation(
            "📊 Total sync logs count: {Count}",
            totalCount);

        // Get recent sync logs (top 20)
        var syncLogs = await syncLogsQueryable
            .OrderByDescending(x => x.SubmittedAt)
            .Take(20)
            .ToListAsync();

        Logger.LogInformation(
            "📊 SyncLogs: Found {Count} records (total: {Total}) for VendorCode={VendorCode}, IsHost={IsHost}",
            syncLogs.Count,
            totalCount,
            vendorCode ?? "(ALL)",
            isHost);

        if (!syncLogs.Any() && isHost)
        {
            // Debug: Check if there's any data at all
            var dbContext = await _dbContextProvider.GetDbContextAsync();
            var rawCount = await dbContext.Set<TalabatCatalogSyncLog>().CountAsync();
            Logger.LogWarning(
                "📊 No sync logs found. Raw DB count (bypassing all filters): {Count}",
                rawCount);
        }

        // Get staging stats
        var allStaging = await stagingQueryable.ToListAsync();

        Logger.LogInformation(
            "📊 Staging products count: {Count}",
            allStaging.Count);

        // Calculate counts from ALL matching records
        var successfulSubmissions = await syncLogsQueryable.CountAsync(x => 
            x.Status == "Done" || x.Status == "Success" || x.Status == "done" || x.Status == "success");
        var failedSubmissions = await syncLogsQueryable.CountAsync(x => 
            x.Status == "Failed" || x.Status == "failed");
        var pendingSubmissions = await syncLogsQueryable.CountAsync(x => 
            x.Status == "Submitted" || x.Status == "Processing" || 
            x.Status == "submitted" || x.Status == "processing" || 
            x.Status == "in_progress");

        var stagingStats = new TalabatStagingStatsDto
        {
            TotalProducts = allStaging.Count,
            ActiveProducts = allStaging.Count(x => x.IsActive),
            InactiveProducts = allStaging.Count(x => !x.IsActive),
            SubmittedProducts = allStaging.Count(x => x.TalabatSubmittedAt != null),
            NotSubmittedProducts = allStaging.Count(x => x.TalabatSubmittedAt == null),
            CompletedProducts = allStaging.Count(x => x.TalabatSyncCompletedAt != null),
            FailedProducts = allStaging.Count(x => !string.IsNullOrWhiteSpace(x.TalabatLastError)),
            LastSyncDate = allStaging.Any() ? allStaging.Max(x => x.SyncDate) : null,
            LastSubmittedAt = allStaging.Where(x => x.TalabatSubmittedAt != null).Select(x => x.TalabatSubmittedAt).DefaultIfEmpty().Max(),
            LastSyncStatus = allStaging.OrderByDescending(x => x.TalabatSubmittedAt).FirstOrDefault()?.TalabatSyncStatus
        };

        // For Host, use empty vendorCode for branch status (or skip it)
        var branchStatus = new TalabatBranchStatusDto
        {
            VendorCode = vendorCode ?? "N/A",
            IsAvailable = true,
            Status = isHost ? "Host Mode" : "Unknown"
        };

        if (!string.IsNullOrWhiteSpace(vendorCode))
        {
            branchStatus = await GetBranchStatusInternalAsync(vendorCode);
        }

        return new TalabatDashboardDto
        {
            Counts = new TalabatSyncCountsDto
            {
                TotalSubmissions = totalCount,
                SuccessfulSubmissions = successfulSubmissions,
                FailedSubmissions = failedSubmissions,
                PendingSubmissions = pendingSubmissions,
                TotalProducts = stagingStats.TotalProducts,
                ActiveProducts = stagingStats.ActiveProducts,
                SyncedProducts = stagingStats.CompletedProducts
            },
            RecentSubmissions = await MapToDtosWithTenantsAsync(syncLogs),
            BranchStatus = branchStatus,
            StagingStats = stagingStats
        };
    }

    /// <summary>
    /// Gets paginated sync logs - ALWAYS shows all data across all tenants
    /// </summary>
    public async Task<PagedResultDto<TalabatSyncLogItemDto>> GetSyncLogsAsync(GetSyncLogsInput input)
    {
        var isHost = !CurrentTenant.IsAvailable;
        
        Logger.LogInformation(
            "📊 GetSyncLogsAsync called. VendorCode={VendorCode}, Skip={Skip}, Take={Take}, IsHost={IsHost}",
            input.VendorCode ?? "(ALL)",
            input.SkipCount,
            input.MaxResultCount,
            isHost);

        // ALWAYS disable multi-tenancy filter
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await GetSyncLogsInternalAsync(input, isHost);
        }
    }

    private async Task<PagedResultDto<TalabatSyncLogItemDto>> GetSyncLogsInternalAsync(GetSyncLogsInput input, bool isHost)
    {
        var queryable = await _syncLogRepository.GetQueryableAsync();

        // Apply vendorCode filter only if provided
        if (!string.IsNullOrWhiteSpace(input.VendorCode))
        {
            var vendorCodeLower = input.VendorCode.ToLower();
            queryable = queryable.Where(x => 
                x.VendorCode != null && x.VendorCode.ToLower() == vendorCodeLower);
        }

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            var statusLower = input.Status.ToLower();
            queryable = queryable.Where(x => 
                x.Status != null && x.Status.ToLower() == statusLower);
        }

        if (input.FromDate.HasValue)
        {
            queryable = queryable.Where(x => x.SubmittedAt >= input.FromDate.Value);
        }

        if (input.ToDate.HasValue)
        {
            queryable = queryable.Where(x => x.SubmittedAt <= input.ToDate.Value);
        }

        // Get total count
        var totalCount = await queryable.CountAsync();

        // Apply sorting
        if (!string.IsNullOrWhiteSpace(input.Sorting))
        {
            queryable = input.Sorting.ToLower() switch
            {
                "submittedat asc" => queryable.OrderBy(x => x.SubmittedAt),
                "submittedat desc" => queryable.OrderByDescending(x => x.SubmittedAt),
                "status asc" => queryable.OrderBy(x => x.Status),
                "status desc" => queryable.OrderByDescending(x => x.Status),
                "vendorcode asc" => queryable.OrderBy(x => x.VendorCode),
                "vendorcode desc" => queryable.OrderByDescending(x => x.VendorCode),
                _ => queryable.OrderByDescending(x => x.SubmittedAt)
            };
        }
        else
        {
            queryable = queryable.OrderByDescending(x => x.SubmittedAt);
        }

        // Apply pagination
        var items = await queryable
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        Logger.LogInformation(
            "📊 GetSyncLogsAsync returning {Count} items (total: {Total}), IsHost={IsHost}",
            items.Count,
            totalCount,
            isHost);

        return new PagedResultDto<TalabatSyncLogItemDto>(
            totalCount,
            await MapToDtosWithTenantsAsync(items)
        );
    }

    private async Task<List<TalabatSyncLogItemDto>> MapToDtosWithTenantsAsync(List<TalabatCatalogSyncLog> logs)
    {
        if (!logs.Any())
        {
            return new List<TalabatSyncLogItemDto>();
        }

        // Get unique tenant IDs
        var tenantIds = logs
            .Where(x => x.TenantId.HasValue)
            .Select(x => x.TenantId!.Value)
            .Distinct()
            .ToList();

        // Load tenant names (disable multi-tenancy filter to load all tenants)
        Dictionary<Guid, string> tenantNames = new();
        
        if (tenantIds.Any())
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var tenants = await _tenantRepository.GetListAsync(includeDetails: false);
                tenantNames = tenants
                    .Where(t => tenantIds.Contains(t.Id))
                    .ToDictionary(t => t.Id, t => t.Name);
            }
        }

        // Map logs to DTOs with tenant information
        return logs.Select(log => new TalabatSyncLogItemDto
        {
            Id = log.Id,
            VendorCode = log.VendorCode,
            ChainCode = log.ChainCode,
            ImportId = log.ImportId,
            Status = log.Status,
            SubmittedAt = log.SubmittedAt,
            CompletedAt = log.CompletedAt,
            CategoriesCount = log.CategoriesCount,
            ProductsCount = log.ProductsCount,
            ProductsCreated = log.ProductsCreated,
            ProductsUpdated = log.ProductsUpdated,
            ErrorsCount = log.ErrorsCount,
            ApiVersion = log.ApiVersion,
            ProcessingDurationSeconds = log.ProcessingDurationSeconds,
            TenantId = log.TenantId,
            TenantName = log.TenantId.HasValue && tenantNames.ContainsKey(log.TenantId.Value)
                ? tenantNames[log.TenantId.Value]
                : (log.TenantId.HasValue ? "Unknown Tenant" : "Host")
        }).ToList();
    }

    public async Task<TalabatBranchStatusDto> GetBranchStatusAsync(string vendorCode)
    {
        return await GetBranchStatusInternalAsync(vendorCode);
    }

    public async Task<TalabatBranchStatusDto> SetBranchBusyAsync(string vendorCode, string? reason = null, int? availableInMinutes = null)
    {
        Logger.LogInformation("Setting branch {VendorCode} to BUSY. Reason={Reason}, AvailableIn={Minutes}min",
            vendorCode, reason ?? "Not specified", availableInMinutes ?? 0);

        // Hardcoded for single-branch testing. Remove when enabling multi-branch input.
        var hardcodedVendorCode = "PH-SIDDIQ-002";
        vendorCode = hardcodedVendorCode;
        // Talabat expects specific closedReason values; default to TOO_BUSY_NO_DRIVERS
        var closedReason = "TOO_BUSY_NO_DRIVERS";
        reason ??= closedReason;
        availableInMinutes ??= 30;

        DateTime? availableAt = availableInMinutes.HasValue
            ? DateTime.UtcNow.AddMinutes(availableInMinutes.Value)
            : null;

        // Get configuration for V2 API
        var chainCode = _configuration["Talabat:ChainCode"] ?? "tlbt-pick";
        var useV2Api = _configuration.GetValue<bool>("Talabat:UseV2AvailabilityApi", true);

        if (useV2Api)
        {
            // Use V2 API endpoint
            // UPDATED: Now uses TalabatAccountService to get platform config from database
            var platformKey = await GetPlatformKeyForVendorAsync(vendorCode);
            var platformRestaurantId = await GetPlatformRestaurantIdForVendorAsync(vendorCode);
            
            // OLD CODE (commented for reference):
            // var platformKey = "TB"; // GetPlatformKeyForVendor(vendorCode);
            // var platformRestaurantId = "783216"; // GetPlatformRestaurantIdForVendor(vendorCode);

            var availabilityState = availableInMinutes.HasValue 
                ? TalabatAvailabilityState.ClosedUntil 
                : TalabatAvailabilityState.Closed;

            var requestV2 = new TalabatUpdateVendorAvailabilityV2Request
            {
                AvailabilityState = availabilityState,
                PlatformKey = platformKey,
                PlatformRestaurantId = platformRestaurantId,
                ClosingMinutes = availabilityState == TalabatAvailabilityState.ClosedUntil && availableInMinutes.HasValue
                    ? availableInMinutes.Value
                    : 30,
                ClosedReason = closedReason
            };

            Logger.LogInformation(
                "Using V2 API: ChainCode={ChainCode}, VendorId={VendorId}, State={State}, PlatformKey={PlatformKey}, PlatformRestaurantId={RestaurantId}",
                chainCode, vendorCode, availabilityState, platformKey, platformRestaurantId);

            var result = await _talabatCatalogClient.UpdateVendorAvailabilityV2Async(
                chainCode,
                vendorCode,
                requestV2);

            return new TalabatBranchStatusDto
            {
                VendorCode = vendorCode,
                IsAvailable = false,
                Status = "Busy",
                Reason = reason,
                AvailableAt = availableAt,
                LastUpdated = DateTime.UtcNow
            };
        }
        else
        {
            // Use V1 API endpoint (legacy)
            var request = new TalabatUpdateVendorAvailabilityRequest
            {
                IsAvailable = false,
                Reason = reason ?? "Temporarily busy",
                AvailableAt = availableAt
            };

            var result = await _talabatCatalogClient.UpdateVendorAvailabilityAsync(vendorCode, request);

            return new TalabatBranchStatusDto
            {
                VendorCode = vendorCode,
                IsAvailable = false,
                Status = "Busy",
                Reason = reason,
                AvailableAt = availableAt,
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    public async Task<TalabatBranchStatusDto> SetBranchAvailableAsync(string vendorCode)
    {
        Logger.LogInformation("Setting branch {VendorCode} to AVAILABLE", vendorCode);

        // OLD CODE (commented for reference - hardcoded vendor):
        // var hardcodedVendorCode = "PH-SIDDIQ-002";
        // vendorCode = hardcodedVendorCode;

        // Get configuration for V2 API
        var chainCode = _configuration["Talabat:ChainCode"] ?? "tlbt-pick";
        var useV2Api = _configuration.GetValue<bool>("Talabat:UseV2AvailabilityApi", true);

        if (useV2Api)
        {
            // Use V2 API endpoint
            // UPDATED: Now uses TalabatAccountService to get platform config from database
            var platformKey = await GetPlatformKeyForVendorAsync(vendorCode);
            var platformRestaurantId = await GetPlatformRestaurantIdForVendorAsync(vendorCode);
            
            // OLD CODE (commented for reference):
            // var platformKey = "TB"; // GetPlatformKeyForVendor(vendorCode);
            // var platformRestaurantId = "783216"; // GetPlatformRestaurantIdForVendor(vendorCode);

            var requestV2 = new TalabatUpdateVendorAvailabilityV2Request
            {
                AvailabilityState = TalabatAvailabilityState.Open,
                PlatformKey = platformKey,
                PlatformRestaurantId = platformRestaurantId,
                ClosingMinutes = null,
                ClosedReason = null
            };

            Logger.LogInformation(
                "Using V2 API: ChainCode={ChainCode}, VendorId={VendorId}, State={State}, PlatformKey={PlatformKey}, PlatformRestaurantId={RestaurantId}",
                chainCode, vendorCode, TalabatAvailabilityState.Open, platformKey, platformRestaurantId);

            var result = await _talabatCatalogClient.UpdateVendorAvailabilityV2Async(
                chainCode,
                vendorCode,
                requestV2);

            return new TalabatBranchStatusDto
            {
                VendorCode = vendorCode,
                IsAvailable = true,
                Status = "Available",
                LastUpdated = DateTime.UtcNow
            };
        }
        else
        {
            // Use V1 API endpoint (legacy)
            var request = new TalabatUpdateVendorAvailabilityRequest
            {
                IsAvailable = true,
                Reason = null,
                AvailableAt = null
            };

            var result = await _talabatCatalogClient.UpdateVendorAvailabilityAsync(vendorCode, request);

            return new TalabatBranchStatusDto
            {
                VendorCode = vendorCode,
                IsAvailable = true,
                Status = "Available",
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Gets platform key for vendor from TalabatAccount or configuration
    /// UPDATED: Now uses TalabatAccountService to check database first
    /// </summary>
    private async Task<string> GetPlatformKeyForVendorAsync(string vendorCode)
    {
        // Try to get from TalabatAccount entity first
        var platformConfig = await _talabatAccountService.GetPlatformConfigAsync(vendorCode);
        if (platformConfig != null && !string.IsNullOrWhiteSpace(platformConfig.PlatformKey))
        {
            return platformConfig.PlatformKey;
        }

        // OLD CODE (fallback to configuration):
        // Try to get vendor-specific config first
        var vendorPlatformKey = _configuration[$"Talabat:VendorConfig:{vendorCode}:PlatformKey"];
        if (!string.IsNullOrWhiteSpace(vendorPlatformKey))
        {
            return vendorPlatformKey;
        }

        // Fall back to default
        return _configuration["Talabat:PlatformKey"] ?? "TB";
    }

    /// <summary>
    /// Gets platform restaurant ID for vendor from TalabatAccount or configuration
    /// UPDATED: Now uses TalabatAccountService to check database first
    /// </summary>
    private async Task<string> GetPlatformRestaurantIdForVendorAsync(string vendorCode)
    {
        // Try to get from TalabatAccount entity first
        var platformConfig = await _talabatAccountService.GetPlatformConfigAsync(vendorCode);
        if (platformConfig != null && !string.IsNullOrWhiteSpace(platformConfig.PlatformRestaurantId))
        {
            return platformConfig.PlatformRestaurantId;
        }

        // OLD CODE (fallback to configuration):
        // Try to get vendor-specific config first
        var vendorRestaurantId = _configuration[$"Talabat:VendorConfig:{vendorCode}:PlatformRestaurantId"];
        if (!string.IsNullOrWhiteSpace(vendorRestaurantId))
        {
            return vendorRestaurantId;
        }

        // Fall back to default
        return _configuration["Talabat:PlatformRestaurantId"] 
            ?? throw new InvalidOperationException($"PlatformRestaurantId not configured for vendor {vendorCode}");
    }

    private async Task<TalabatBranchStatusDto> GetBranchStatusInternalAsync(string vendorCode)
    {
        try
        {
            var result = await _talabatCatalogClient.GetVendorAvailabilityAsync(vendorCode);

            if (result != null)
            {
                return new TalabatBranchStatusDto
                {
                    VendorCode = result.VendorCode,
                    IsAvailable = result.IsAvailable,
                    Status = result.IsAvailable ? "Available" : "Busy",
                    Reason = result.Reason,
                    AvailableAt = result.AvailableAt,
                    LastUpdated = result.LastUpdated
                };
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not get branch status for {VendorCode}", vendorCode);
        }

        return new TalabatBranchStatusDto
        {
            VendorCode = vendorCode,
            IsAvailable = true,
            Status = "Unknown"
        };
    }
}
