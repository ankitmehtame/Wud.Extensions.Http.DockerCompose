using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Wud.Extensions.Http.DockerCompose.WebApi;
using Wud.Extensions.Http.DockerCompose.WebApi.Tests;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo
{
    Version = VersionUtils.AssemblyVersion,
    Title = "WUD Extensions - Http Docker Compose",
    Description = "v" + VersionUtils.InfoVersion
}));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UpdateSerializerOptions();
});
builder.Services.AddHttpClient();
builder.Services.AddHangfire(c => c.UseMemoryStorage());
builder.Services.AddHangfireServer();
builder.Services.AddSingleton<IEnvironmentProvider, EnvironmentProvider>();
builder.Services.AddSingleton<IDockerComposeUtility, DockerComposeUtility>();
builder.Services.AddSingleton<IWudService, WudService>();
builder.Services.AddSingleton<ContainerApis>();
builder.Services.AddSingleton<BackgroundJobCaller>();
builder.Services.AddSingleton<IBackgroundJobHelper, BackgroundJobHelper>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost(Constants.Api.CONTAINER_NEW_VERSION_API, ([FromBody] WudContainer container, ContainerApis containerApis) => containerApis.ContainerNewVersionApi(container))
.WithName("Container-New-Version")
.WithOpenApi();

app.MapPost(Constants.Api.CONTAINERS_SYNC_API, (ContainerApis containerApis) => containerApis.ContainersSyncApi())
.WithName("Containers-Sync")
.WithOpenApi();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Starting v{version}", VersionUtils.InfoVersion);

var bgHelper = app.Services.GetRequiredService<IBackgroundJobHelper>();
await bgHelper.ScheduleRecurringBackgroundSyncJob();

app.Run();
