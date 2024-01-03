using FluentAssertions;
using NSubstitute;
using Xunit.Abstractions;

namespace Wud.Extensions.Http.DockerCompose.WebApi.Tests;

public class DockerComposeUtilityTests(ITestOutputHelper outputHelper) : IDisposable
{
    private readonly List<string> tempFiles = [];
    private readonly WudContainer container = CreateContainer(containerName: "homeassistant_ha", imageName: "homeassistant/home-assistant", imageTag: "2023.12.4");
    private readonly WudContainer containerDummyImage = CreateContainer(containerName: "homeassistant_ha", imageName: "dummy-image", imageTag: "2023.12.4");

    [Fact]
    public async Task ShouldUpdateDockerFileForContainer()
    {
        var envProvider = Substitute.For<IEnvironmentProvider>();
        var utility = new DockerComposeUtility(logger: outputHelper.ToLogger<DockerComposeUtility>(), environmentProvider: envProvider);
        const string origFile = """Files/DummyDockerCompose01.yaml""";
        var tempFile = GetNewTempFilePath($"DockerCompose01-{Guid.NewGuid()}.yaml");
        File.Copy(origFile, tempFile, overwrite: true);
        envProvider.GetEnvironmentVariable(Constants.Env.DOCKER_FILES_CSV).Returns(tempFile);
        var result = await utility.UpdateDockerFileForContainer(container);
        result.Should().Be(UpdateResult.UpdatedSuccessfully);
        var updatedLines = await File.ReadAllLinesAsync(tempFile);
        var origLines = await File.ReadAllLinesAsync(origFile);
        updatedLines.Length.Should().Be(origLines.Length);
        for (var lineIndex = 0; lineIndex < origLines.Length - 1; lineIndex++)
        {
            var origLine = origLines[lineIndex].TrimEnd();
            var updatedLine = updatedLines[lineIndex].TrimEnd();
            if (origLine.Trim() == "image: homeassistant/home-assistant:2023.12")
            {
                updatedLine.Trim().Should().Be("image: homeassistant/home-assistant:2023.12.4", "Line number {0}", lineIndex + 1);
            }
            else
            {
                updatedLine.Should().Be(origLine, "Line number {0}", lineIndex + 1);
            }
        }
    }

    [Fact]
    public async Task ShouldUpdateDockerFileForContainerWhichHasComment()
    {
        var envProvider = Substitute.For<IEnvironmentProvider>();
        var utility = new DockerComposeUtility(logger: outputHelper.ToLogger<DockerComposeUtility>(), environmentProvider: envProvider);
        const string origFile = """Files/DummyDockerCompose02.yaml""";
        var tempFile = GetNewTempFilePath($"DockerCompose02-{Guid.NewGuid()}.yaml");
        File.Copy(origFile, tempFile, overwrite: true);
        envProvider.GetEnvironmentVariable(Constants.Env.DOCKER_FILES_CSV).Returns(tempFile);
        var result = await utility.UpdateDockerFileForContainer(container);
        result.Should().Be(UpdateResult.UpdatedSuccessfully);
        var updatedLines = await File.ReadAllLinesAsync(tempFile);
        var origLines = await File.ReadAllLinesAsync(origFile);
        (updatedLines.FirstOrDefault(l => l.Trim().StartsWith("image: homeassistant/home-assistant:"))?.Trim()).Should().Be("image: homeassistant/home-assistant:2023.12.4 #my-comment");
        updatedLines.Length.Should().Be(origLines.Length);
        for (var lineIndex = 0; lineIndex < origLines.Length - 1; lineIndex++)
        {
            var origLine = origLines[lineIndex].TrimEnd();
            var updatedLine = updatedLines[lineIndex].TrimEnd();
            if (origLine.Trim() == "image: homeassistant/home-assistant:2023.12 #my-comment")
            {
                updatedLine.Trim().Should().Be("image: homeassistant/home-assistant:2023.12.4 #my-comment", "Line number {0}", lineIndex + 1);
            }
            else
            {
                updatedLine.Should().Be(origLine, "Line number {0}", lineIndex + 1);
            }
        }
    }
    
    [Fact]
    public async Task ShouldUpdateDockerFileForContainerWhichHasSameImageInMultipleContainers()
    {
        var envProvider = Substitute.For<IEnvironmentProvider>();
        var utility = new DockerComposeUtility(logger: outputHelper.ToLogger<DockerComposeUtility>(), environmentProvider: envProvider);
        const string origFile = """Files/DummyDockerCompose03.yaml""";
        var tempFile = GetNewTempFilePath($"DockerCompose03-{Guid.NewGuid()}.yaml");
        File.Copy(origFile, tempFile, overwrite: true);
        envProvider.GetEnvironmentVariable(Constants.Env.DOCKER_FILES_CSV).Returns(tempFile);
        var result = await utility.UpdateDockerFileForContainer(containerDummyImage);
        result.Should().Be(UpdateResult.UpdatedSuccessfully);
        var updatedLines = await File.ReadAllLinesAsync(tempFile);
        var origLines = await File.ReadAllLinesAsync(origFile);
        updatedLines.Length.Should().Be(origLines.Length);
        for (var lineIndex = 0; lineIndex < origLines.Length - 1; lineIndex++)
        {
            var origLine = origLines[lineIndex].TrimEnd();
            var updatedLine = updatedLines[lineIndex].TrimEnd();            
            if (origLine.Trim().StartsWith("image: dummy-image:2023.12 #my-comment"))
            {
                updatedLine.Trim().Should().Be("image: dummy-image:2023.12.4 #my-comment", "Line number {0}", lineIndex + 1);
            }
            else
            {
                updatedLine.Should().Be(origLine, "Line number {0}", lineIndex + 1);
            }
        }
    }

