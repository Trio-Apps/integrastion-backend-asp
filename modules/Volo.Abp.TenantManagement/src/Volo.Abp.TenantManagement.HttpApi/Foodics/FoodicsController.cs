using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.TenantManagement;

namespace Foodics
{
    [Controller]
    [RemoteService(Name = "FoodicsAccount")]
    [Area(TenantManagementRemoteServiceConsts.ModuleName)]
    [Route("api/foodics")]
    public class FoodicsController : AbpControllerBase, IFoodicsAccountAppService
    {
        private readonly IFoodicsAccountAppService _foodicsAccountAppService;

        public FoodicsController(IFoodicsAccountAppService foodicsAccountAppService)
        {
            _foodicsAccountAppService = foodicsAccountAppService;
        }

        [HttpPost]
        public async Task<FoodicsAccountDto> CreateAsync(CreateUpdateFoodicsAccountDto input)
        {
            return await _foodicsAccountAppService.CreateAsync(input);
        }

        [HttpDelete]
        public Task DeleteAsync(Guid id)
        {
            return _foodicsAccountAppService.DeleteAsync(id);
        }

        [HttpGet]
        public async Task<PagedResultDto<FoodicsAccountDto>> GetListAsync(PagedAndSortedResultRequestDto input)
        {
            return await _foodicsAccountAppService.GetListAsync(input);
        }

        [HttpPost]
        [Route("{id}/authorization-url")]
        public Task<FoodicsAuthorizationUrlDto> GetAuthorizationUrlAsync(Guid id)
        {
            return _foodicsAccountAppService.GetAuthorizationUrlAsync(id);
        }

        [HttpPost]
        [Route("complete-authorization")]
        public Task<FoodicsOAuthCallbackResultDto> CompleteAuthorizationAsync(CompleteFoodicsAuthorizationDto input)
        {
            return _foodicsAccountAppService.CompleteAuthorizationAsync(input);
        }

        [HttpGet]
        [AllowAnonymous]
        [Route("oauth/callback")]
        public async Task<IActionResult> OAuthCallbackAsync(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            [FromQuery] string? error_description)
        {
            FoodicsOAuthCallbackResultDto result;

            if (!string.IsNullOrWhiteSpace(error))
            {
                result = new FoodicsOAuthCallbackResultDto
                {
                    Success = false,
                    Message = error,
                    Details = error_description
                };
            }
            else
            {
                result = await _foodicsAccountAppService.CompleteAuthorizationAsync(new CompleteFoodicsAuthorizationDto
                {
                    Code = code ?? string.Empty,
                    State = state ?? string.Empty
                });
            }

            var title = result.Success ? "Foodics Connected" : "Foodics Connection Failed";
            var color = result.Success ? "#0f9f6e" : "#c81e1e";
            var details = System.Net.WebUtility.HtmlEncode(result.Details ?? string.Empty);
            var message = System.Net.WebUtility.HtmlEncode(result.Message);

            return Content(
                $$"""
                <!doctype html>
                <html>
                <head>
                  <meta charset="utf-8">
                  <title>{{title}}</title>
                  <style>
                    body { font-family: Arial, sans-serif; margin: 40px; color: #24324a; }
                    .status { color: {{color}}; font-size: 22px; font-weight: 700; }
                    pre { white-space: pre-wrap; background: #f6f8fb; border: 1px solid #d7deea; padding: 12px; border-radius: 6px; }
                  </style>
                </head>
                <body>
                  <div class="status">{{title}}</div>
                  <p>{{message}}</p>
                  <pre>{{details}}</pre>
                  <p>You can close this window and return to BOOM-IT.</p>
                </body>
                </html>
                """,
                "text/html");
        }

        [HttpPost]
        [Route("{id}/test-connection")]
        public Task<FoodicsConnectionTestResultDto> TestConnectionAsync(Guid id)
        {
            return _foodicsAccountAppService.TestConnectionAsync(id);
        }

        [HttpPatch]
        [Route("{id}")]
        public async Task<FoodicsAccountDto> UpdateAsync(Guid id, CreateUpdateFoodicsAccountDto input)
        {
            return await _foodicsAccountAppService.UpdateAsync(id, input);
        }
    }
}
