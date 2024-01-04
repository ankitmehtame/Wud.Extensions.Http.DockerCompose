using System.Reflection;
using System.Text.Json;
using Semver;
namespace Wud.Extensions.Http.DockerCompose.WebApi;

public static class VersionUtils
{
    private static readonly Lazy<SemVersion> prodVersion = new (() =>
    {
        string versionFromAssembly = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        if (!SemVersion.TryParse(versionFromAssembly.Trim(), SemVersionStyles.Any, out var semverAssembly))
        {
            return SemVersion.FromVersion(new Version());
        }
        return semverAssembly;
    });

    private static readonly Lazy<SemVersion> localVersion = new (() =>
    {
        var versionFileLoc = File.Exists("version.json") ? "version.json" : File.Exists("../version.json") ? "../version.json" : null;
        if (versionFileLoc != null)
        {
            var fileText = File.ReadAllText(versionFileLoc);
            var versionFileContents = JsonSerializer.Deserialize<VersionFileVersion>(fileText, JsonUtils.Options);
            var versionFileVersion = versionFileContents?.Version;
            if (!string.IsNullOrWhiteSpace(versionFileVersion) && SemVersion.TryParse(versionFileVersion.Trim(), SemVersionStyles.Any, out var fileSemver))
            {
                return fileSemver;
            }
        }
        return prodVersion.Value;
    });

    private static readonly bool IsLocal = !string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Production", StringComparison.OrdinalIgnoreCase);

    public static readonly string InfoVersion = !IsLocal ? prodVersion.Value.ToString() : localVersion.Value.ToString();
    public static readonly string AssemblyVersion = !IsLocal ? prodVersion.Value.WithoutPrereleaseOrMetadata().ToVersion().ToString(3) : localVersion.Value.WithoutPrereleaseOrMetadata().ToVersion().ToString(3);

    private record VersionFileVersion(string Version);
}
