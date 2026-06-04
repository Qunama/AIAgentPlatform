// ProjectAIAgent.Core/Agents/OrchestratorAgent.cs
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProjectAIAgent.Core.Models;
using ProjectAIAgent.Core.Services;
using ProjectAIAgent.Core.Tools;

namespace ProjectAIAgent.Core.Agents;

/// <summary>
/// Главный оркестрирующий агент. Принимает запросы пользователя,
/// формирует план изменений, выполняет шаги, валидирует и документирует результат.
/// </summary>
public class OrchestratorAgent : BaseAgent
{
    public override string Name => "Orchestrator";
    public override string Role => "Orchestrator";
    protected override string PromptResourceName => "orchestrator.txt";
    
    private readonly ToolRegistry _toolRegistry;
    private readonly ILlmService? _llmService;
    private readonly ContextAgent _contextAgent;
    private const int MaxIterations = 10;
    
    public OrchestratorAgent(
        ILogger<OrchestratorAgent> logger,
        IServiceProvider serviceProvider,
        ToolRegistry toolRegistry,
        ContextAgent contextAgent,
        ILlmService? llmService = null)
        : base(logger, serviceProvider)
    {
        _toolRegistry = toolRegistry;
        _contextAgent = contextAgent ?? throw new ArgumentNullException(nameof(contextAgent));
        _llmService = llmService;
    }
    
    /// <summary>
    /// Основной метод: принять запрос пользователя и выполнить полный цикл оркестрации.
    /// </summary>
    public async Task<string> ProcessRequestAsync(string userRequest)
    {
        Logger.LogInformation("Orchestrator received request: {Request}", userRequest);
        
        // Фаза 1: Планирование
        _contextAgent.SetPhase(WorkPhase.Planning);
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
        
        var systemPromptFinal = await BuildSystemPromptAsync(contextSummary);
        
        var conversationHistory = new StringBuilder();
        conversationHistory.AppendLine($"[USER REQUEST]: {userRequest}");
        conversationHistory.AppendLine($"[PROJECT CONTEXT]: {contextSummary}");
        conversationHistory.AppendLine("[INSTRUCTION]: Analyze the request. " +
            "If the request requires code changes, first explore the project structure, then read relevant files, " +
            "then make changes. After all changes, run 'dotnet build' to validate. " +
            "Start with the first tool call.");
        
        var iteration = 0;
        string? lastToolName = null;
        
        while (iteration < MaxIterations)
        {
            iteration++;
            Logger.LogDebug("Orchestrator iteration {Iteration}/{MaxIterations}", iteration, MaxIterations);
            
            _contextAgent.SetPhase(WorkPhase.Executing);
            
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
                conversationHistory.AppendLine();
                conversationHistory.AppendLine($"[ASSISTANT]: {llmResponse}");
                conversationHistory.AppendLine("[USER]: Respond with valid JSON. " +
                    "{\"action\":\"delegate\",\"tool\":\"tool_name\",\"args\":{...}} or " +
                    "{\"action\":\"report\",\"message\":\"...\"}");
                continue;
            }
            
            var action = actionPlan.TryGetValue("action", out var actionObj) 
                ? actionObj?.ToString()?.ToLowerInvariant() 
                : null;
            
            Logger.LogInformation("Orchestrator action: {Action}", action);
            
            if (action == "report")
            {
                _contextAgent.SetPhase(WorkPhase.Reporting);
                
                var finalMessage = 
                    TryGetStringField(actionPlan, "message") ??
                    TryGetStringField(actionPlan, "summary") ??
                    TryGetStringField(actionPlan, "report") ??
                    "Запрос выполнен.";
                
                _contextAgent.RecordInteraction(userRequest, finalMessage, true);
                _contextAgent.SetPhase(WorkPhase.Idle);
                
                return finalMessage;
            }
            
            if (action == "delegate" || action == "plan")
            {
                var toolName = actionPlan.TryGetValue("tool", out var t) ? t?.ToString() : "unknown";
                lastToolName = toolName;
                
                var toolResult = await ExecuteToolFromPlan(actionPlan);
                var toolSuccess = !toolResult.StartsWith("Error");
                
                if (toolSuccess)
                {
                    RegisterToolSuccess(toolName, actionPlan);
                }
                else
                {
                    _contextAgent.RegisterError();
                }
                
                conversationHistory.AppendLine();
                conversationHistory.AppendLine($"[ASSISTANT]: {llmResponse}");
                conversationHistory.AppendLine($"[TOOL RESULT — {toolName}]:");
                conversationHistory.AppendLine(toolResult);
                
                // После успешной записи — предлагаем валидацию
                if (toolSuccess && (toolName == "write_file" || toolName == "update_documentation"))
                {
                    conversationHistory.AppendLine("[USER]: File modified. " +
                        "Consider running 'dotnet build' to validate changes. " +
                        "If validation passes, report the result. If it fails, fix the errors.");
                }
                // После сборки — анализируем результат
                else if (toolName == "run_shell_command")
                {
                    conversationHistory.AppendLine("[USER]: Command executed. " +
                        "If it succeeded, proceed. If it failed, analyze the errors and fix them. " +
                        "After fixing, run the build again.");
                }
                else
                {
                    conversationHistory.AppendLine("[USER]: Based on this result, what's your next step? " +
                        "If done, run 'dotnet build' to validate, then report. " +
                        "Respond ONLY with JSON.");
                }
                continue;
            }
            
            _contextAgent.RegisterError();
            Logger.LogWarning("Unknown action: {Action}", action);
            conversationHistory.AppendLine();
            conversationHistory.AppendLine($"[ASSISTANT]: {llmResponse}");
            conversationHistory.AppendLine("[USER]: Unknown action. Valid: 'delegate' or 'report'. Respond ONLY with JSON.");
        }
        
        Logger.LogWarning("Orchestrator reached max iterations ({MaxIterations})", MaxIterations);
        _contextAgent.RecordInteraction(userRequest, "Max iterations reached", false);
        _contextAgent.SetPhase(WorkPhase.Idle);
        return "⚠️ Достигнут лимит итераций. Пожалуйста, уточните запрос.";
    }
    
    /// <summary>
    /// Строит системный промпт с инструментами и контекстом.
    /// </summary>
    private async Task<string> BuildSystemPromptAsync(string contextSummary)
    {
        var prompt = await GetSystemPromptAsync();
        var toolsDesc = _toolRegistry.GetToolsDescription();
        prompt = prompt.Replace("{TOOLS}", toolsDesc);
        
        if (!string.IsNullOrWhiteSpace(contextSummary))
        {
            prompt += $"\n\n## Current Project Context\n{contextSummary}";
        }
        
        return prompt;
    }
    
    /// <summary>
    /// Регистрирует успешный вызов инструмента в ContextAgent.
    /// </summary>
    private void RegisterToolSuccess(string? toolName, Dictionary<string, object> actionPlan)
    {
        if (toolName == "write_file" || toolName == "update_documentation")
        {
            if (actionPlan.TryGetValue("args", out var argsObj))
            {
                var argsDict = argsObj as Dictionary<string, object>;
                if (argsDict != null)
                {
                    if (argsDict.TryGetValue("file_path", out var fp) || argsDict.TryGetValue("path", out fp))
                    {
                        _contextAgent.RegisterFileModification(fp?.ToString() ?? "unknown");
                    }
                }
            }
        }
        
        if (toolName == "project_structure")
        {
            // Кешируем структуру проекта (результат будет в следующей итерации)
            // ContextAgent.CacheProjectStructure() будет вызван при получении результата
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