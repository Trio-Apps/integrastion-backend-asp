using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Talabat;
using OrderXChange.Application.Staging;
using OrderXChange.Domain.Staging;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Domain.Repositories;

namespace OrderXChange.Controllers;

/// <summary>
/// Test controller for Talabat submission from staging table
/// </summary>
[Route("api/test/talabat-submission")]
[AllowAnonymous] // For testing only - remove in production
public class TalabatSubmissionTestController : AbpController
{
	private readonly IRepository<FoodicsProductStaging, Guid> _stagingRepository;
	private readonly IRepository<Foodics.FoodicsAccount, Guid> _foodicsAccountRepository;
	private readonly FoodicsProductStagingToFoodicsConverter _stagingConverter;
	private readonly TalabatCatalogSyncService _talabatSyncService;
	private readonly IConfiguration _configuration;
	private readonly ILogger<TalabatSubmissionTestController> _logger;

	public TalabatSubmissionTestController(
		IRepository<FoodicsProductStaging, Guid> stagingRepository,
		IRepository<Foodics.FoodicsAccount, Guid> foodicsAccountRepository,
		FoodicsProductStagingToFoodicsConverter stagingConverter,
		TalabatCatalogSyncService talabatSyncService,
		IConfiguration configuration,
		ILogger<TalabatSubmissionTestController> logger)
	{
		_stagingRepository = stagingRepository;
		_foodicsAccountRepository = foodicsAccountRepository;
		_stagingConverter = stagingConverter;
		_talabatSyncService = talabatSyncService;
		_configuration = configuration;
		_logger = logger;
	}

