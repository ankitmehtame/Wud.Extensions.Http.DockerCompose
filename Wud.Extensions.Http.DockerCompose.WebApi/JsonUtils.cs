using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wud.Extensions.Http.DockerCompose.WebApi;

public static class JsonUtils
{
    private static readonly Lazy<JsonSerializerOptions> lazyOptions = new Lazy<JsonSerializerOptions>(() =>
    {
        var options = new JsonSerializerOptions();
        UpdateSerializerOptions(options);
        return options;
    });

    public static void UpdateSerializerOptions(this JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    }
    
    public static readonly JsonSerializerOptions Options = lazyOptions.Value;

    public static string ToJson<T>(this T item)
    {
        return JsonSerializer.Serialize(item, Options);
    }
}
