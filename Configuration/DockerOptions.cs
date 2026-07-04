namespace DevOps_Agent.Configuration;
public class DockerOptions
{
    // The ONLY containers the agent is permitted to restart. Anything not in this list is denied by default
    public List<string> RestartAllowlist { get; set; } = new();
}
