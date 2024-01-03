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
            { UpdateResult.UpdatedSuccessfully, Results.Json(new Dictionary<string, UpdateResult> {{"homeassistant_ha", UpdateResult.UpdatedSuccessfully}}, statusCode: StatusCodes.Status200OK) },
            { UpdateResult.AlreadyUpToDate, Results.Json(new Dictionary<string, UpdateResult> {{"homeassistant_ha", UpdateResult.AlreadyUpToDate}}, statusCode: StatusCodes.Status200OK ) },
            { UpdateResult.NoDockerFiles, Results.Json(new Dictionary<string, UpdateResult> {{"homeassistant_ha", UpdateResult.NoDockerFiles}}, statusCode: StatusCodes.Status404NotFound) },
            { UpdateResult.ContainerNotFound, Results.Json(new Dictionary<string, UpdateResult> {{"homeassistant_ha", UpdateResult.ContainerNotFound}}, statusCode: StatusCodes.Status404NotFound) },
            { UpdateResult.ImageNotFound, Results.Json(new Dictionary<string, UpdateResult> {{"homeassistant_ha", UpdateResult.ImageNotFound}}, statusCode: StatusCodes.Status404NotFound) },
            { UpdateResult.UnableToUpdateDockerFile, Results.Json(new Dictionary<string, UpdateResult> {{"homeassistant_ha", UpdateResult.UnableToUpdateDockerFile}}, statusCode: StatusCodes.Status500InternalServerError) },
        };
    }

    [Fact]
    public async Task ShouldHandleContainersSyncApiAllOkay()
    {
        await VerifyContainersSyncApi(UpdateResult.UpdatedSuccessfully, UpdateResult.AlreadyUpToDate, UpdateResult.UpdatedSuccessfully, UpdateResult.AlreadyUpToDate, StatusCodes.Status200OK);
    }
    
    [Fact]
    public async Task ShouldHandleContainersSyncApiPartialOkay()
    {
        await VerifyContainersSyncApi(UpdateResult.UpdatedSuccessfully, UpdateResult.AlreadyUpToDate, UpdateResult.ContainerNotFound, UpdateResult.ImageNotFound, StatusCodes.Status206PartialContent);
    }

    [Fact]
    public async Task ShouldHandleContainersSyncApiAllNotFound()
    {
        await VerifyContainersSyncApi(UpdateResult.ImageNotFound, UpdateResult.ContainerNotFound, UpdateResult.ContainerNotFound, UpdateResult.ImageNotFound, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ShouldHandleContainersSyncApiAllFailed()
    {
        await VerifyContainersSyncApi(UpdateResult.ImageNotFound, UpdateResult.ContainerNotFound, UpdateResult.UnableToUpdateDockerFile, UpdateResult.UnableToUpdateDockerFile, StatusCodes.Status500InternalServerError);
    }

    private async Task VerifyContainersSyncApi(UpdateResult container1Result, UpdateResult container2Result, UpdateResult container3Result, UpdateResult container4Result, int expectedStatusCode)
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

        dockerComposeUtility.UpdateDockerFileForContainer(container11).Returns(Task.FromResult(container1Result));
        dockerComposeUtility.UpdateDockerFileForContainer(container12).Returns(Task.FromResult(container2Result));
        dockerComposeUtility.UpdateDockerFileForContainer(container21).Returns(Task.FromResult(container3Result));
        dockerComposeUtility.UpdateDockerFileForContainer(container22).Returns(Task.FromResult(container4Result));
        
        var containersApi = new ContainerApis(logger: logger, dockerComposeUtility: dockerComposeUtility, wudService: wudService);
        
        var res = await containersApi.ContainersSyncApi();

        var expectation = Results.Json(new Dictionary<string, UpdateResult>
        {
            {"container11", container1Result},
            {"container12", container2Result},
            {"container21", container3Result},
            {"container22", container4Result}
        }, statusCode: expectedStatusCode);
        res.Should().BeEquivalentTo(expectation, op => op.RespectingRuntimeTypes());
    }

    private static WudContainer CreateContainer(string containerSequence)
    {
        return new (Id: $"dummy-id-{containerSequence}", Name: $"container{containerSequence}", Watcher: "dummy-watcher", UpdateAvailable: null, Image: new WudContainerImage(Id: "dummy-image-id", Name: "homeassistant/home-assistant", Tag: new WudImageTag(Value: "2023.12.4", Semver: true), Created: DateTimeOffset.Now), Result: null);
    }
}
