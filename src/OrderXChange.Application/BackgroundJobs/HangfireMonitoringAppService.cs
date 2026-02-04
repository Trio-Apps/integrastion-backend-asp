using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;
using Hangfire.Storage.Monitoring;
using Volo.Abp.Application.Services;

namespace OrderXChange.BackgroundJobs;

public class HangfireMonitoringAppService : ApplicationService, IHangfireMonitoringAppService
{
    private readonly JobStorage _jobStorage;

    public HangfireMonitoringAppService(JobStorage jobStorage)
    {
        _jobStorage = jobStorage;
    }

    public Task<HangfireDashboardDto> GetDashboardAsync()
    {
        var monitoringApi = _jobStorage.GetMonitoringApi();
        var statistics = monitoringApi.GetStatistics();

        var dto = new HangfireDashboardDto
        {
            Counts = new HangfireJobCountDto
            {
                Succeeded = statistics.Succeeded,
                Failed = statistics.Failed,
                Processing = statistics.Processing,
                Scheduled = statistics.Scheduled,
                Enqueued = statistics.Enqueued,
                AwaitingRetry = 0,
                Deleted = statistics.Deleted
            },
            SucceededJobs = MapJobs(monitoringApi, monitoringApi.SucceededJobs(0, 10).Select(job => job.Key), "Succeeded"),
            FailedJobs = MapJobs(monitoringApi, monitoringApi.FailedJobs(0, 10).Select(job => job.Key), "Failed"),
            EnqueuedJobs = MapEnqueuedJobs(monitoringApi),
            ScheduledJobs = MapScheduledJobs(monitoringApi, monitoringApi.ScheduledJobs(0, 10)),
            ProcessingJobs = MapProcessingJobs(monitoringApi, monitoringApi.ProcessingJobs(0, 10))
        };

        return Task.FromResult(dto);
    }

    private static List<HangfireJobItemDto> MapJobs(IMonitoringApi monitoringApi, IEnumerable<string> jobIds, string state)
    {
        return jobIds
            .Select(jobId => CreateJobItem(monitoringApi, jobId, state))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();
    }

    private static List<HangfireJobItemDto> MapProcessingJobs(IMonitoringApi monitoringApi, IEnumerable<KeyValuePair<string, ProcessingJobDto>> jobs)
    {
        return jobs
            .Select(job =>
            {
                var item = CreateJobItem(monitoringApi, job.Key, "Processing") ?? new HangfireJobItemDto
                {
                    Id = job.Key,
                    State = "Processing"
                };

                item.StartedAt ??= job.Value.StartedAt;
                item.Arguments ??= FormatArguments(job.Value.Job);

                return item;
            })
            .ToList();
    }

    private static List<HangfireJobItemDto> MapScheduledJobs(IMonitoringApi monitoringApi, JobList<ScheduledJobDto> jobs)
    {
        return jobs
            .Select(job =>
            {
                var item = CreateJobItem(monitoringApi, job.Key, "Scheduled") ?? new HangfireJobItemDto
                {
                    Id = job.Key,
                    State = "Scheduled"
                };

                item.ScheduledAt = job.Value.ScheduledAt;
                item.EnqueuedAt = job.Value.EnqueueAt;
                item.Arguments ??= FormatArguments(job.Value.Job);

                return item;
            })
            .ToList();
    }

    private static List<HangfireJobItemDto> MapEnqueuedJobs(IMonitoringApi monitoringApi)
    {
        var result = new List<HangfireJobItemDto>();
        var queues = monitoringApi.Queues();

        foreach (var queue in queues)
        {
            var jobs = monitoringApi.EnqueuedJobs(queue.Name, 0, (int)System.Math.Min(queue.Length, 10));
            result.AddRange(jobs.Select(job =>
            {
                var item = CreateJobItem(monitoringApi, job.Key, "Enqueued") ?? new HangfireJobItemDto
                {
                    Id = job.Key,
                    State = "Enqueued"
                };

                item.Queue = queue.Name;
                item.EnqueuedAt = job.Value.EnqueuedAt;
                item.Arguments ??= FormatArguments(job.Value.Job);

                return item;
            }));
        }

        return result;
    }

    private static HangfireJobItemDto? CreateJobItem(IMonitoringApi monitoringApi, string jobId, string state)
    {
        var details = monitoringApi.JobDetails(jobId);
        if (details == null)
        {
            return new HangfireJobItemDto
            {
                Id = jobId,
                State = state
            };
        }

        return new HangfireJobItemDto
        {
            Id = jobId,
            State = state,
            JobName = FormatJobName(details.Job),
            Arguments = FormatArguments(details.Job),
            CreatedAt = details.CreatedAt,
            Queue = TryGetQueue(details.Properties),
            ExceptionMessage = TryGetExceptionMessage(details.History)
        };
    }

    private static string? FormatJobName(Job? job)
    {
        if (job == null)
        {
            return null;
        }

        var typeName = job.Type?.Name ?? job.Type?.FullName;
        var methodName = job.Method?.Name;
        return typeName != null && methodName != null ? $"{typeName}.{methodName}" : typeName ?? methodName;
    }

    private static string? FormatArguments(Job? job)
    {
        if (job?.Args == null || job.Args.Count == 0)
        {
            return null;
        }

        return string.Join(", ", job.Args.Select(arg => arg?.ToString()));
    }

    private static string? TryGetQueue(IDictionary<string, string>? properties)
    {
        if (properties != null && properties.TryGetValue("Queue", out var queue))
        {
            return queue;
        }

        return null;
    }

    private static string? TryGetExceptionMessage(IList<StateHistoryDto>? history)
    {
        var failedState = history?.FirstOrDefault(h => h.StateName == "Failed");
        if (failedState?.Data != null && failedState.Data.TryGetValue("ExceptionMessage", out var message))
        {
            return message;
        }

        return null;
    }
}

