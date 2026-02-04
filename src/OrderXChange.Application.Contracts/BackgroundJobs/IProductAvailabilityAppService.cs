using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;
using Volo.Abp.Application.Services;

namespace OrderXChange.BackgroundJobs;

/// <summary>
/// Application service for fetching and preparing product availability data from Foodics
/// </summary>
public interface IProductAvailabilityAppService : IApplicationService
{
    /// <summary>
    /// Fetches products with availability and pricing from Foodics and prepares data for Talabat
    /// </summary>
    /// <param name="page">Page number to fetch (default: 1)</param>
    /// <param name="perPage">Items per page (default: 100)</param>
    /// <returns>Prepared product availability data ready for Talabat integration</returns>
    Task<ProductAvailabilitySyncResultDto> FetchAndPrepareAsync(int page = 1, int perPage = 100);
}


