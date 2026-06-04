// ProjectAIAgent.Core/Agents/OrchestratorAgent.cs
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectAIAgent.Core.Models;
using ProjectAIAgent.Core.Services;
using ProjectAIAgent.Core.Tools;

namespace ProjectAIAgent.Core.Agents;

public class OrchestratorAgent : BaseAgent
{
    public override string Name => "Orchestrator";
    public override string Role => "Orchestrator";
    protected override string PromptResourceName => "orchestrator.txt";
    
    private readonly ToolRegistry _toolRegistry;
    private readonly ILlmService? _llmService;
    private readonly ContextAgent _contextAgent;
    private readonly BuildValidationService _buildValidation;
    private readonly ReportService _reportService;
    private const int MaxIterations = 10;
    
    // Опциональный сервис SignalR (может быть null в консольном режиме)
    private readonly object? _signalRService;
    
    public OrchestratorAgent(
        ILogger<OrchestratorAgent> logger,
        IServiceProvider serviceProvider,
        ToolRegistry toolRegistry,
        ContextAgent contextAgent,
        BuildValidationService buildValidation,
        ReportService reportService,
        ILlmService? llmService = null)
        : base(logger, serviceProvider)
    {
        _toolRegistry = toolRegistry;
        _contextAgent = contextAgent ?? throw new ArgumentNullException(nameof(contextAgent));
        _buildValidation = buildValidation ?? throw new ArgumentNullException(nameof(buildValidation));
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _llmService = llmService;
        
        // Пытаемся получить SignalRLoggingService (может отсутствовать в консольном режиме)
        var signalRType = Type.GetType("ProjectAIAgent.Host.SignalRLoggingService, ProjectAIAgent.Host", throwOnError: false);
        _signalRService = signalRType != null ? serviceProvider.GetService(signalRType) : null;
    }
    
