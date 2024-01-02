using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Wud.Extensions.Http.DockerCompose.WebApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo
{
    Version = AssemblyUtils.AssemblyVersion,
    Title = "WUD Extensions - Http Docker Compose",
    Description = "v" + AssemblyUtils.InfoVersion
}));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UpdateSerializerOptions();
});
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IEnvironmentProvider, EnvironmentProvider>();
builder.Services.AddSingleton<IDockerComposeUtility, DockerComposeUtility>();
builder.Services.AddSingleton<IWudService, WudService>();
builder.Services.AddSingleton<ContainerApis>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapPost(Constants.Api.CONTAINER_NEW_VERSION_API, ([FromBody] WudContainer container, ContainerApis containerApis) => containerApis.ContainerNewVersionApi(container))
.WithName("Container-New-Version")
.WithOpenApi();

app.MapPost(Constants.Api.CONTAINERS_SYNC_API, (ContainerApis containerApis) => containerApis.ContainersSyncApi())
.WithName("Containers-Sync")
.WithOpenApi();

app.Run();
