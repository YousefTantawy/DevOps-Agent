using DevOps_Agent.Plugins;
using Docker.DotNet;
using Microsoft.SemanticKernel;
using Scalar.AspNetCore;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddSingleton<IDockerClient>(provider =>
{
    var dockerUri = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "npipe://./pipe/docker_engine"
        : "unix:///var/run/docker.sock";

    return new DockerClientConfiguration(new Uri(dockerUri)).CreateClient();
});

builder.Services.AddKernel()
    .AddGoogleAIGeminiChatCompletion(
        modelId: builder.Configuration["AI:ModelId"]!,
        apiKey: builder.Configuration["AI:ApiKey"]!
    )
    .Plugins.AddFromType<DockerOperationsPlugin>();

builder.Services.AddControllers();

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapControllers();
app.Run();