    public async Task<string> ProcessRequestAsync(string userRequest)
    {
        var startedAt = DateTime.UtcNow;
        var toolsUsed = new List<string>();
        var errors = new List<string>();
        var llmCallCount = 0;
        var toolCallCount = 0;
        var validationResult = string.Empty;
        bool success = false;
        string finalMessage = string.Empty;
        
        Logger.LogInformation("Orchestrator received request: {Request}", userRequest);
        
        _contextAgent.SetPhase(WorkPhase.Planning);
        await SendPhaseUpdateAsync(WorkPhase.Planning);
        
        var contextSummary = _contextAgent.GetContextSummary();
        
        if (_llmService == null)
        {
            var systemPrompt = await GetSystemPromptAsync();
            var toolsDescription = _toolRegistry.GetToolsDescription();
            var fullPrompt = systemPrompt.Replace("{TOOLS}", toolsDescription);
            return $"Placeholder: Orchestrator received '{userRequest}'. " +
                   $"System prompt: {fullPrompt.Length} chars. " +
                   $"Available tools: {_toolRegistry.GetAllTools().Count}";
        }
        
        _buildValidation.Reset();
        
        var systemPromptFinal = await BuildSystemPromptAsync(contextSummary);
        
        var conversationHistory = new StringBuilder();
        conversationHistory.AppendLine($"[USER REQUEST]: {userRequest}");
        conversationHistory.AppendLine($"[PROJECT CONTEXT]: {contextSummary}");
        conversationHistory.AppendLine("[INSTRUCTION]: Analyze the request. " +
            "If code changes are needed: explore structure -> read files -> make changes -> run 'dotnet build'. " +
            "If build fails, fix errors and retry. After successful build, report. Start with the first tool call.");
        
        var iteration = 0;
        var buildAttemptCount = 0;
        
        while (iteration < MaxIterations)
        {
            iteration++;
            Logger.LogDebug("Orchestrator iteration {Iteration}/{MaxIterations}", iteration, MaxIterations);
            
            _contextAgent.SetPhase(WorkPhase.Executing);
            await SendPhaseUpdateAsync(WorkPhase.Executing);
            
            llmCallCount++;
            var llmResponse = await _llmService.GenerateAsync(
                systemPromptFinal,
                conversationHistory.ToString(),
                CancellationToken.None);
            
            Logger.LogDebug("LLM response ({Length} chars): {Preview}", 
                llmResponse.Length, 
                llmResponse.Length > 500 ? llmResponse[..500] + "..." : llmResponse);
            
            var actionPlan = ExtractActionPlanRobust(llmResponse);
            
            if (actionPlan == null)
            {
                _contextAgent.RegisterError();
                errors.Add("Failed to parse LLM response");
                conversationHistory.AppendLine();
                conversationHistory.AppendLine($"[ASSISTANT]: {llmResponse}");
                conversationHistory.AppendLine("[USER]: Respond with valid JSON.");
                continue;
            }
            
            var action = actionPlan.TryGetValue("action", out var actionObj) 
                ? actionObj?.ToString()?.ToLowerInvariant() 
                : null;
            
            Logger.LogInformation("Orchestrator action: {Action}", action);
            
            if (action == "report")
            {
                _contextAgent.SetPhase(WorkPhase.Reporting);
                await SendPhaseUpdateAsync(WorkPhase.Reporting);
                success = true;
                
                finalMessage = 
                    TryGetStringField(actionPlan, "message") ??
                    TryGetStringField(actionPlan, "summary") ??
                    TryGetStringField(actionPlan, "report") ??
                    "Запрос выполнен.";
                
                break;
            }
            
            if (action == "delegate" || action == "plan")
            {
                var toolName = actionPlan.TryGetValue("tool", out var t) ? t?.ToString() : "unknown";
                
                if (toolName == "run_shell_command" && 
                    actionPlan.TryGetValue("args", out var rawArgs) &&
                    rawArgs is Dictionary<string, object> buildArgs &&
                    buildArgs.TryGetValue("command", out var cmd) &&
                    cmd?.ToString()?.Contains("dotnet build") == true)
                {
                    buildAttemptCount++;
                    _contextAgent.SetPhase(WorkPhase.Validating);
                    await SendPhaseUpdateAsync(WorkPhase.Validating);
                    
                    var projectPath = _contextAgent.GetContext().ProjectPath;
                    if (string.IsNullOrWhiteSpace(projectPath))
                        projectPath = Directory.GetCurrentDirectory();
                    
                    var buildResult = await _buildValidation.BuildAsync(projectPath);
                    
                    await SendBuildResultAsync(buildResult.Success, buildResult.AttemptNumber, buildResult.GetSummary());
                    
                    if (buildResult.Success)
                        validationResult = $"✅ Сборка успешна (попытка {buildResult.AttemptNumber})";
                    else if (!buildResult.ShouldRetry)
                        validationResult = $"❌ Сборка провалена после {buildResult.AttemptNumber} попыток";
                    else
                        validationResult = $"🔄 Сборка не удалась (попытка {buildResult.AttemptNumber}), осталось {buildResult.RemainingAttempts}";
                    
                    conversationHistory.AppendLine();
                    conversationHistory.AppendLine($"[BUILD RESULT — attempt {buildAttemptCount}/{_buildValidation.MaxBuildAttempts}]:");
                    conversationHistory.AppendLine(buildResult.GetSummary());
                    
                    if (buildResult.Success)
                    {
                        conversationHistory.AppendLine("[USER]: Build succeeded! Show git diff, then report.");
                    }
                    else if (buildResult.ShouldRetry)
                    {
                        conversationHistory.AppendLine("[USER]: Build failed. " +
                            $"You have {buildResult.RemainingAttempts} attempts left. Fix the errors and retry.");
                    }
                    else
                    {
                        conversationHistory.AppendLine("[USER]: All build attempts exhausted. Report the failure.");
                    }
                    continue;
                }
                
                toolCallCount++;
                toolsUsed.Add(toolName ?? "unknown");
                
                var toolResult = await ExecuteToolFromPlan(actionPlan);
                var toolSuccess = !toolResult.StartsWith("Error");
                
                await SendToolExecutionAsync(toolName!, toolSuccess, toolResult.Truncate(200));
                
                if (toolSuccess)
                    RegisterToolSuccess(toolName, actionPlan);
                else
                {
                    _contextAgent.RegisterError();
                    errors.Add($"Tool '{toolName}' failed: {toolResult}!");
                }
                
                conversationHistory.AppendLine();
                conversationHistory.AppendLine($"[ASSISTANT]: {llmResponse}");
                conversationHistory.AppendLine($"[TOOL RESULT — {toolName}]:");
                conversationHistory.AppendLine(toolResult);
                
                if (toolSuccess && (toolName == "write_file" || toolName == "update_documentation"))
                {
                    conversationHistory.AppendLine("[USER]: File modified. Run 'dotnet build' to validate.");
                }
                else if (toolName == "run_shell_command")
                {
                    conversationHistory.AppendLine("[USER]: Command executed. Proceed or fix errors.");
                }
                else
                {
                    conversationHistory.AppendLine("[USER]: Based on this result, what's your next step? Respond ONLY with JSON.");
                }
                continue;
            }
            
            _contextAgent.RegisterError();
            errors.Add($"Unknown action: {action}");
            Logger.LogWarning("Unknown action: {Action}", action);
            conversationHistory.AppendLine();
            conversationHistory.AppendLine($"[ASSISTANT]: {llmResponse}");
            conversationHistory.AppendLine("[USER]: Unknown action. Valid: 'delegate' or 'report'.");
        }
        
        if (!success && string.IsNullOrEmpty(finalMessage))
        {
            finalMessage = "⚠️ Достигнут лимит итераций.";
            Logger.LogWarning("Orchestrator reached max iterations ({MaxIterations})", MaxIterations);
        }
        
        _contextAgent.RecordInteraction(userRequest, finalMessage, success);
        _contextAgent.SetPhase(WorkPhase.Idle);
        await SendPhaseUpdateAsync(WorkPhase.Idle);
        
        var modifiedFiles = _contextAgent.GetContext().ModifiedFiles;
        
        var reportData = new ReportData
        {
            UserRequest = userRequest,
            Message = finalMessage,
            Success = success,
            ModifiedFiles = modifiedFiles,
            ValidationResult = string.IsNullOrWhiteSpace(validationResult) ? null : validationResult,
            Errors = errors,
            ToolsUsed = toolsUsed,
            Duration = DateTime.UtcNow - startedAt,
            LlmCallCount = llmCallCount,
            ToolCallCount = toolCallCount
        };
        
        var report = _reportService.GenerateReport(reportData);
        await SendFinalResultAsync(success, report);
        
        return report;
    }
    
