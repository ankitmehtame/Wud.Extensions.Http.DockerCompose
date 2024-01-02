namespace Wud.Extensions.Http.DockerCompose.WebApi;

public static class Constants
{
    public static class Api
    {
        public const string CONTAINERS_SYNC_API = "/containers-sync";
        public const string CONTAINER_NEW_VERSION_API = "/container-new-version";
    }
    public static class Env
    {
        public const string WUD_CONTAINERS_URL = "WUD_CONTAINERS_URL";
        public const string DOCKER_FILES_CSV = "DOCKER_FILES_CSV";
    }
}