	/// <summary>
	/// Submit menu from staging table to Talabat
	/// Submits for the oldest account (by last submission time)
	/// </summary>
	[HttpPost("submit-from-staging")]
	public async Task<IActionResult> SubmitFromStagingAsync(
		[FromQuery] Guid? foodicsAccountId = null,
		CancellationToken cancellationToken = default)
	{
		var testId = Guid.NewGuid().ToString("N")[..8];

		_logger.LogInformation(
			"🚀 [Test {TestId}] Starting Talabat submission from staging table. RequestedAccountId={AccountId}",
			testId,
			foodicsAccountId?.ToString() ?? "<oldest>");

		try
		{
			// Step 1: Determine which account to submit
			Guid accountToSubmit;
			if (foodicsAccountId.HasValue)
			{
				accountToSubmit = foodicsAccountId.Value;
				_logger.LogInformation(
					"📌 [Test {TestId}] Using specified account: {AccountId}",
					testId,
					accountToSubmit);
			}
			else
			{
				// Find oldest account by last submission time
				var allStaging = await _stagingRepository.GetListAsync(cancellationToken: cancellationToken);
				
				var accountGroups = allStaging
					.GroupBy(x => x.FoodicsAccountId)
					.Select(g => new
					{
						AccountId = g.Key,
						LastSubmitted = g.Max(x => x.TalabatSubmittedAt),
						ProductCount = g.Count(),
						LastSynced = g.Max(x => x.SyncDate)
					})
					.OrderBy(x => x.LastSubmitted ?? DateTime.MinValue)  // Oldest submission first (or never submitted)
					.ThenBy(x => x.LastSynced)  // Then oldest sync
					.ToList();

				if (!accountGroups.Any())
				{
					return Ok(new
					{
						success = false,
						message = "No products found in staging table",
						testId,
						timestamp = DateTime.UtcNow
					});
				}

				var oldestAccount = accountGroups.First();
				accountToSubmit = oldestAccount.AccountId;

				_logger.LogInformation(
					"📌 [Test {TestId}] Selected oldest account: {AccountId}, Products={ProductCount}, " +
					"LastSubmitted={LastSubmitted}, LastSynced={LastSynced}",
					testId,
					accountToSubmit,
					oldestAccount.ProductCount,
					oldestAccount.LastSubmitted?.ToString("yyyy-MM-dd HH:mm:ss") ?? "NEVER",
					oldestAccount.LastSynced.ToString("yyyy-MM-dd HH:mm:ss"));

				_logger.LogInformation(
					"📊 [Test {TestId}] All accounts submission status:",
					testId);

				foreach (var account in accountGroups.Take(5))
				{
					_logger.LogInformation(
						"   - Account {AccountId}: {ProductCount} products, Last submitted: {LastSubmitted}",
						account.AccountId,
						account.ProductCount,
						account.LastSubmitted?.ToString("yyyy-MM-dd HH:mm:ss") ?? "NEVER");
				}
			}

			// Step 2: Load products from staging
			var stagingProducts = await _stagingRepository.GetListAsync(
				x => x.FoodicsAccountId == accountToSubmit && x.IsActive,
				cancellationToken: cancellationToken);

			if (!stagingProducts.Any())
			{
				return Ok(new
				{
					success = false,
					message = $"No active products found in staging for account {accountToSubmit}",
					accountId = accountToSubmit,
					testId,
					timestamp = DateTime.UtcNow
				});
			}

			_logger.LogInformation(
				"📦 [Test {TestId}] Loaded {ProductCount} active products from staging for account {AccountId}",
				testId,
				stagingProducts.Count,
				accountToSubmit);

			// Step 3: Convert staging products to Foodics DTOs
			var foodicsProducts = _stagingConverter.ConvertToFoodicsDto(stagingProducts);

			_logger.LogInformation(
				"🔄 [Test {TestId}] Converted {ConvertedCount}/{TotalCount} staging products to Foodics DTOs",
				testId,
				foodicsProducts.Count,
				stagingProducts.Count);

			if (!foodicsProducts.Any())
			{
				return Ok(new
				{
					success = false,
					message = "Failed to convert staging products to Foodics format",
					accountId = accountToSubmit,
					stagingProductCount = stagingProducts.Count,
					testId,
					timestamp = DateTime.UtcNow
				});
			}

			// Step 4: Get Talabat configuration
			var chainCode = _configuration["Talabat:ChainCode"];
			var callbackBaseUrl = _configuration["Talabat:CallbackBaseUrl"];

			if (string.IsNullOrWhiteSpace(chainCode))
			{
				return Ok(new
				{
					success = false,
					message = "Talabat ChainCode not configured (check appsettings.json)",
					testId,
					timestamp = DateTime.UtcNow
				});
			}

			var callbackUrl = !string.IsNullOrWhiteSpace(callbackBaseUrl)
				? $"{callbackBaseUrl.TrimEnd('/')}/catalog-status"
				: null;

			_logger.LogInformation(
				"⚙️ [Test {TestId}] Talabat configuration: ChainCode={ChainCode}, CallbackUrl={CallbackUrl}",
				testId,
				chainCode,
				callbackUrl ?? "<not configured>");

			// Step 5: Submit to Talabat using V2 API
			_logger.LogInformation(
				"🚀 [Test {TestId}] Submitting to Talabat V2 API...",
				testId);

			var correlationId = $"test-{testId}";
			var result = await _talabatSyncService.SyncCatalogV2Async(
				foodicsProducts,
				chainCode,
				accountToSubmit,
				correlationId,
				vendorCode: null,
				cancellationToken);

			// Step 6: Return result
			if (result.Success)
			{
				_logger.LogInformation(
					"✅ [Test {TestId}] Talabat submission SUCCESSFUL! ImportId={ImportId}, Duration={Duration}ms",
					testId,
					result.ImportId,
					result.Duration?.TotalMilliseconds ?? 0);

				return Ok(new
				{
					success = true,
					message = "Catalog submitted to Talabat successfully",
					testId,
					accountId = accountToSubmit,
					correlationId,
					importId = result.ImportId,
					chainCode,
					callbackUrl,
					stagingProductCount = stagingProducts.Count,
					convertedProductCount = foodicsProducts.Count,
					categoriesCount = result.CategoriesCount,
					productsCount = result.ProductsCount,
					duration = result.Duration?.TotalMilliseconds,
					timestamp = DateTime.UtcNow,
					webhookInstructions = new
					{
						message = "Webhook will be sent to the callback URL when import completes",
						callbackEndpoint = callbackUrl,
						ngrokStatus = callbackUrl?.Contains("ngrok") == true ? "ACTIVE" : "N/A",
						checkLogs = "Monitor application logs for webhook receipt"
					}
				});
			}
			else
			{
				_logger.LogError(
					"❌ [Test {TestId}] Talabat submission FAILED! Message={Message}, Errors={Errors}",
					testId,
					result.Message,
					result.Errors != null ? string.Join("; ", result.Errors.Take(3)) : "<none>");

				return Ok(new
				{
					success = false,
					message = result.Message ?? "Talabat submission failed",
					testId,
					accountId = accountToSubmit,
					correlationId,
					chainCode,
					callbackUrl,
					stagingProductCount = stagingProducts.Count,
					convertedProductCount = foodicsProducts.Count,
					errors = result.Errors,
					duration = result.Duration?.TotalMilliseconds,
					timestamp = DateTime.UtcNow
				});
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"💥 [Test {TestId}] Exception during Talabat submission test. Error: {Error}",
				testId,
				ex.Message);

			return Ok(new
			{
				success = false,
				message = "Exception occurred during submission",
				testId,
				error = ex.Message,
				stackTrace = ex.StackTrace,
				timestamp = DateTime.UtcNow
			});
		}
	}

