using FluentAssertions;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit.Abstractions;

namespace Wud.Extensions.Http.DockerCompose.WebApi.Tests;

public class ContainerApisTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [MemberData(nameof(ContainerNewVersionApiResults))]
    public async Task ShouldHandleContainerNewVersionApi(UpdateResult updateResult, IResult expectation)
    {
        var envProvider = Substitute.For<IEnvironmentProvider>();
        var logger = outputHelper.ToLogger<ContainerApis>();
        var wudService = Substitute.For<IWudService>();
        var dockerComposeUtility = Substitute.For<IDockerComposeUtility>();
        WudContainer container = new (Id: "dummy-id", Name: "homeassistant_ha", Watcher: "dummy-watcher", UpdateAvailable: null, Image: new WudContainerImage(Id: "dummy-image-id", Name: "homeassistant/home-assistant", Tag: new WudImageTag(Value: "2023.12.4", Semver: true), Created: DateTimeOffset.Now), Result: null);
        dockerComposeUtility.UpdateDockerFileForContainer(container).Returns(updateResult);
        var containersApi = new ContainerApis(logger: logger, dockerComposeUtility: dockerComposeUtility, wudService: wudService);
        
        var res = await containersApi.ContainerNewVersionApi(container);

        res.Should().BeEquivalentTo(expectation, op => op.RespectingRuntimeTypes());
    }

    public static TheoryData<UpdateResult, IResult> ContainerNewVersionApiResults() {
        return new TheoryData<UpdateResult, IResult>
        {
            { UpdateResult.UpdatedSuccessfully, Results.Ok("""{"homeassistant_ha":"UpdatedSuccessfully"}""") },
            { UpdateResult.AlreadyUpToDate, Results.Ok("""{"homeassistant_ha":"AlreadyUpToDate"}""") },
            { UpdateResult.NoDockerFiles, Results.NotFound("""{"homeassistant_ha":"NoDockerFiles"}""") },
            { UpdateResult.ContainerNotFound, Results.NotFound("""{"homeassistant_ha":"ContainerNotFound"}""") },
            { UpdateResult.ImageNotFound, Results.NotFound("""{"homeassistant_ha":"ImageNotFound"}""") },
            { UpdateResult.UnableToUpdateDockerFile, Results.Problem("""{"homeassistant_ha":"UnableToUpdateDockerFile"}""") },
        };
    }

    [Fact]
    public async Task ShouldHandleContainersSyncApi()
    {
        var envProvider = Substitute.For<IEnvironmentProvider>();
        var logger = outputHelper.ToLogger<ContainerApis>();
        var wudService = Substitute.For<IWudService>();
        var dockerComposeUtility = Substitute.For<IDockerComposeUtility>();
        var container01 = CreateContainer("01");
        var container02 = CreateContainer("02");
        var container11 = CreateContainer("11");
        var container12 = CreateContainer("12");
        var container21 = CreateContainer("21");
        var container22 = CreateContainer("22");
        var container31 = CreateContainer("31");
        var container32 = CreateContainer("32");

        dockerComposeUtility.GetDockerFiles().Returns(["file1", "file2"]);
        dockerComposeUtility.GetContainerNamesFromDockerFile("file1").Returns(Task.FromResult<string[]>(["container11", "container12"]));
        dockerComposeUtility.GetContainerNamesFromDockerFile("file2").Returns(Task.FromResult<string[]>(["container21", "container22"]));

        wudService.GetContainers().Returns(Task.FromResult<WudContainer[]>([container01, container02, container11, container12, container21, container22, container31, container32]));

        dockerComposeUtility.UpdateDockerFileForContainer(container11).Returns(Task.FromResult(UpdateResult.UpdatedSuccessfully));
        dockerComposeUtility.UpdateDockerFileForContainer(container12).Returns(Task.FromResult(UpdateResult.AlreadyUpToDate));
        dockerComposeUtility.UpdateDockerFileForContainer(container21).Returns(Task.FromResult(UpdateResult.UpdatedSuccessfully));
        dockerComposeUtility.UpdateDockerFileForContainer(container22).Returns(Task.FromResult(UpdateResult.AlreadyUpToDate));
        
        var containersApi = new ContainerApis(logger: logger, dockerComposeUtility: dockerComposeUtility, wudService: wudService);
        
        var res = await containersApi.ContainersSyncApi();

        var expectation = Results.Ok("""{"container11":"UpdatedSuccessfully","container12":"AlreadyUpToDate","container21":"UpdatedSuccessfully","container22":"AlreadyUpToDate"}""");
        res.Should().BeEquivalentTo(expectation, op => op.RespectingRuntimeTypes());
    }

    private static WudContainer CreateContainer(string containerSequence)
    {
        return new (Id: $"dummy-id-{containerSequence}", Name: $"container{containerSequence}", Watcher: "dummy-watcher", UpdateAvailable: null, Image: new WudContainerImage(Id: "dummy-image-id", Name: "homeassistant/home-assistant", Tag: new WudImageTag(Value: "2023.12.4", Semver: true), Created: DateTimeOffset.Now), Result: null);
    }
}
