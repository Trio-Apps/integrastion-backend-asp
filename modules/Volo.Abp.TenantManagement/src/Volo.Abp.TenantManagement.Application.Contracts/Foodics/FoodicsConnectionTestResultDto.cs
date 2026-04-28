using System;

namespace Foodics
{
    public class FoodicsConnectionTestResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
        public string ApiEnvironment { get; set; }
        public bool AccessTokenConfigured { get; set; }
        public DateTime TestedAtUtc { get; set; }
    }
}
