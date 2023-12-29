using YamlDotNet.RepresentationModel;

namespace Wud.Extensions.Http.DockerCompose.WebApi;

public class DockerComposeUtility(ILogger<DockerComposeUtility> logger, IEnvironmentProvider environmentProvider)
{
    public const string ENV_DOCKER_FILES_CSV = "DOCKER_FILES_CSV";

    private record DockerFilesEnv(int DockerFilesEnvHash, string[] PrevDockerFiles) { }

    private DockerFilesEnv? _prevDockerFiles = null;


    private IEnumerable<string> GetDockerFiles()
    {
        var envVar = environmentProvider.GetEnvironmentVariable(ENV_DOCKER_FILES_CSV) ?? string.Empty;
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

    private async Task<FindImageResult> GetDockerComposeImageValueForContainer(string dockerFile, string containerName)
    {
        await Task.Yield();
        using var fileStream = new FileStream(dockerFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        using var input = new StreamReader(fileStream);
        var yamlStream = new YamlStream();
        yamlStream.Load(input);
        var root = (YamlMappingNode)yamlStream.Documents[0].RootNode;
        var servicesNode = root.Children.FirstOrDefault(x => ((YamlScalarNode)x.Key).Value == "services");
        if (servicesNode.Value as YamlMappingNode == null)
        {
            logger.LogError("Unable to find services in {dockerFile}", dockerFile);
            return new FindImageResult(FindResult.ContainerNotFound, null, null);
        }
        var allServices = (YamlMappingNode)servicesNode.Value;

        foreach (var serviceNode in allServices.Children)
        {
            if (serviceNode.Value is not YamlMappingNode serviceChildren) continue;
            var serviceName = serviceNode.Key.ToString();
            var containerNameNode = serviceChildren.FirstOrDefault(n => (n.Key as YamlScalarNode)?.Value == "container_name");
            var containerNameValue = ((YamlScalarNode)containerNameNode.Value).Value;
            var isMatchingContainer = string.Equals(containerNameValue, containerName, StringComparison.OrdinalIgnoreCase);
            if (!isMatchingContainer) continue;

            var imageNode = serviceChildren.FirstOrDefault(n => (n.Key as YamlScalarNode)?.Value == "image");
            if (imageNode.Key == null)
            {
                logger.LogError("Unable to find image tag for {containerName} in docker file {dockerFile}", containerName, dockerFile);
                return new FindImageResult(FindResult.ImageNotFound, ServiceName: serviceName, ImageTagValue: null);
            }
            var imageNodeValue = (imageNode.Value as YamlScalarNode)?.Value ?? string.Empty;
            return new FindImageResult(FindResult.ImageFoundSuccessfully, ImageTagValue: imageNodeValue, ServiceName: serviceName);
        }
        return new FindImageResult(FindResult.ContainerNotFound, null, null);
    }


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
            if (imageNodeValue == expectedImageValue)
            {
                logger.LogDebug("Image tag of container {containerName} is already correct ({imageValue}) in docker compose file {dockerFile}", container.Name, expectedImageValue, dockerFile);
                return UpdateResult.AlreadyUpToDate;
            }
            // Update image tag
            var newFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.yaml");
            logger.LogDebug("Writing to temp file {file}", newFile);
            using(var outStream = new StreamWriter(newFile))
            {
                var foundService = false;
                await foreach(var line in File.ReadLinesAsync(dockerFile))
                {
                    var trimmedLine = line.TrimStart();
                    var serviceName = imageFindResult.ServiceName;
                    foundService = foundService || trimmedLine.StartsWith($"{serviceName}:");
                    var newLine = foundService && trimmedLine.StartsWith($"image: {imageNodeValue}")
                        ? line.Replace($"image: {imageNodeValue}", $"image: {expectedImageValue}")
                        : line;
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
                    File.Move(newFile, dockerFile, true);
                }
                catch (Exception ex)
                {
                    logger.LogError("Unable to write to docker compose file {dockerFile} - {ex}", dockerFile, (ex.GetBaseException() ?? ex).Message);
                    return UpdateResult.UnableToUpdateDockerFile;
                }
                return UpdateResult.UpdatedSuccessfully;
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
