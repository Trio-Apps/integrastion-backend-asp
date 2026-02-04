using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace OrderXChange.HttpApi.Host.Filters;

/// <summary>
/// Action filter to log slow API requests (> 1 second) to help diagnose performance issues
/// </summary>
public class SlowRequestLoggingFilter : IAsyncActionFilter, ITransientDependency
{
    private readonly ILogger<SlowRequestLoggingFilter> _logger;
    private const int SlowRequestThresholdMs = 1000; // 1 second

    public SlowRequestLoggingFilter(ILogger<SlowRequestLoggingFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await next();
        }
        finally
        {
            stopwatch.Stop();
            
            if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "⚠️ SLOW REQUEST: {Method} {Path} took {ElapsedMs}ms",
                    context.HttpContext.Request.Method,
                    context.HttpContext.Request.Path,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogDebug(
                    "✅ Request: {Method} {Path} completed in {ElapsedMs}ms",
                    context.HttpContext.Request.Method,
                    context.HttpContext.Request.Path,
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }
}

