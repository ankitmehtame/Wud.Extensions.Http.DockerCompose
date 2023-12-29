namespace Wud.Extensions.Http.DockerCompose.WebApi;

public record WudContainer(string Id, string Name, string Watcher, bool? UpdateAvailable, WudContainerImage Image, WudResult? Result);

public record WudContainerImage(string Id, string? Name, WudImageTag Tag, DateTimeOffset Created);

public record WudImageTag(string Value, bool? Semver);

public record WudResult(string? Tag);
