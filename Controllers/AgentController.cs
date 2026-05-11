using DevOps_Agent.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevOps_Agent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletionService;

    public AgentController(Kernel kernel, IChatCompletionService chatCompletionService)
    {
        _kernel = kernel;
        _chatCompletionService = chatCompletionService;
    }

    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerAgent([FromBody] AlertPayload payload)
    {
        // Define the Agent's Persona and Context
        var chatHistory = new ChatHistory("You are an autonomous DevOps Remediation Agent. Your job is to investigate infrastructure alerts. Use your available tools to gather information.");

        chatHistory.AddUserMessage($"ALERT INCOMING: The system reported an issue with container '{payload.ContainerName}'. Error: {payload.ErrorMessage}. Investigate the current status of this container.");

        var executionSettings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var result = await _chatCompletionService.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            _kernel);

        return Ok(new { AgentResponse = result.Content });
    }
}
