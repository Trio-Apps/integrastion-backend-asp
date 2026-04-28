using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using OrderXChange.BackgroundJobs;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace Foodics
{
    [Authorize]
    [RemoteService(true)]
    public class FoodicsAccountAppService : TenantManagementAppServiceBase ,  IFoodicsAccountAppService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IRepository<FoodicsAccount> _foodicsAccountRepository;
        private readonly IMenuSyncAppService _menuSyncAppService;
        private readonly IConfiguration _configuration;
        private readonly IDataFilter<IMultiTenant> _multiTenantFilter;

        public FoodicsAccountAppService(
            ITenantRepository tenantRepository,
            IRepository<FoodicsAccount> foodicsAccountRepository,
            IMenuSyncAppService menuSyncAppService,
            IConfiguration configuration,
            IDataFilter<IMultiTenant> multiTenantFilter)
        {
            _tenantRepository = tenantRepository;
            _foodicsAccountRepository = foodicsAccountRepository;
            _menuSyncAppService = menuSyncAppService;
            _configuration = configuration;
            _multiTenantFilter = multiTenantFilter;
        }

        public  async Task<FoodicsAccountDto> CreateAsync(CreateUpdateFoodicsAccountDto input)
        {
            if (!CurrentTenant.IsAvailable)
                throw new UserFriendlyException(L["onlyTenantAvailable"]);

            var tenant = await _tenantRepository.GetAsync(CurrentTenant.Id.Value);

            var foodicsAccount = new FoodicsAccount
            {
                OAuthClientId = input.OAuthClientId,
                OAuthClientSecret = input.OAuthClientSecret,
                AccessToken = input.AccessToken,
                BrandName = input.BrandName,
                ApiEnvironment = FoodicsApiEnvironment.Normalize(input.ApiEnvironment)
            };
            EntityHelper.TrySetId(foodicsAccount, GuidGenerator.Create,
                           true);
            tenant.FoodicsAccounts.Add(foodicsAccount);
            await _tenantRepository.UpdateAsync(tenant , autoSave: true);

            if (!string.IsNullOrWhiteSpace(foodicsAccount.AccessToken))
            {
                await TriggerMenuSyncSafelyAsync(foodicsAccount.Id);
            }

            return ObjectMapper.Map<FoodicsAccount,FoodicsAccountDto>(foodicsAccount);
        }

        public  async Task<FoodicsAccountDto> UpdateAsync(Guid id, CreateUpdateFoodicsAccountDto input)
        {
            if (!CurrentTenant.IsAvailable)
                throw new UserFriendlyException(L["onlyTenantAvailable"]);

            var tenant = await _tenantRepository.GetAsync(CurrentTenant.Id.Value);
            var foodics = await _foodicsAccountRepository.GetAsync(x => x.Id == id);
            foodics.OAuthClientSecret = input.OAuthClientSecret;
            foodics.OAuthClientId = input.OAuthClientId;
            foodics.AccessToken = input.AccessToken;
            foodics.BrandName = input.BrandName;
            foodics.ApiEnvironment = FoodicsApiEnvironment.Normalize(input.ApiEnvironment);

            //tenant.FoodicsAccounts.Add(foodics);
            await _tenantRepository.UpdateAsync(tenant, autoSave: true);

            if (!string.IsNullOrWhiteSpace(foodics.AccessToken))
            {
                await TriggerMenuSyncSafelyAsync(foodics.Id);
            }
            return ObjectMapper.Map<FoodicsAccount, FoodicsAccountDto>(foodics);
        }

        public async Task<PagedResultDto<FoodicsAccountDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            var query = (await _foodicsAccountRepository.GetQueryableAsync()).PageBy(input.SkipCount, input.MaxResultCount);

            return new PagedResultDto<FoodicsAccountDto>
            {
                Items = ObjectMapper.Map<List<FoodicsAccount>, List<FoodicsAccountDto>>([.. query]),
                TotalCount = query.Count()
            };
        }

        public async Task DeleteAsync([Required]Guid id)
        {
            await _foodicsAccountRepository.DeleteAsync(x => x.Id == id);
        }

        public async Task<FoodicsAuthorizationUrlDto> GetAuthorizationUrlAsync(Guid id)
        {
            var account = await _foodicsAccountRepository.GetAsync(x => x.Id == id);

            if (string.IsNullOrWhiteSpace(account.OAuthClientId))
            {
                throw new UserFriendlyException("Foodics OAuth client id is missing for this account.");
            }

            var state = BuildAuthorizationState(account.Id);
            var baseUrl = FoodicsApiEnvironment.Normalize(account.ApiEnvironment) == FoodicsApiEnvironment.Production
                ? "https://console.foodics.com/authorize"
                : "https://console-sandbox.foodics.com/authorize";

            var authorizationUrl =
                $"{baseUrl}?client_id={Uri.EscapeDataString(account.OAuthClientId)}&state={Uri.EscapeDataString(state)}";

            return new FoodicsAuthorizationUrlDto
            {
                AuthorizationUrl = authorizationUrl,
                State = state,
                RedirectUri = GetOAuthRedirectUri()
            };
        }

        [AllowAnonymous]
        public async Task<FoodicsOAuthCallbackResultDto> CompleteAuthorizationAsync(CompleteFoodicsAuthorizationDto input)
        {
            if (string.IsNullOrWhiteSpace(input.Code))
            {
                return new FoodicsOAuthCallbackResultDto
                {
                    Success = false,
                    Message = "Foodics authorization callback did not include a code."
                };
            }

            if (!TryGetAccountIdFromState(input.State, out var foodicsAccountId))
            {
                return new FoodicsOAuthCallbackResultDto
                {
                    Success = false,
                    Message = "Foodics authorization state is invalid or missing.",
                    Details = input.State
                };
            }

            using (_multiTenantFilter.Disable())
            {
                var account = await _foodicsAccountRepository.GetAsync(x => x.Id == foodicsAccountId);
                var redirectUri = GetOAuthRedirectUri();

                try
                {
                    var accessToken = await RequestAccessTokenAsync(account, input.Code, redirectUri);

                    account.AccessToken = accessToken;
                    await _foodicsAccountRepository.UpdateAsync(account, autoSave: true);

                    await TriggerMenuSyncSafelyAsync(account.Id);

                    return new FoodicsOAuthCallbackResultDto
                    {
                        Success = true,
                        FoodicsAccountId = account.Id,
                        Message = "Foodics account connected successfully."
                    };
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Foodics authorization callback failed for FoodicsAccountId {FoodicsAccountId}.", account.Id);

                    return new FoodicsOAuthCallbackResultDto
                    {
                        Success = false,
                        FoodicsAccountId = account.Id,
                        Message = ex.Message,
                        Details = BuildExceptionDetails(ex)
                    };
                }
            }
        }

        public async Task<FoodicsConnectionTestResultDto> TestConnectionAsync(Guid id)
        {
            var account = await _foodicsAccountRepository.GetAsync(x => x.Id == id);

            try
            {
                var branches = await _menuSyncAppService.GetBranchesForAccountAsync(id);

                return new FoodicsConnectionTestResultDto
                {
                    Success = true,
                    Message = $"Foodics connection succeeded. Active branches returned: {branches.Count}.",
                    Details = branches.Count > 0
                        ? $"First branch: {branches[0].Name ?? branches[0].Id}"
                        : "Token was accepted, but Foodics returned no active branches.",
                    ApiEnvironment = FoodicsApiEnvironment.Normalize(account.ApiEnvironment),
                    AccessTokenConfigured = !string.IsNullOrWhiteSpace(account.AccessToken),
                    TestedAtUtc = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Foodics connection test failed for FoodicsAccountId {FoodicsAccountId}.", id);

                return new FoodicsConnectionTestResultDto
                {
                    Success = false,
                    Message = ex.Message,
                    Details = BuildExceptionDetails(ex),
                    ApiEnvironment = FoodicsApiEnvironment.Normalize(account.ApiEnvironment),
                    AccessTokenConfigured = !string.IsNullOrWhiteSpace(account.AccessToken),
                    TestedAtUtc = DateTime.UtcNow
                };
            }
        }

        private async Task TriggerMenuSyncSafelyAsync(Guid foodicsAccountId)
        {
            try
            {
                await _menuSyncAppService.TriggerMenuSyncAsync(foodicsAccountId);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to trigger menu sync for FoodicsAccountId {FoodicsAccountId}.", foodicsAccountId);
            }
        }

        private static string BuildExceptionDetails(Exception exception)
        {
            var messages = new List<string>();

            for (var current = exception; current != null; current = current.InnerException)
            {
                messages.Add($"{current.GetType().Name}: {current.Message}");
            }

            return string.Join(Environment.NewLine, messages);
        }

        private async Task<string> RequestAccessTokenAsync(FoodicsAccount account, string code, string redirectUri)
        {
            var tokenUrl = FoodicsApiEnvironment.Normalize(account.ApiEnvironment) == FoodicsApiEnvironment.Production
                ? "https://api.foodics.com/oauth/token"
                : "https://api-sandbox.foodics.com/oauth/token";

            using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
            {
                Content = JsonContent.Create(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["code"] = code,
                    ["client_id"] = account.OAuthClientId,
                    ["client_secret"] = account.OAuthClientSecret,
                    ["redirect_uri"] = redirectUri
                })
            };

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var httpClient = new HttpClient();
            var response = await httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Foodics token request failed. StatusCode={(int)response.StatusCode}, Body={body}");
            }

            var token = JsonSerializer.Deserialize<FoodicsTokenResponse>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new InvalidOperationException($"Foodics token response missing access_token. Body={body}");
            }

            return token.AccessToken;
        }

        private string GetOAuthRedirectUri()
        {
            var configuredRedirectUri = _configuration["Foodics:OAuthRedirectUri"];
            if (!string.IsNullOrWhiteSpace(configuredRedirectUri))
            {
                return configuredRedirectUri;
            }

            var selfUrl = _configuration["App:SelfUrl"]?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(selfUrl))
            {
                throw new InvalidOperationException("Foodics:OAuthRedirectUri or App:SelfUrl must be configured.");
            }

            return $"{selfUrl}/api/foodics/oauth/callback";
        }

        private static string BuildAuthorizationState(Guid foodicsAccountId)
        {
            var value = $"{foodicsAccountId:N}:{Guid.NewGuid():N}";
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static bool TryGetAccountIdFromState(string? state, out Guid foodicsAccountId)
        {
            foodicsAccountId = default;

            if (string.IsNullOrWhiteSpace(state))
            {
                return false;
            }

            try
            {
                var base64 = state.Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                var accountIdText = decoded.Split(':')[0];
                return Guid.TryParse(accountIdText, out foodicsAccountId);
            }
            catch
            {
                return false;
            }
        }

        private class FoodicsTokenResponse
        {
            public string AccessToken { get; set; } = string.Empty;
            public string TokenType { get; set; } = string.Empty;
        }
    }
}
