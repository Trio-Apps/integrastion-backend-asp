using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Domain.Staging;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using System.Linq.Dynamic.Core;

namespace OrderXChange.Talabat;

[Route("api/app/talabat-order-logs")]
public class TalabatOrderLogAppService : ApplicationService, ITalabatOrderLogAppService
{
    private readonly IRepository<TalabatOrderSyncLog, Guid> _orderLogRepository;

    public TalabatOrderLogAppService(IRepository<TalabatOrderSyncLog, Guid> orderLogRepository)
    {
        _orderLogRepository = orderLogRepository;
    }

    [HttpGet]
    public async Task<PagedResultDto<TalabatOrderLogDto>> GetListAsync(GetTalabatOrderLogsInput input)
    {
        var queryable = await _orderLogRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.VendorCode))
        {
            var vendor = input.VendorCode.Trim();
            queryable = queryable.Where(x => x.VendorCode == vendor);
        }

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            var status = input.Status.Trim();
            queryable = queryable.Where(x => x.Status == status);
        }

        if (input.IsTestOrder.HasValue)
        {
            queryable = queryable.Where(x => x.IsTestOrder == input.IsTestOrder.Value);
        }

        if (input.FromDate.HasValue)
        {
            queryable = queryable.Where(x => x.ReceivedAt >= input.FromDate.Value);
        }

        if (input.ToDate.HasValue)
        {
            queryable = queryable.Where(x => x.ReceivedAt <= input.ToDate.Value);
        }

        var totalCount = await queryable.CountAsync();

        var sorting = string.IsNullOrWhiteSpace(input.Sorting)
            ? "ReceivedAt desc"
            : input.Sorting;

        var items = await queryable
            .OrderBy(sorting)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        var dtoItems = items.Select(ObjectMapper.Map<TalabatOrderSyncLog, TalabatOrderLogDto>).ToList();

        return new PagedResultDto<TalabatOrderLogDto>(totalCount, dtoItems);
    }
}
