using FluentAssertions;
using NSubstitute;
using Xunit.Abstractions;

namespace Wud.Extensions.Http.DockerCompose.WebApi.Tests;

public class DockerComposeUtilityTests(ITestOutputHelper outputHelper)
{

    private WudContainer container = new (Id: "dummy-id", Name: "homeassistant_ha", Watcher: "dummy-watcher", UpdateAvailable: null, Image: new WudContainerImage(Id: "dummy-image-id", Name: "homeassistant/home-assistant", Tag: new WudImageTag(Value: "2023.12.4", Semver: true), Created: DateTimeOffset.Now), Result: null);
    private WudContainer containerDummyImage = new (Id: "dummy-id", Name: "homeassistant_ha", Watcher: "dummy-watcher", UpdateAvailable: null, Image: new WudContainerImage(Id: "dummy-image-id", Name: "dummy-image", Tag: new WudImageTag(Value: "2023.12.4", Semver: true), Created: DateTimeOffset.Now), Result: null);

    [Fact]
    public async Task ShouldUpdateDockerFileForContainer()
    {
        var envProvider = Substitute.For<IEnvironmentProvider>();
        var utility = new DockerComposeUtility(logger: outputHelper.ToLogger<DockerComposeUtility>(), environmentProvider: envProvider);
        const string origFile = """Files/DummyDockerCompose01.yaml""";
        var tempFile = Path.Combine(Path.GetTempPath(), $"DockerCompose01-{Guid.NewGuid()}.yaml");
        File.Copy(origFile, tempFile, overwrite: true);
        envProvider.GetEnvironmentVariable(DockerComposeUtility.ENV_DOCKER_FILES_CSV).Returns(tempFile);
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"DockerCompose02-{Guid.NewGuid()}.yaml");
        File.Copy(origFile, tempFile, overwrite: true);
        envProvider.GetEnvironmentVariable(DockerComposeUtility.ENV_DOCKER_FILES_CSV).Returns(tempFile);
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
        var tempFile = Path.Combine(Path.GetTempPath(), $"DockerCompose03-{Guid.NewGuid()}.yaml");
        File.Copy(origFile, tempFile, overwrite: true);
        envProvider.GetEnvironmentVariable(DockerComposeUtility.ENV_DOCKER_FILES_CSV).Returns(tempFile);
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
}
