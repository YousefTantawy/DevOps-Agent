using System.ComponentModel;
using Docker.DotNet;
using Microsoft.SemanticKernel;

namespace DevOps_Agent.Plugins;

public class DockerOperationsPlugin
{
    private readonly IDockerClient _dockerClient;

    public DockerOperationsPlugin(IDockerClient dockerClient)
    {
        _dockerClient = dockerClient;
    }

    [KernelFunction("get_container_status")]
    [Description("Gets the current running state of a specific Docker container. Use this to check if a container is running, exited, or dead.")]
    public async Task<string> GetContainerStatusAsync(
        [Description("The exact name of the Docker container to inspect")] string containerName)
    {
        try
        {
            var response = await _dockerClient.Containers.InspectContainerAsync(containerName);
            return $"Container '{containerName}' status: {response.State.Status}";
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return $"Error: Container '{containerName}' not found.";
        }
        catch (Exception ex)
        {
            return $"Error retrieving status: {ex.Message}";
        }
    }
}
