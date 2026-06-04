// ProjectAIAgent.Host/Controllers/AgentController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Agents;
using ProjectAIAgent.Core.Services;
using ProjectAIAgent.Core.Tools;
using ProjectAIAgent.Core.Models;

namespace ProjectAIAgent.Host.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly OrchestratorAgent _orchestrator;
    private readonly ContextAgent _contextAgent;
    private readonly IOptions<AgentOptions> _agentOptions;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        OrchestratorAgent orchestrator,
        ContextAgent contextAgent,
        IOptions<AgentOptions> agentOptions,
        ILogger<AgentController> logger)
    {
        _orchestrator = orchestrator;
        _contextAgent = contextAgent;
        _agentOptions = agentOptions;
        _logger = logger;
    }

    /// <summary>
    /// Устанавливает путь к проекту.
    /// </summary>
    [HttpPost("set-project")]
    public IActionResult SetProject([FromBody] SetProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
            return BadRequest(new { error = "Path is required" });

        if (!Directory.Exists(request.Path))
            return BadRequest(new { error = $"Path not found: {request.Path}" });

        _agentOptions.Value.ProjectPath = request.Path;
        _contextAgent.SetProjectPath(request.Path);

        _logger.LogInformation("Project path set via API: {Path}", request.Path);

        return Ok(new { message = $"Project path set to: {request.Path}" });
    }

    /// <summary>
    /// Отправляет запрос агенту и возвращает результат.
    /// </summary>
    [HttpPost("request")]
    public async Task<IActionResult> ProcessRequest([FromBody] AgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Query is required" });

        _logger.LogInformation("API request received: {Query}", request.Query);

        try
        {
            var result = await _orchestrator.ProcessRequestAsync(request.Query);
            return Ok(new AgentResponse
            {
                Success = true,
                Message = result,
                Query = request.Query
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process API request");
            return StatusCode(500, new AgentResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                Query = request.Query
            });
        }
    }

    /// <summary>
    /// Возвращает текущий статус агента.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var context = _contextAgent.GetContext();

        return Ok(new
        {
            projectPath = context.ProjectPath,
            phase = context.CurrentPhase.ToString(),
            modifiedFiles = context.ModifiedFiles,
            errorCount = context.ErrorCount,
            historyCount = context.History.Count,
            lastRequest = context.LastRequest,
            lastResponse = context.LastResponse?.Truncate(200)
        });
    }

    /// <summary>
    /// Возвращает историю взаимодействий.
    /// </summary>
    [HttpGet("history")]
    public IActionResult GetHistory([FromQuery] int limit = 10)
    {
        var context = _contextAgent.GetContext();
        var history = context.History
            .OrderByDescending(h => h.Timestamp)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(h => new
            {
                timestamp = h.Timestamp.ToString("O"),
                request = h.Request,
                response = h.Response.Truncate(200),
                success = h.Success
            });

        return Ok(history);
    }
}

// DTOs

public class SetProjectRequest
{
    public string Path { get; set; } = string.Empty;
}

public class AgentRequest
{
    public string Query { get; set; } = string.Empty;
}

public class AgentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
}