    // Приватные методы для SignalR (безопасно вызываются через reflection-подобный паттерн)
    
    private async Task SendPhaseUpdateAsync(WorkPhase phase)
    {
        if (_signalRService == null) return;
        try
        {
            var method = _signalRService.GetType().GetMethod("SendPhaseUpdateAsync");
            if (method != null)
            {
                var task = method.Invoke(_signalRService, new object[] { phase }) as Task;
                if (task != null)
                    await task;
            }
        }
        catch { }
    }
    
    private async Task SendToolExecutionAsync(string toolName, bool success, string? summary)
    {
        if (_signalRService == null) return;
        try
        {
            var method = _signalRService.GetType().GetMethod("SendToolExecutionAsync");
            if (method != null)
            {
                var task = method.Invoke(_signalRService, new object?[] { toolName, success, summary }) as Task;
                if (task != null)
                    await task;
            }
        }
        catch { }
    }
    
    private async Task SendBuildResultAsync(bool success, int attempt, string summary)
    {
        if (_signalRService == null) return;
        try
        {
            var method = _signalRService.GetType().GetMethod("SendBuildResultAsync");
            if (method != null)
            {
                var task = method.Invoke(_signalRService, new object?[] { success, attempt, summary }) as Task;
                if (task != null)
                    await task;
            }
        }
        catch { }
    }
    
