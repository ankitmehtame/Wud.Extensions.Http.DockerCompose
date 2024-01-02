using System.Collections.Frozen;
using System.Text.Json;

namespace Wud.Extensions.Http.DockerCompose.WebApi;

public class ContainerApis(ILogger<ContainerApis> logger, IDockerComposeUtility dockerComposeUtility, IWudService wudService)
{
    public async Task<IResult> ContainerNewVersionApi(WudContainer container)
    {
        logger.LogInformation("{api} called - {container}", Constants.Api.CONTAINER_NEW_VERSION_API, container);
        var res = await dockerComposeUtility.UpdateDockerFileForContainer(container);
        var responseJson = JsonSerializer.Serialize(new Dictionary<string, UpdateResult> { { container.Name, res } }, JsonUtils.Options);
        return res switch
        {
            UpdateResult.ContainerNotFound => Results.NotFound(responseJson),
            UpdateResult.ImageNotFound => Results.NotFound(responseJson),
            UpdateResult.NoDockerFiles => Results.NotFound(responseJson),
            UpdateResult.AlreadyUpToDate => Results.Ok(responseJson),
            UpdateResult.UpdatedSuccessfully => Results.Ok(responseJson),
            UpdateResult.UnableToUpdateDockerFile => Results.Problem(responseJson),
            _ => throw new NotSupportedException(res.ToString())
        };
    }

    public async Task<IResult> ContainersSyncApi()
    {
        logger.LogInformation("{api} called", Constants.Api.CONTAINERS_SYNC_API);
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
        return Results.Ok(JsonSerializer.Serialize(mapResult, JsonUtils.Options));
    }
}
