using DevOps_Agent.Plugins;
using Docker.DotNet;
using Microsoft.SemanticKernel;
using Scalar.AspNetCore;
using System.Reflection;
using System.Runtime.InteropServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi(); // For API documentation

builder.Services.AddSingleton<IDockerClient>(provider =>
{
    var dockerUri = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "npipe://./pipe/docker_engine" // If the OS platform is Windows, use the named pipe for Docker.
        : "unix:///var/run/docker.sock"; // Else, use the Unix socket for Docker (common in Linux and macOS environments).

    return new DockerClientConfiguration(new Uri(dockerUri)).CreateClient(); // Use the Docker.DotNet library to create a Docker client that can communicate with the Docker daemon.
}); // For our case, we only need 1 instance to interact with the Docker API, so we can register it as a singleton.

builder.Services.AddKernel() // Add kernel is used to add a semantic kernel that handles AI services 
    .AddGoogleAIGeminiChatCompletion( // Since the API key used here is Gemini, we use the fitting creation function
        modelId: builder.Configuration["AI:ModelId"]!, 
        apiKey: builder.Configuration["AI:ApiKey"]!
    ) // Add neccassary info, can be found in appsettings.json
    .Plugins.AddFromType<DockerOperationsPlugin>(); // Allow the AI to interact with 

builder.Host.ConfigureAppConfiguration((context, configurationBuilder) =>
{
    configurationBuilder
        .AddUserSecrets(Assembly.GetExecutingAssembly())
        .AddEnvironmentVariables();
});

builder.Services.AddControllers();  

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapControllers();
app.Run();