using System.ComponentModel;
using System.Text;
using DevOps_Agent.Configuration;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace DevOps_Agent.Plugins;

public class DockerOperationsPlugin
{
    private readonly IDockerClient _dockerClient;
    private readonly List<string> _restartAllowlist; // Containers the agent is permitted to restart

    public DockerOperationsPlugin(IDockerClient dockerClient, IOptions<DockerOptions> dockerOptions)
    {
        _dockerClient = dockerClient;
        _restartAllowlist = dockerOptions.Value.RestartAllowlist; // Pulled from the "Docker" config section
    }

    [KernelFunction("get_container_status")] // Name of the plugin for the kernel to call
    [Description("Gets the current running state of a specific Docker container. Use this to check if a container is running, exited, or dead.")]
    public async Task<string> GetContainerStatusAsync(
        [Description("The exact name of the Docker container to inspect")] string containerName)
    {
        try
        {
            var response = await _dockerClient.Containers.InspectContainerAsync(containerName); // Fetch the container's details
            var state = response.State; // From the details, fetch its state (running, exited, etc.)
            var info = $"Container '{containerName}': status={state.Status}, running={state.Running}, exitCode={state.ExitCode}"; 
            if (!string.IsNullOrEmpty(state.Error))
                info += $", error='{state.Error}'";
            return info;
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return $"Error: Container '{containerName}' not found."; // In the case of container not existing
        }
        catch (Exception ex)
        {
            return $"Error retrieving status: {ex.Message}";
        }
    }
     
    [KernelFunction("get_container_logs")] // 
    [Description("Fetches the recent stdout/stderr log output from a Docker container. Use this to diagnose why a container crashed or is misbehaving.")]
    public async Task<string> GetContainerLogsAsync(
        [Description("The exact name of the Docker container")] string containerName,
        [Description("Number of recent log lines to retrieve (default 50, max 200)")] int tail = 50)
    {
        tail = Math.Clamp(tail, 1, 200); // Clamp function helps limit the amount of logs retrived 
        try
        {
            var stream = await _dockerClient.Containers.GetContainerLogsAsync(
                containerName,
                tty: false,
                new ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Tail = tail.ToString(),
                    Timestamps = false
                });

            var (stdout, stderr) = await stream.ReadOutputToEndAsync(default); // Read the logs from the stream into stdout and stderr strings
            var combined = new StringBuilder(); // Combines both output and errors into 1 big string for easier reading, StringBuilder is a much more efficient way to concatenate strings than using the + operator in a loop
            if (!string.IsNullOrWhiteSpace(stdout))
                combined.AppendLine(stdout.TrimEnd());
            if (!string.IsNullOrWhiteSpace(stderr))
                combined.AppendLine(stderr.TrimEnd());

            var result = combined.ToString().Trim();
            return string.IsNullOrEmpty(result)
                ? $"Container '{containerName}' has no recent log output."
                : $"Last {tail} lines from '{containerName}':\n{result}";
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return $"Error: Container '{containerName}' not found.";
        }
        catch (Exception ex)
        {
            return $"Error retrieving logs: {ex.Message}";
        }
    }

    [KernelFunction("restart_container")]
    [Description("Restarts a Docker container. Use this as a remediation step after diagnosing the issue. Only restart if logs/status indicate it will help (e.g. a transient crash, not a misconfiguration).")]
    public async Task<string> RestartContainerAsync(
        [Description("The exact name of the Docker container to restart")] string containerName)
    {
        // GUARDRAIL: default-deny. The model can *request* any restart, but this runs
        // regardless of what it decided — the Docker call below never fires unless the
        // container name was explicitly approved in config. Returning an error string
        // (instead of throwing) lets the model learn it was blocked and report that.
        if (!_restartAllowlist.Contains(containerName))
        {
            return $"Error: Restarting '{containerName}' is not permitted. Allowed containers: " +
                   $"{(_restartAllowlist.Count > 0 ? string.Join(", ", _restartAllowlist) : "(none configured)")}.";
        }

        try
        {
            await _dockerClient.Containers.RestartContainerAsync(
                containerName,
                new ContainerRestartParameters { WaitBeforeKillSeconds = 10 }); // this function takes name and optional wait time before forcefully killing the container in the case that it doesn't stop
            return $"Container '{containerName}' restarted successfully.";
        }
        catch (DockerApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return $"Error: Container '{containerName}' not found.";
        }
        catch (Exception ex)
        {
            return $"Error restarting container: {ex.Message}";
        }
    }
}