    private async Task SendFinalResultAsync(bool success, string report)
    {
        if (_signalRService == null) return;
        try
        {
            var method = _signalRService.GetType().GetMethod("SendFinalResultAsync");
            if (method != null)
            {
                var task = method.Invoke(_signalRService, new object?[] { success, report }) as Task;
                if (task != null)
                    await task;
            }
        }
        catch { }
    }
    
    private async Task<string> BuildSystemPromptAsync(string contextSummary)
    {
        var prompt = await GetSystemPromptAsync();
        var toolsDesc = _toolRegistry.GetToolsDescription();
        prompt = prompt.Replace("{TOOLS}", toolsDesc);
        
        if (!string.IsNullOrWhiteSpace(contextSummary))
            prompt += $"\n\n## Current Project Context\n{contextSummary}";
        
        return prompt;
    }
    
    private void RegisterToolSuccess(string? toolName, Dictionary<string, object> actionPlan)
    {
        if (toolName == "write_file" || toolName == "update_documentation")
        {
            if (actionPlan.TryGetValue("args", out var argsObj) && argsObj is Dictionary<string, object> argsDict)
            {
                if (argsDict.TryGetValue("file_path", out var fp) || argsDict.TryGetValue("path", out fp))
                    _contextAgent.RegisterFileModification(fp?.ToString() ?? "unknown");
            }
        }
    }
    
    private Dictionary<string, object>? ExtractActionPlanRobust(string llmResponse)
    {
        var codeBlocks = LlmResponseParser.ExtractAllCodeBlocks(llmResponse);
        foreach (var block in codeBlocks)
        {
            var plan = LlmResponseParser.ExtractActionPlan(block);
            if (plan != null && plan.ContainsKey("action"))
                return plan;
        }
        return LlmResponseParser.ExtractActionPlan(llmResponse);
    }
    
    private static string? TryGetStringField(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var value) && value != null)
            return value.ToString();
        return null;
    }
    
    private async Task<string> ExecuteToolFromPlan(Dictionary<string, object> actionPlan)
    {
        try
        {
            if (!actionPlan.TryGetValue("tool", out var toolObj) || toolObj == null)
                return "Error: 'tool' field is missing.";
            
            var toolName = toolObj.ToString()!;
            var args = ParseArgs(actionPlan);
            
            Logger.LogInformation("Executing tool: {ToolName} with args: {@Args}", toolName, args);
            
            var result = await _toolRegistry.ExecuteToolAsync(toolName, args);
            
            if (result.Success)
            {
                var output = result.Output ?? "No output";
                if (output.Length > 3000)
                    output = output[..3000] + $"\n... [truncated, total {output.Length} chars]";
                return $"Success: {output}";
            }
            
            return $"Error: {result.Error}";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to execute tool");
            return $"Error: {ex.Message}";
        }
    }
    
    private static Dictionary<string, object> ParseArgs(Dictionary<string, object> actionPlan)
    {
        var args = new Dictionary<string, object>();
        
        if (!actionPlan.TryGetValue("args", out var argsObj) || argsObj == null)
            return args;
        
        if (argsObj is Dictionary<string, object> argsDict)
        {
            foreach (var kvp in argsDict)
                args[kvp.Key] = kvp.Value;
        }
        else if (argsObj is JsonElement argsElement && argsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in argsElement.EnumerateObject())
            {
                args[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString()!,
                    JsonValueKind.Number => prop.Value.TryGetInt32(out var i) ? i : prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => prop.Value.GetRawText()
                };
            }
        }
        
        return args;
    }
}