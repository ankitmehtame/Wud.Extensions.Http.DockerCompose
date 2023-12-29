using System.Reflection;

namespace Wud.Extensions.Http.DockerCompose.WebApi;

public static class AssemblyUtils
{
    public static string InfoVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
    public static string AssemblyVersion = Assembly.GetEntryAssembly().GetName().Version.ToString(3);
}
