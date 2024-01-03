using System.Runtime.ExceptionServices;
using YamlDotNet.RepresentationModel;

namespace Wud.Extensions.Http.DockerCompose.WebApi;

public class DockerComposeUtility(ILogger<DockerComposeUtility> logger, IEnvironmentProvider environmentProvider) : IDockerComposeUtility
{
    private record DockerFilesEnv(int DockerFilesEnvHash, string[] PrevDockerFiles) { }

    private DockerFilesEnv? _prevDockerFiles = null;


    public IEnumerable<string> GetDockerFiles()
    {
        var envVar = environmentProvider.GetEnvironmentVariable(Constants.Env.DOCKER_FILES_CSV) ?? string.Empty;
        var envHash = envVar.GetHashCode();
        var prev = _prevDockerFiles;
        var isPrevValid = prev != null && prev.DockerFilesEnvHash == envHash;
        var dockerFiles = isPrevValid ? prev!.PrevDockerFiles : envVar.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (!isPrevValid)
            Interlocked.Exchange(ref _prevDockerFiles, new DockerFilesEnv(envHash, dockerFiles!));
        foreach (var dockerFile in dockerFiles)
        {
            if (string.IsNullOrWhiteSpace(dockerFile)) continue;
            if (!Path.Exists(dockerFile)) logger.LogWarning("Docker file {dockerFile} does not exist", dockerFile);
            yield return dockerFile;
        }
    }

    private enum FindResult
    {
        ContainerNotFound,
        ImageNotFound,
        ImageFoundSuccessfully,
    }

    private record FindImageResult(FindResult Result, string? ImageTagValue, string? ServiceName);

    private record ServiceNodeAction(YamlMappingNode ServiceNode, string ServiceName, string? ContainerName);

