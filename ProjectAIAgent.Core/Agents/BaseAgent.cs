// ProjectAIAgent.Core/Agents/BaseAgent.cs
using Microsoft.Extensions.Logging;
using ProjectAIAgent.Core.Tools;
namespace ProjectAIAgent.Core.Agents;
using Microsoft.Extensions.DependencyInjection;

public abstract class BaseAgent
{
    protected readonly ILogger Logger;
    protected readonly IServiceProvider ServiceProvider;
    
    // Имя агента для логирования и идентификации
    public abstract string Name { get; }
    
    // Роль агента в системе (Orchestrator, CodeEditor, etc.)
    public abstract string Role { get; }
    
    // Путь к файлу системного промпта
    protected abstract string PromptResourceName { get; }
    
    // Кешированный системный промпт
    private string? _cachedSystemPrompt;
    
    protected BaseAgent(ILogger logger, IServiceProvider serviceProvider)
    {
        Logger = logger;
        ServiceProvider = serviceProvider;
    }

    /// <summary>
    /// Загружает системный промпт из ресурса
    /// </summary>
    protected virtual async Task<string> GetSystemPromptAsync()
    {
        if (_cachedSystemPrompt != null)
            return _cachedSystemPrompt;
            
        // Промпты хранятся в Core/Prompts/ и копируются в output
        var promptPath = Path.Combine(
            AppContext.BaseDirectory, "Prompts", PromptResourceName);
            
        if (!File.Exists(promptPath))
        {
            Logger.LogWarning("Prompt file {Path} not found, using default", promptPath);
            return GetDefaultPrompt();
        }
        
        _cachedSystemPrompt = await File.ReadAllTextAsync(promptPath);
        return _cachedSystemPrompt;
    }
    
    /// <summary>
    /// Получает экземпляр инструмента по типу
    /// </summary>
    protected T GetTool<T>() where T : IAgentTool
    {
        return ServiceProvider.GetRequiredService<T>();
    }
    
    protected virtual string GetDefaultPrompt() => 
        $"You are a {Role} agent in the AIAgentPlatform system.";
}