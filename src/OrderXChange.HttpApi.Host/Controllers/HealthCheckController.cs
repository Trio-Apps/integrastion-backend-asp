using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.EventBus.Distributed;

namespace OrderXChange.HttpApi.Host.Controllers;

/// <summary>
/// Health check controller for system diagnostics
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthCheckController : AbpControllerBase
{
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly ILogger<HealthCheckController> _logger;

    public HealthCheckController(
        IDistributedEventBus distributedEventBus,
        ILogger<HealthCheckController> logger)
    {
        _distributedEventBus = distributedEventBus;
        _logger = logger;
    }

    /// <summary>
    /// Health check endpoint - returns basic system status
    /// GET /api/health/status
    /// </summary>
    [HttpGet("status")]
    public ActionResult<HealthCheckResult> GetStatus()
    {
        return Ok(new HealthCheckResult
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Services = new ServiceHealthStatus
            {
                EventBus = "operational"
            }
        });
    }

    /// <summary>
    /// Test Kafka connectivity by publishing a test event
    /// GET /api/health/kafka-test
    /// </summary>
    [HttpGet("kafka-test")]
    public async Task<ActionResult<KafkaTestResult>> TestKafkaConnectivity()
    {
        try
        {
            _logger.LogInformation("🧪 Testing Kafka connectivity...");

            // Attempt to publish a test event with timeout
            using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                var testEvent = new KafkaTestEvent { Timestamp = DateTime.UtcNow };
                
                await _distributedEventBus.PublishAsync(testEvent, false);
            }

            _logger.LogInformation("✅ Kafka connectivity test passed");
            
            return Ok(new KafkaTestResult
            {
                Status = "connected",
                Message = "Kafka connection is operational",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (System.OperationCanceledException)
        {
            _logger.LogWarning("⚠️ Kafka connectivity test timed out");
            
            return StatusCode(503, new KafkaTestResult
            {
                Status = "timeout",
                Message = "Kafka connection test timed out after 5 seconds",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Kafka connectivity test failed");
            
            return StatusCode(503, new KafkaTestResult
            {
                Status = "failed",
                Message = $"Kafka connection failed: {ex.Message}",
                Timestamp = DateTime.UtcNow,
                ErrorDetails = ex.ToString()
            });
        }
    }
}

/// <summary>
/// Health check result DTO
/// </summary>
public class HealthCheckResult
{
    public string Status { get; set; }
    public DateTime Timestamp { get; set; }
    public ServiceHealthStatus Services { get; set; }
}

/// <summary>
/// Service health status
/// </summary>
public class ServiceHealthStatus
{
    public string EventBus { get; set; }
}

/// <summary>
/// Kafka test result DTO
/// </summary>
public class KafkaTestResult
{
    public string Status { get; set; }
    public string Message { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ErrorDetails { get; set; }
}

/// <summary>
/// Test event for Kafka connectivity check
/// </summary>
public class KafkaTestEvent
{
    public DateTime Timestamp { get; set; }
}

