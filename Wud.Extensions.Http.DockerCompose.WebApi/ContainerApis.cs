using System.Collections.Frozen;
using Nito.AsyncEx;
using Wud.Extensions.Http.DockerCompose.WebApi.Tests;

namespace Wud.Extensions.Http.DockerCompose.WebApi;

public class ContainerApis(ILogger<ContainerApis> logger, IDockerComposeUtility dockerComposeUtility, IWudService wudService, IBackgroundJobHelper backgroundJobHelper)
{
    private readonly AsyncLock syncLock = new ();

    public async Task<IResult> ContainerNewVersionApi(WudContainer container)
    {
        logger.LogInformation("{api} called - {container}", Constants.Api.CONTAINER_NEW_VERSION_API, container);
        var res = await dockerComposeUtility.UpdateDockerFileForContainer(container);
        var response = new Dictionary<string, UpdateResult> { { container.Name, res } };
        await backgroundJobHelper.ScheduleOneTimeBackgroundSyncJobs();
        return Results.Json(response, statusCode: GetStatusCode(res));
    }
    
    public async Task<IResult> ContainersSyncApi()
    {
        return await ContainersSyncApi(isInternal: false);
    }

    public async Task<IResult> ContainersSyncApiInternal()
    {
        return await ContainersSyncApi(isInternal: true);
    }

    private async Task<IResult> ContainersSyncApi(bool isInternal)
    {
        if (!isInternal)
            logger.LogInformation("{api} called", Constants.Api.CONTAINERS_SYNC_API);
        else
            logger.LogInformation("Running background job - {api}", Constants.Api.CONTAINERS_SYNC_API);
        using var oneSyncAtOnce = await syncLock.LockAsync();
        var allDockerFileContainers = (await Task.WhenAll(dockerComposeUtility.GetDockerFiles()
                .Select(dockerComposeUtility.GetContainerNamesFromDockerFile)))
            .SelectMany(x => x).ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        WudContainer[] containers;
        try
        {
            containers = await wudService.GetContainers();
        }
        catch (Exception)
        {
            return Results.Problem("Containers unreachable");
        }
        Dictionary<string, UpdateResult> mapResult = [];
        foreach (var container in containers)
        {
            if (!allDockerFileContainers.Contains(container.Name))
                continue;
            logger.LogInformation("Container {container} - checking", container.Name);
            var res = await dockerComposeUtility.UpdateDockerFileForContainer(container);
            mapResult[container.Name] = res;
            logger.LogInformation("Container {container} result - {result}", container.Name, res.ToString());
        }
        
        var statusCodes = mapResult.Values.Select(GetStatusCode).ToArray();
        int finalStatusCode;
        if (!statusCodes.Any()) finalStatusCode = StatusCodes.Status202Accepted;
        else if (statusCodes.Distinct().Count() == 1) finalStatusCode = statusCodes.First();
        else if (statusCodes.Any(code => code < 300)) finalStatusCode = StatusCodes.Status206PartialContent;
        else finalStatusCode = statusCodes.Max();
        logger.LogInformation("Result {result}", mapResult.ToJson());
        return Results.Json(mapResult, statusCode: finalStatusCode);
    }

    private static int GetStatusCode(UpdateResult updateResult)
    {
        return updateResult switch
        {
            UpdateResult.ContainerNotFound => StatusCodes.Status404NotFound,
            UpdateResult.ImageNotFound => StatusCodes.Status404NotFound,
            UpdateResult.NoDockerFiles => StatusCodes.Status404NotFound,
            UpdateResult.AlreadyUpToDate => StatusCodes.Status200OK,
            UpdateResult.UpdatedSuccessfully => StatusCodes.Status200OK,
            UpdateResult.UnableToUpdateDockerFile => StatusCodes.Status500InternalServerError,
            _ => throw new NotSupportedException(updateResult.ToString())
        };
    }
}