	/// <summary>
	/// Get staging table statistics
	/// </summary>
	[HttpGet("staging-stats")]
	public async Task<IActionResult> GetStagingStatsAsync(CancellationToken cancellationToken = default)
	{
		var allStaging = await _stagingRepository.GetListAsync(cancellationToken: cancellationToken);

		var stats = new
		{
			totalProducts = allStaging.Count,
			activeProducts = allStaging.Count(x => x.IsActive),
			inactiveProducts = allStaging.Count(x => !x.IsActive),
			uniqueAccounts = allStaging.Select(x => x.FoodicsAccountId).Distinct().Count(),
			uniqueCategories = allStaging.Where(x => !string.IsNullOrWhiteSpace(x.CategoryId))
				.Select(x => x.CategoryId).Distinct().Count(),
			
			submissionStatus = new
			{
				submitted = allStaging.Count(x => x.TalabatSubmittedAt != null),
				notSubmitted = allStaging.Count(x => x.TalabatSubmittedAt == null),
				completed = allStaging.Count(x => x.TalabatSyncCompletedAt != null),
				failed = allStaging.Count(x => !string.IsNullOrWhiteSpace(x.TalabatLastError))
			},

			accountBreakdown = allStaging
				.GroupBy(x => x.FoodicsAccountId)
				.Select(g => new
				{
					accountId = g.Key,
					productCount = g.Count(),
					activeCount = g.Count(x => x.IsActive),
					lastSynced = g.Max(x => x.SyncDate),
					lastSubmitted = g.Max(x => x.TalabatSubmittedAt),
					syncStatus = g.Max(x => x.TalabatSyncStatus) ?? "Not Submitted"
				})
				.OrderBy(x => x.lastSubmitted ?? DateTime.MinValue)
				.ToList(),

			timestamp = DateTime.UtcNow
		};

		return Ok(stats);
	}

	/// <summary>
	/// Get Talabat configuration
	/// </summary>
	[HttpGet("talabat-config")]
	public IActionResult GetTalabatConfig()
	{
		var config = new
		{
			baseUrl = _configuration["Talabat:BaseUrl"],
			chainCode = _configuration["Talabat:ChainCode"],
			callbackBaseUrl = _configuration["Talabat:CallbackBaseUrl"],
			callbackEndpoint = !string.IsNullOrWhiteSpace(_configuration["Talabat:CallbackBaseUrl"])
				? $"{_configuration["Talabat:CallbackBaseUrl"]?.TrimEnd('/')}/catalog-status"
				: null,
			enabled = _configuration.GetValue<bool>("Talabat:Enabled", true),
			defaultVendorCode = _configuration["Talabat:DefaultVendorCode"],
			vendors = _configuration.GetSection("Talabat:Vendors").Get<string[]>(),
			ngrokActive = _configuration["Talabat:CallbackBaseUrl"]?.Contains("ngrok") == true,
			timestamp = DateTime.UtcNow
		};

		return Ok(config);
	}
}

