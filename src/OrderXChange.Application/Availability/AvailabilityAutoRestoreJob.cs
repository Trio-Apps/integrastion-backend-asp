using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace OrderXChange.Availability;

/// <summary>
/// Recurring job that restores "for a day" out-of-stock items back to in stock once their
/// <see cref="ItemAvailabilityState.RestoreAtUtc"/> has passed. Runs across all tenants.
/// </summary>
public class AvailabilityAutoRestoreJob : ITransientDependency
{
    private readonly IRepository<ItemAvailabilityState, Guid> _repository;
    private readonly IClock _clock;
    private readonly IDataFilter _dataFilter;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<AvailabilityAutoRestoreJob> _logger;

    public AvailabilityAutoRestoreJob(
        IRepository<ItemAvailabilityState, Guid> repository,
        IClock clock,
        IDataFilter dataFilter,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<AvailabilityAutoRestoreJob> logger)
    {
        _repository = repository;
        _clock = clock;
        _dataFilter = dataFilter;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    public async Task RestoreDueItemsAsync()
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true);
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var now = _clock.Now;
            var due = await _repository.GetListAsync(x =>
                !x.IsInStock &&
                x.Mode == AvailabilityMode.ForADay &&
                x.RestoreAtUtc != null &&
                x.RestoreAtUtc <= now);

            if (due.Count > 0)
            {
                // Removing the override restores the item to the in-stock default (audited by delete).
                foreach (var state in due)
                    await _repository.DeleteAsync(state);

                _logger.LogInformation(
                    "Availability auto-restore: restored {Count} 'for a day' item(s) to in stock.",
                    due.Count);
            }
        }

        await uow.CompleteAsync();
    }
}