    [Fact]
    public async Task ShouldUpdateDockerFileForContainerWithQuotes()
    {
        var envProvider = Substitute.For<IEnvironmentProvider>();
        var utility = new DockerComposeUtility(logger: outputHelper.ToLogger<DockerComposeUtility>(), environmentProvider: envProvider);
        const string origFile = """Files/DummyDockerCompose01.yaml""";
        var tempFile = GetNewTempFilePath($"DockerCompose01-{Guid.NewGuid()}.yaml");
        File.Copy(origFile, tempFile, overwrite: true);
        envProvider.GetEnvironmentVariable(Constants.Env.DOCKER_FILES_CSV).Returns(tempFile);

        var container = CreateContainer(containerName: "homeassistant_db", imageName: "bitnami/postgresql", imageTag: "14.1");

        var result = await utility.UpdateDockerFileForContainer(container);
        result.Should().Be(UpdateResult.UpdatedSuccessfully);
        var updatedLines = await File.ReadAllLinesAsync(tempFile);
        var origLines = await File.ReadAllLinesAsync(origFile);
        updatedLines.Length.Should().Be(origLines.Length);
        for (var lineIndex = 0; lineIndex < origLines.Length - 1; lineIndex++)
        {
            var origLine = origLines[lineIndex].TrimEnd();
            var updatedLine = updatedLines[lineIndex].TrimEnd();
            if (origLine.Trim() == "image: 'bitnami/postgresql:14'")
            {
                updatedLine.Trim().Should().Be("image: 'bitnami/postgresql:14.1'", "Line number {0}", lineIndex + 1);
            }
            else
            {
                updatedLine.Should().Be(origLine, "Line number {0}", lineIndex + 1);
            }
        }
    }

    [Fact]
    public async Task ShouldNotUpdateDockerFileForContainerWhichIsStable()
    {
        var envProvider = Substitute.For<IEnvironmentProvider>();
        var utility = new DockerComposeUtility(logger: outputHelper.ToLogger<DockerComposeUtility>(), environmentProvider: envProvider);
        const string origFile = """Files/DummyDockerCompose02.yaml""";
        var tempFile = GetNewTempFilePath($"DockerCompose02-{Guid.NewGuid()}.yaml");
        File.Copy(origFile, tempFile, overwrite: true);
        envProvider.GetEnvironmentVariable(Constants.Env.DOCKER_FILES_CSV).Returns(tempFile);
        
        var container1 = CreateContainer(containerName: "zigbee2mqtt", imageName: "koenkk/zigbee2mqtt", imageTag: "latest");
        var result1 = await utility.UpdateDockerFileForContainer(container1);
        result1.Should().Be(UpdateResult.AlreadyUpToDate);

        var container2 = CreateContainer(containerName: "esphome", imageName: "esphome/esphome", imageTag: "stable");
        var result2 = await utility.UpdateDockerFileForContainer(container1);
        result2.Should().Be(UpdateResult.AlreadyUpToDate);

        var updatedLines = await File.ReadAllLinesAsync(tempFile);
        var origLines = await File.ReadAllLinesAsync(origFile);
        updatedLines.Should().BeEquivalentTo(origLines, o => o.WithStrictOrdering());
    }

    //homeassistant_db


    [Fact]
    public async Task ShouldGetContainerNamesFromDockerFile()
    {
        var envProvider = Substitute.For<IEnvironmentProvider>();
        var utility = new DockerComposeUtility(logger: outputHelper.ToLogger<DockerComposeUtility>(), environmentProvider: envProvider);
        const string origFile = """Files/DummyDockerCompose02.yaml""";
        var tempFile = GetNewTempFilePath($"DockerCompose03-{Guid.NewGuid()}.yaml");
        File.Copy(origFile, tempFile, overwrite: true);
        envProvider.GetEnvironmentVariable(Constants.Env.DOCKER_FILES_CSV).Returns(tempFile);

        var containers = await utility.GetContainerNamesFromDockerFile(tempFile);
        containers.Should().BeEquivalentTo(["zigbee2mqtt", "homeassistant_db", "homeassistant_ha", "esphome"]);
    }

    private string GetNewTempFilePath(string fileName)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), fileName);
        tempFiles.Add(tempFile);
        return tempFile;
    }

    private static WudContainer CreateContainer(string containerName, string imageName, string imageTag)
    {
        return new (Id: $"dummy-id-{containerName}", Name: containerName, Watcher: "dummy-watcher", UpdateAvailable: null, Image: new WudContainerImage(Id: "dummy-image-id", Name: imageName, Tag: new WudImageTag(Value: imageTag, Semver: true), Created: DateTimeOffset.Now), Result: null);
    }

    public void Dispose()
    {
        tempFiles.ForEach(tempFile =>
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                    outputHelper.WriteLine("Deleted temp file {tempFile}", tempFile);
                }
            }
            catch (Exception)
            {
                if (File.Exists(tempFile))
                {
                    outputHelper.WriteLine("Unable to deleted temp file {tempFile}", tempFile);
                }
            }
        });
        GC.SuppressFinalize(this);
    }
}
