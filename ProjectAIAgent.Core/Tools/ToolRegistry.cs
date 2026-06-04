// ProjectAIAgent.Core/Tools/ToolRegistry.cs
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ProjectAIAgent.Core.Tools;

/// <summary>
/// Реестр инструментов — обеспечивает поиск и вызов инструментов по имени
/// </summary>
public class ToolRegistry
{
    private readonly Dictionary<string, IAgentTool> _tools = new();
    private readonly ILogger<ToolRegistry> _logger;
    
    public ToolRegistry(IEnumerable<IAgentTool> tools, ILogger<ToolRegistry> logger)
    {
        _logger = logger;
        foreach (var tool in tools)
        {
            if (_tools.ContainsKey(tool.ToolName))
            {
                logger.LogWarning("Duplicate tool name: {Name}. Overwriting.", tool.ToolName);
            }
            _tools[tool.ToolName] = tool;
            logger.LogInformation("Registered tool: {Name} — {Description}", 
                tool.ToolName, tool.Description);
        }
    }
    
    /// <summary>
    /// Получить все зарегистрированные инструменты
    /// </summary>
    public IReadOnlyDictionary<string, IAgentTool> GetAllTools() => _tools;
    
    /// <summary>
    /// Получить инструмент по имени
    /// </summary>
    public IAgentTool? GetTool(string name) => 
        _tools.TryGetValue(name, out var tool) ? tool : null;
    
    /// <summary>
    /// Выполнить инструмент по имени с параметрами
    /// </summary>
    public async Task<ToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            return ToolResult.Fail($"Tool '{toolName}' not found. Available: {string.Join(", ", _tools.Keys)}");
        }
        
        try
        {
            _logger.LogDebug("Executing tool {Tool} with params: {@Params}", toolName, parameters);
            return await tool.ExecuteAsync(parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {Tool} execution failed", toolName);
            return ToolResult.Fail($"Tool execution failed: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Генерирует описание всех инструментов для системного промпта
    /// </summary>
    public string GetToolsDescription()
    {
        var lines = new List<string>();
        foreach (var tool in _tools.Values)
        {
            lines.Add($"- {tool.ToolName}: {tool.Description}");
            lines.Add($"  Parameters: {tool.ParametersSchema}");
        }
        return string.Join("\n", lines);
    }
}

/// <summary>
/// Методы расширения для регистрации инструментов в DI
/// </summary>
public static class ToolRegistrationExtensions
{
    /// <summary>
    /// Автоматически регистрирует все классы, реализующие IAgentTool, из указанной сборки
    /// </summary>
    public static IServiceCollection AddAgentTools(this IServiceCollection services, Assembly assembly)
    {
        var toolTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IAgentTool).IsAssignableFrom(t));
            
        foreach (var type in toolTypes)
        {
            services.AddSingleton(typeof(IAgentTool), type);
        }
        
        // Реестр регистрируем отдельно, он зависит от IEnumerable<IAgentTool>
        services.AddSingleton<ToolRegistry>();
        
        return services;
    }
}