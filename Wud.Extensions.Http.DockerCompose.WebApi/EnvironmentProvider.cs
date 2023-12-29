namespace Wud.Extensions.Http.DockerCompose.WebApi;

public class EnvironmentProvider : IEnvironmentProvider
{
    public string? GetEnvironmentVariable(string variable)
    {
        return Environment.GetEnvironmentVariable(variable);
    }
}

public interface IEnvironmentProvider
{
    string? GetEnvironmentVariable(string variable);
}
