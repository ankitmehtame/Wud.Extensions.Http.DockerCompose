using System.Reflection;
using Semver;
namespace Wud.Extensions.Http.DockerCompose.WebApi;

public static class VersionUtils
{
    private static readonly Lazy<SemVersion> _version = new Lazy<SemVersion>(() =>
    {
        string versionFileText = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        if (!SemVersion.TryParse(versionFileText.Trim(), SemVersionStyles.Any, out var semverFile))
        {
            return SemVersion.FromVersion(new Version());
        }
        return semverFile;   
    });

    public static readonly string InfoVersion = _version.Value.ToString();
    public static readonly string AssemblyVersion = _version.Value.WithoutPrereleaseOrMetadata().ToVersion().ToString(3);
}
