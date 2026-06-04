// ProjectAIAgent.Core/Agents/OrchestratorAgent.cs
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProjectAIAgent.Core.Services;
using ProjectAIAgent.Core.Tools;

namespace ProjectAIAgent.Core.Agents;

/// <summary>
/// Главный оркестрирующий агент. Принимает запросы пользователя,
/// формирует план изменений и делегирует выполнение инструментам.
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
    /// Основной метод: принять запрос пользователя и выполнить цикл оркестрации.
    /// </summary>
    public async Task<string> ProcessRequestAsync(string userRequest)
    {
        Logger.LogInformation("Orchestrator received request: {Request}", userRequest);
        
        // Устанавливаем фазу планирования
        _contextAgent.SetPhase(Models.WorkPhase.Planning);
        
        // Получаем сводку контекста от ContextAgent
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
        
        var systemPromptFinal = await GetSystemPromptAsync();
        var toolsDesc = _toolRegistry.GetToolsDescription();
        systemPromptFinal = systemPromptFinal.Replace("{TOOLS}", toolsDesc);
        
        // Добавляем контекст проекта в промпт
        if (!string.IsNullOrWhiteSpace(contextSummary))
        {
            systemPromptFinal += $"\n\n## Current Project Context\n{contextSummary}";
        }
        
        // Начинаем диалог с запроса пользователя и контекста
        var conversationHistory = new StringBuilder();
        conversationHistory.AppendLine($"[USER REQUEST]: {userRequest}");
        conversationHistory.AppendLine($"[PROJECT CONTEXT]: {contextSummary}");
        conversationHistory.AppendLine("[INSTRUCTION]: Analyze the request and context. " +
            "Decide the first action: 'delegate' to call a tool, or 'report' if the request is trivial.");
        
        var iteration = 0;
        
        while (iteration < MaxIterations)
        {
            iteration++;
            Logger.LogDebug("Orchestrator iteration {Iteration}/{MaxIterations}", iteration, MaxIterations);
            
            // Устанавливаем фазу выполнения
            _contextAgent.SetPhase(Models.WorkPhase.Executing);
            
            // Отправляем: системный промпт + историю диалога
            var llmResponse = await _llmService.GenerateAsync(
                systemPromptFinal,
                conversationHistory.ToString(),
                CancellationToken.None);
            
            Logger.LogDebug("LLM response ({Length} chars): {Preview}", 
                llmResponse.Length, 
                llmResponse.Length > 500 ? llmResponse[..500] + "..." : llmResponse);
            
            // Извлекаем JSON из ответа
            var actionPlan = ExtractActionPlanRobust(llmResponse);
            
            if (actionPlan == null)
            {
                Logger.LogWarning("Failed to extract action plan from LLM response");
                _contextAgent.RegisterError();
                conversationHistory.AppendLine();
                conversationHistory.AppendLine($"[ASSISTANT]: {llmResponse}");
                conversationHistory.AppendLine("[USER]: Your response must contain a valid JSON with 'action' field. " +
                    "Valid actions: 'delegate' or 'report'. " +
                    "For delegate: {\"action\":\"delegate\", \"tool\":\"tool_name\", \"args\":{...}}. " +
                    "For report: {\"action\":\"report\", \"message\":\"your report here\"}. " +
                    "Respond ONLY with the JSON, no markdown blocks, no extra text.");
                continue;
            }
            
            // Определяем действие
            var action = actionPlan.TryGetValue("action", out var actionObj) 
                ? actionObj?.ToString()?.ToLowerInvariant() 
                : null;
            
            Logger.LogInformation("Orchestrator action: {Action}", action);
            
            if (action == "report")
            {
                // Устанавливаем фазу отчёта
                _contextAgent.SetPhase(Models.WorkPhase.Reporting);
                
                // Ищем сообщение в разных возможных полях
                var finalMessage = 
                    TryGetStringField(actionPlan, "message") ??
                    TryGetStringField(actionPlan, "summary") ??
                    TryGetStringField(actionPlan, "report") ??
                    "Запрос выполнен.";
                
                // Записываем успешное взаимодействие в историю
                _contextAgent.RecordInteraction(userRequest, finalMessage, true);
                
                return finalMessage;
            }
            
            if (action == "delegate" || action == "plan")
            {
                var toolName = actionPlan.TryGetValue("tool", out var t) ? t?.ToString() : "unknown";
                var toolResult = await ExecuteToolFromPlan(actionPlan);
                
                var toolSuccess = !toolResult.StartsWith("Error");
                
                if (toolSuccess)
                {
                    // Регистрируем изменённые файлы в ContextAgent
                    if (toolName == "write_file" || toolName == "update_documentation")
                    {
                        if (actionPlan.TryGetValue("args", out var argsObj) && argsObj is Dictionary<string, object> argsDict)
                        {
                            if (argsDict.TryGetValue("file_path", out var fp) || argsDict.TryGetValue("path", out fp))
                            {
                                _contextAgent.RegisterFileModification(fp?.ToString() ?? "unknown");
                            }
                        }
                    }
                }
                else
                {
                    _contextAgent.RegisterError();
                }
                
                // Добавляем ответ ассистента и результат инструмента в историю
                conversationHistory.AppendLine();
                conversationHistory.AppendLine($"[ASSISTANT]: {llmResponse}");
                conversationHistory.AppendLine($"[TOOL RESULT — {toolName}]:");
                conversationHistory.AppendLine(toolResult);
                conversationHistory.AppendLine("[USER]: Based on this tool result, what's your next step? " +
                    "If the user request is fulfilled, respond with action:'report'. " +
                    "If you need another tool, respond with action:'delegate'. " +
                    "Respond ONLY with JSON, no markdown.");
                continue;
            }
            
            // Неизвестное действие — просим уточнить
            _contextAgent.RegisterError();
            Logger.LogWarning("Unknown action: {Action}", action);
            conversationHistory.AppendLine();
            conversationHistory.AppendLine($"[ASSISTANT]: {llmResponse}");
            conversationHistory.AppendLine("[USER]: Unknown action. Valid actions are 'delegate' and 'report'. " +
                "Respond ONLY with JSON.");
        }
        
        Logger.LogWarning("Orchestrator reached max iterations ({MaxIterations})", MaxIterations);
        _contextAgent.RecordInteraction(userRequest, "Max iterations reached", false);
        _contextAgent.SetPhase(Models.WorkPhase.Idle);
        return "⚠️ Достигнут лимит итераций. Пожалуйста, уточните запрос.";
    }
    
    /// <summary>
    /// Извлекает JSON из ответа LLM. Поддерживает:
    /// - Чистый JSON: {"action": ...}
    /// - JSON в Markdown-блоке: ```json { ... } ```
    /// - JSON внутри обычного текста
    /// </summary>
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
    
    /// <summary>
    /// Безопасно извлекает строковое поле из словаря.
    /// </summary>
    private static string? TryGetStringField(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out var value) && value != null)
        {
            return value.ToString();
        }
        return null;
    }
    
    /// <summary>
    /// Извлекает из плана имя инструмента и аргументы, вызывает инструмент, возвращает результат.
    /// </summary>
    private async Task<string> ExecuteToolFromPlan(Dictionary<string, object> actionPlan)
    {
        try
        {
            if (!actionPlan.TryGetValue("tool", out var toolObj) || toolObj == null)
            {
                return "Error: 'tool' field is missing in the action plan.";
            }
            
            var toolName = toolObj.ToString()!;
            
            var args = new Dictionary<string, object>();
            if (actionPlan.TryGetValue("args", out var argsObj) && argsObj != null)
            {
                if (argsObj is Dictionary<string, object> argsDict)
                {
                    foreach (var kvp in argsDict)
                    {
                        args[kvp.Key] = kvp.Value;
                    }
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
            }
            
            Logger.LogInformation("Executing tool: {ToolName} with args: {@Args}", toolName, args);
            
            var result = await _toolRegistry.ExecuteToolAsync(toolName, args);
            
            if (result.Success)
            {
                var output = result.Output ?? "No output";
                if (output.Length > 3000)
                {
                    output = output[..3000] + $"\n... [truncated, total {output.Length} chars]";
                }
                return $"Success: {output}";
            }
            else
            {
                return $"Error: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to execute tool from plan");
            return $"Error executing tool: {ex.Message}";
        }
    }
}