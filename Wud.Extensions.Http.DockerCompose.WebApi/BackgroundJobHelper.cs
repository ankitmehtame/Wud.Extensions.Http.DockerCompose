using System.Linq.Expressions;
using Hangfire;
using Nito.AsyncEx;

namespace Wud.Extensions.Http.DockerCompose.WebApi.Tests;

public class BackgroundJobHelper(ILogger<BackgroundJobHelper> logger, IBackgroundJobClient backgroundJobClient, IRecurringJobManager recurringJobManager, BackgroundJobCaller caller) : IBackgroundJobHelper
{
    private readonly AsyncLock jobScheduleLock = new ();
    private static readonly TimeSpan firstJobTime = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan secondJobTime = TimeSpan.FromMinutes(2);

    private string? firstRetryJobId = null;
    private string? secondRetryJobId = null;
    private const string backgroundCronExpression = "15 * * * *";
    private const string containersSyncJobName = "ContainersSync";

    public Task ScheduleRecurringBackgroundSyncJob()
    {
        logger.LogInformation("Scheduling background job for " + containersSyncJobName);
        recurringJobManager.AddOrUpdate(containersSyncJobName, caller.GetSyncMethodCall(), backgroundCronExpression);
        return Task.FromResult(true);
    }

    public async Task ScheduleOneTimeBackgroundSyncJobs()
    {
        using var jobSync = await jobScheduleLock.LockAsync();
        var isFirstJobRescheduledSuccessfully = false;
        if (firstRetryJobId != null)
        {
            isFirstJobRescheduledSuccessfully = backgroundJobClient.Reschedule(firstRetryJobId, firstJobTime);
        }
        if (!isFirstJobRescheduledSuccessfully)
        {
            firstRetryJobId = backgroundJobClient.Schedule(caller.GetSyncMethodCall(), firstJobTime);
        }
        
        var isSecondJobRescheduledSuccessfully = false;
        if (secondRetryJobId != null)
        {
            isSecondJobRescheduledSuccessfully = backgroundJobClient.Reschedule(secondRetryJobId, secondJobTime);
        }
        
        if (!isSecondJobRescheduledSuccessfully)
        {
            secondRetryJobId = backgroundJobClient.Schedule(caller.GetSyncMethodCall(), secondJobTime);
        }
    }
}

public class BackgroundJobCaller
{

    public BackgroundJobCaller(IServiceProvider serviceProvider)
    {
        syncMethodCall = new (() =>
        {
            var containersApi = serviceProvider.GetRequiredService<ContainerApis>();
            return () => containersApi.ContainersSyncApiInternal();
        }, isThreadSafe: true);
    }
    
    private readonly Lazy<Expression<Func<Task>>> syncMethodCall;

    public Expression<Func<Task>> GetSyncMethodCall()
    {
        return syncMethodCall.Value;
    }
}

public interface IBackgroundJobHelper
{
    Task ScheduleRecurringBackgroundSyncJob();
    Task ScheduleOneTimeBackgroundSyncJobs();
}
