// ProjectAIAgent.Core/Tools/IAgentTool.cs
namespace ProjectAIAgent.Core.Tools;

/// <summary>
/// Результат выполнения инструмента
/// </summary>
public class ToolResult
{
    public bool Success { get; init; }
    public string Output { get; init; } = string.Empty;
    public string? Error { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
    
    public static ToolResult Ok(string output, Dictionary<string, object>? metadata = null)
        => new() { Success = true, Output = output, Metadata = metadata };
        
    public static ToolResult Fail(string error)
        => new() { Success = false, Error = error, Output = error };
}

/// <summary>
/// Дескриптор инструмента для автоматической регистрации и описания LLM
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class AgentToolAttribute : Attribute
{
    public string Name { get; }
    public string Description { get; }
    
    public AgentToolAttribute(string name, string description)
    {
        Name = name;
        Description = description;
    }
}

/// <summary>
/// Базовый интерфейс инструмента агента
/// </summary>
public interface IAgentTool
{
    /// <summary>
    /// Уникальное имя инструмента (используется для вызова агентом)
    /// </summary>
    string ToolName { get; }
    
    /// <summary>
    /// Описание инструмента (включается в системный промпт агента)
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// JSON-схема параметров для LLM
    /// </summary>
    string ParametersSchema { get; }
    
    /// <summary>
    /// Выполнить инструмент с переданными параметрами
    /// </summary>
    Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters);
}

/// <summary>
/// Типизированная версия для инструментов с известными параметрами
/// </summary>
public interface IAgentTool<TParams> : IAgentTool where TParams : class
{
    Task<ToolResult> ExecuteTypedAsync(TParams parameters);
}