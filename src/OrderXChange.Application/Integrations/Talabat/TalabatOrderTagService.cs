using System;
using System.Threading;
using System.Threading.Tasks;
using Foodics;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace OrderXChange.Application.Integrations.Talabat;

public class TalabatOrderTagService : ITransientDependency
{
    private const string TalabatOrderTagName = "Talabat";

    private readonly IRepository<FoodicsAccount, Guid> _foodicsAccountRepository;
    private readonly FoodicsAccountTokenService _foodicsAccountTokenService;
    private readonly FoodicsTagClient _foodicsTagClient;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<TalabatOrderTagService> _logger;

    public TalabatOrderTagService(
        IRepository<FoodicsAccount, Guid> foodicsAccountRepository,
        FoodicsAccountTokenService foodicsAccountTokenService,
        FoodicsTagClient foodicsTagClient,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<TalabatOrderTagService> logger)
    {
        _foodicsAccountRepository = foodicsAccountRepository;
        _foodicsAccountTokenService = foodicsAccountTokenService;
        _foodicsTagClient = foodicsTagClient;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    public async Task<string?> GetTalabatOrderTagIdAsync(
        Guid foodicsAccountId,
        CancellationToken cancellationToken = default)
    {
        using (var readUow = _unitOfWorkManager.Begin(requiresNew: true))
        {
            var existingAccount = await _foodicsAccountRepository.GetAsync(
                x => x.Id == foodicsAccountId,
                cancellationToken: cancellationToken);

            await readUow.CompleteAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(existingAccount?.TalabatOrderTagId))
            {
                return existingAccount.TalabatOrderTagId;
            }
        }

        var accessToken = await _foodicsAccountTokenService.GetAccessTokenAsync(foodicsAccountId, cancellationToken);
        var tag = await _foodicsTagClient.FindOrderTagByNameAsync(
            TalabatOrderTagName,
            accessToken,
            foodicsAccountId,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(tag?.Id))
        {
            _logger.LogWarning(
                "Foodics Talabat order tag was not found. FoodicsAccountId={FoodicsAccountId}, TagName={TagName}",
                foodicsAccountId,
                TalabatOrderTagName);
            return null;
        }

        using var writeUow = _unitOfWorkManager.Begin(requiresNew: true);
        var accountToUpdate = await _foodicsAccountRepository.GetAsync(
            x => x.Id == foodicsAccountId,
            cancellationToken: cancellationToken);

        accountToUpdate.TalabatOrderTagId = tag.Id;
        await _foodicsAccountRepository.UpdateAsync(accountToUpdate, autoSave: true, cancellationToken: cancellationToken);
        await writeUow.CompleteAsync(cancellationToken);

        _logger.LogInformation(
            "Cached Foodics Talabat order tag id. FoodicsAccountId={FoodicsAccountId}, TagId={TagId}",
            foodicsAccountId,
            tag.Id);

        return tag.Id;
    }
}