    private async Task<bool> IterateDockerComposeServices(string dockerFile, Func<ServiceNodeAction, Task<bool>> serviceAction)
    {
        await Task.Yield();
        logger.LogDebug("Opening file {file} for reading", dockerFile);
        using var fileStream = new FileStream(dockerFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        using var input = new StreamReader(fileStream);
        var yamlStream = new YamlStream();
        yamlStream.Load(input);
        var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;
        var servicesNode = root.Children.FirstOrDefault(x => ((YamlScalarNode)x.Key).Value == "services");
        if (servicesNode.Value as YamlMappingNode == null)
        {
            logger.LogError("Unable to find services in {dockerFile}", dockerFile);
            return false;
        }
        var allServices = (YamlMappingNode)servicesNode.Value;

        foreach (var serviceNode in allServices.Children)
        {
            if (serviceNode.Value is not YamlMappingNode serviceChildren) continue;
            var serviceName = serviceNode.Key.ToString();
            var containerNameNode = serviceChildren.FirstOrDefault(n => (n.Key as YamlScalarNode)?.Value == "container_name");
            var containerNameValue = ((YamlScalarNode)containerNameNode.Value).Value;
            var shouldContinue = await serviceAction.Invoke(new ServiceNodeAction(serviceChildren, ServiceName: serviceName, ContainerName: containerNameValue));
            if (!shouldContinue) break;
        }
        fileStream.Close();
        logger.LogDebug("Closed file {file}", dockerFile);
        return true;
    }

    private async Task<FindImageResult> GetDockerComposeImageValueForContainer(string dockerFile, string containerName)
    {
        ServiceNodeAction? matchingContainerService = null;
        var hasValidServices = await IterateDockerComposeServices(dockerFile, x =>
        {
            var isMatchingContainer = string.Equals(x.ContainerName, containerName, StringComparison.OrdinalIgnoreCase);
            if (!isMatchingContainer) return Task.FromResult(true);
            matchingContainerService = x;
            return Task.FromResult(false);
        });
        if (!hasValidServices)
        {
            logger.LogError("Unable to find services in {dockerFile}", dockerFile);
            return new FindImageResult(FindResult.ContainerNotFound, null, null);
        }
        if (matchingContainerService == null)
        {
            return new FindImageResult(FindResult.ContainerNotFound, null, null);
        }
        var imageNode = matchingContainerService.ServiceNode.FirstOrDefault(n => (n.Key as YamlScalarNode)?.Value == "image");
        if (imageNode.Key == null)
        {
            logger.LogError("Unable to find image tag for {containerName} in docker file {dockerFile}", containerName, dockerFile);
            return new FindImageResult(FindResult.ImageNotFound, ServiceName: matchingContainerService.ServiceName, ImageTagValue: null);
        }
        var imageNodeValue = (imageNode.Value as YamlScalarNode)?.Value ?? string.Empty;
        return new FindImageResult(FindResult.ImageFoundSuccessfully, ImageTagValue: imageNodeValue, ServiceName: matchingContainerService.ServiceName);
    }

    public async Task<string[]> GetContainerNamesFromDockerFile(string dockerFile)
    {
        List<string> containerNames = [];
        var hasValidServices = await IterateDockerComposeServices(dockerFile, x =>
        {
            if (!string.IsNullOrWhiteSpace(x.ContainerName)) containerNames.Add(x.ContainerName);
            return Task.FromResult(true);
        });
        if (!hasValidServices) return [];
        return [.. containerNames];
    }

    private static readonly HashSet<string> NoImageTagVersions = ["latest"];


    public async Task<UpdateResult> UpdateDockerFileForContainer(WudContainer container)
    {
        var dockerFiles = GetDockerFiles();
        List<string> listDockerFiles = new();
        foreach (var dockerFile in dockerFiles)
        {
            listDockerFiles.Add(dockerFile);
            var imageFindResult = await GetDockerComposeImageValueForContainer(dockerFile, container.Name);
            if (imageFindResult.Result == FindResult.ContainerNotFound) continue;
            if (imageFindResult.Result == FindResult.ImageNotFound)
            {
                return UpdateResult.ImageNotFound;
            }
            var imageNodeValue = imageFindResult.ImageTagValue;
            var expectedImageValue = $"{container.Image.Name}:{container.Image.Tag.Value}";
            var imageTagVersion = (imageNodeValue ?? string.Empty).Split(':', StringSplitOptions.TrimEntries).ElementAtOrDefault(1) ?? string.Empty;
            logger.LogDebug("Found image tag version {imageTagVersion}, expected {expectedImageTagVersion}", imageTagVersion, container.Image.Tag.Value);
            if (imageTagVersion == container.Image.Tag.Value || (string.IsNullOrEmpty(imageTagVersion) && NoImageTagVersions.Contains(container.Image.Tag.Value)))
            {
                logger.LogDebug("Image tag of container {containerName} is already correct ({imageValue}) in docker compose file {dockerFile}", container.Name, expectedImageValue, dockerFile);
                return UpdateResult.AlreadyUpToDate;
            }
            // Update image tag
            var newFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.yaml");
            logger.LogDebug("Writing to temp file {file}", newFile);
            logger.LogDebug("Updating image with {expectedImageValue}", expectedImageValue);
            var updated = false;
            using (var outStream = new StreamWriter(newFile))
            {
                var foundService = false;
                await foreach (var line in File.ReadLinesAsync(dockerFile))
                {
                    var trimmedLine = line.TrimStart();
                    var serviceName = imageFindResult.ServiceName;
                    foundService = foundService || trimmedLine.StartsWith($"{serviceName}:");
                    string newLine;
                    var possibility1Line = new { Original = $"image: {imageNodeValue}", New = $"image: {expectedImageValue}" };
                    var possibility2Line = new { Original = $"image: '{imageNodeValue}'", New = $"image: '{expectedImageValue}'" };
                    var possibility3Line = new { Original = $"image: \"{imageNodeValue}\"", New = $"image: \"{expectedImageValue}\"" };
                    if (foundService && (trimmedLine.StartsWith(possibility1Line.Original) || trimmedLine.StartsWith(possibility2Line.Original) || trimmedLine.StartsWith(possibility3Line.Original)))
                    {
                        newLine = line
                                    .Replace(possibility1Line.Original, possibility1Line.New)
                                    .Replace(possibility2Line.Original, possibility2Line.New)
                                    .Replace(possibility3Line.Original, possibility3Line.New);
                        updated = true;
                    }
                    else
                    {
                        newLine = line;
                    }
                    outStream.WriteLine(newLine);
                }
                await outStream.FlushAsync();
            }
            var updatedImageNodeValue = await GetDockerComposeImageValueForContainer(newFile, container.Name);
            if (updatedImageNodeValue.Result == FindResult.ImageFoundSuccessfully && updatedImageNodeValue.ImageTagValue == expectedImageValue)
            {
                logger.LogDebug("Replacing original file with new one");
                try
                {
                    FileUtils.Move(newFile, dockerFile, true);
                }
                catch (Exception ex)
                {
                    logger.LogError("Unable to write to docker compose file {dockerFile} - {ex}", dockerFile, ex.Cause());
                    return UpdateResult.UnableToUpdateDockerFile;
                }
                return UpdateResult.UpdatedSuccessfully;
            }
            if (!updated)
            {
                logger.LogWarning("Unable to find the correct image tag to update for {container} in {dockerFile}. Intended to update from {current} to {imageTagVersion}", container.Name, dockerFile, imageTagVersion, container.Image.Tag.Value);
            }
            return UpdateResult.UnableToUpdateDockerFile;
        }
        if (listDockerFiles.Count == 0)
        {
            logger.LogError("No docker files found");
            return UpdateResult.NoDockerFiles;
        }
        logger.LogError("Unable to find container {container} in docker files {listDockerFiles}", container.Name, listDockerFiles);
        return UpdateResult.ContainerNotFound;
    }
}

public enum UpdateResult
{
    NoDockerFiles,
    ContainerNotFound,
    ImageNotFound,
    UnableToUpdateDockerFile,
    AlreadyUpToDate,
    UpdatedSuccessfully,
}

public interface IDockerComposeUtility
{
    IEnumerable<string> GetDockerFiles();
    Task<string[]> GetContainerNamesFromDockerFile(string dockerFile);
    Task<UpdateResult> UpdateDockerFileForContainer(WudContainer container);
}
