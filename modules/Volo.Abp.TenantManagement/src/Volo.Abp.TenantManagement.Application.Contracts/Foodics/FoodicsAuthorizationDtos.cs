using System;

namespace Foodics
{
    public class FoodicsAuthorizationUrlDto
    {
        public string AuthorizationUrl { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
    }

    public class CompleteFoodicsAuthorizationDto
    {
        public string Code { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
    }

    public class FoodicsOAuthCallbackResultDto
    {
        public bool Success { get; set; }
        public Guid? FoodicsAccountId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
    }
}
