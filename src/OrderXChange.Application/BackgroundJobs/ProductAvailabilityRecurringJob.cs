using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace OrderXChange.BackgroundJobs;

/// <summary>
/// Recurring job to sync product availability and pricing from Foodics to prepare for Talabat integration
/// </summary>
public class ProductAvailabilityRecurringJob : ITransientDependency
{
    private readonly IProductAvailabilityAppService _productAvailabilityAppService;
    private readonly ILogger<ProductAvailabilityRecurringJob> _logger;
    // private readonly IRepository<JobExecution, Guid> _jobExecutionRepository;
    private readonly ICurrentTenant _currentTenant;

    public ProductAvailabilityRecurringJob(
        IProductAvailabilityAppService productAvailabilityAppService,
        ILogger<ProductAvailabilityRecurringJob> logger,
        // IRepository<JobExecution, Guid> jobExecutionRepository,
        ICurrentTenant currentTenant)
    {
        _productAvailabilityAppService = productAvailabilityAppService;
        _logger = logger;
        // _jobExecutionRepository = jobExecutionRepository;
        _currentTenant = currentTenant;
    }

    [UnitOfWork]
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteForTenantAsync(
        Guid tenantId,
        string? branchId = null,
        PerformContext? context = null,
        CancellationToken cancellationToken = default)
    {
        using (_currentTenant.Change(tenantId))
        {
            // var recurringJobId = context?.GetJobParameter<string>("RecurringJobId");

            // var jobExecution = new JobExecution
            // {
            //     TenantId = tenantId,
            //     JobName = nameof(ProductAvailabilityRecurringJob) + "." + nameof(ExecuteForTenantAsync),
            //     HangfireJobId = context?.BackgroundJob?.Id,
            //     State = "Processing",
            //     StartedAt = DateTime.UtcNow,
            //     BranchId = branchId,
            //     IntegrationName = "FoodicsProductAvailability",
            //     Arguments = string.IsNullOrWhiteSpace(branchId) ? "branchId=<all>" : $"branchId={branchId}",
            //     Summary = recurringJobId != null ? $"RecurringJobId={recurringJobId}" : null
            // };

            // await _jobExecutionRepository.InsertAsync(jobExecution, autoSave: true, cancellationToken);

            _logger.LogInformation(
                "Product availability sync job started for tenant {TenantId}, branch {BranchId}",
                tenantId,
                branchId ?? "<all>");

            try
            {
                // Fetch and prepare product availability data
                var result = await _productAvailabilityAppService.FetchAndPrepareAsync(page: 1, perPage: 100);

                // jobExecution.State = "Succeeded";
                // jobExecution.CompletedAt = DateTime.UtcNow;
                // jobExecution.Summary =
                //     $"Fetched {result.TotalProducts} products, {result.TotalBranches} branches. " +
                //     $"Available: {result.AvailableProducts}, Unavailable: {result.UnavailableProducts}. " +
                //     $"Prepared {result.Products.Count} product-branch combinations for Talabat.";

                // await _jobExecutionRepository.UpdateAsync(jobExecution, autoSave: true, cancellationToken);

                _logger.LogInformation(
                    "Product availability sync job completed successfully for tenant {TenantId}, branch {BranchId}. " +
                    "Products: {TotalProducts}, Branches: {TotalBranches}, Available: {AvailableProducts}, Unavailable: {UnavailableProducts}",
                    tenantId,
                    branchId ?? "<all>",
                    result.TotalProducts,
                    result.TotalBranches,
                    result.AvailableProducts,
                    result.UnavailableProducts);

                // TODO: Push the prepared data to Talabat integration service
                // This is where you would call the Talabat API to update item/choice availability
                // Example: await _talabatIntegrationService.UpdateProductAvailabilityAsync(result.Products, cancellationToken);
            }
            catch (Exception ex)
            {
                // jobExecution.State = "Failed";
                // jobExecution.CompletedAt = DateTime.UtcNow;
                // jobExecution.ExceptionMessage = ex.Message;

                // await _jobExecutionRepository.UpdateAsync(jobExecution, autoSave: true, cancellationToken);

                _logger.LogError(
                    ex,
                    "Product availability sync job failed for tenant {TenantId}, branch {BranchId}",
                    tenantId,
                    branchId ?? "<all>");
                throw;
            }
        }
    }
}


