namespace Wud.Extensions.Http.DockerCompose.WebApi;

public class WudService(ILogger<WudService> logger, IHttpClientFactory httpClientFactory, IEnvironmentProvider environmentProvider) : IWudService
{
    public async Task<WudContainer[]> GetContainers()
    {
        var wudContainersUrl = environmentProvider.GetEnvironmentVariable(Constants.Env.WUD_CONTAINERS_URL);
        try
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(wudContainersUrl, "Environment variable " + Constants.Env.WUD_CONTAINERS_URL);
            var httpClient = httpClientFactory.CreateClient("wud-containers");
            return (await httpClient.GetFromJsonAsync<WudContainer[]>(wudContainersUrl)) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError("Unable to get WUD containers from {wudContainersUrl} - {ex}", wudContainersUrl, ex.Cause());
            throw;
        }
    }
}

public interface IWudService
{
    Task<WudContainer[]> GetContainers();
}
