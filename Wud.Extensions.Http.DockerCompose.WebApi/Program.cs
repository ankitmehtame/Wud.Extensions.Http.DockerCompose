using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Wud.Extensions.Http.DockerCompose.WebApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = AssemblyUtils.AssemblyVersion,
                    Title = "http forwarder app",
                    Description = "v" + AssemblyUtils.InfoVersion
                }));
builder.Services.AddSingleton<IEnvironmentProvider, EnvironmentProvider>();
builder.Services.AddSingleton<DockerComposeUtility>();

var app = builder.Build();

// if (app.Environment.IsDevelopment())
// {
    app.UseSwagger();
    app.UseSwaggerUI();
// }

app.UseHttpsRedirection();

const string CONTAINER_NEW_VERSION_API = "/container-new-version";
app.MapPost(CONTAINER_NEW_VERSION_API, async ([FromBody] WudContainer container, ILogger<Program> logger, DockerComposeUtility dockerComposeUtility) =>
{
    logger.LogInformation("{api} called - {container}", CONTAINER_NEW_VERSION_API, container);
    var res = await dockerComposeUtility.UpdateDockerFileForContainer(container);
    return res switch
    {
        UpdateResult.ContainerNotFound => Results.NotFound(res.ToString()),
        UpdateResult.ImageNotFound => Results.NotFound(res.ToString()),
        UpdateResult.NoDockerFiles => Results.NotFound(res.ToString()),
        UpdateResult.AlreadyUpToDate => Results.Ok(res.ToString()),
        UpdateResult.UpdatedSuccessfully => Results.Ok(res.ToString()),
        UpdateResult.UnableToUpdateDockerFile => Results.Problem(res.ToString()),
        _ => throw new NotSupportedException(res.ToString())
    };
})
.WithName("Container-New-Version")
.WithOpenApi();

app.Run();
