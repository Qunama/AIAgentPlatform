// ProjectAIAgent.Core/Agents/ContextAgent.cs
using Microsoft.Extensions.Logging;
using ProjectAIAgent.Core.Models;

namespace ProjectAIAgent.Core.Agents;

/// <summary>
/// Агент управления контекстом проекта.
/// Хранит состояние, историю взаимодействий и предоставляет сводку для других агентов.
/// </summary>
public class ContextAgent : BaseAgent
{
    public override string Name => "ContextAgent";
    public override string Role => "Context";
    protected override string PromptResourceName => "orchestrator.txt"; // Не используется напрямую

    private readonly ProjectContext _context;

    public ContextAgent(
        ILogger<ContextAgent> logger,
        IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
        _context = new ProjectContext();
    }

    /// <summary>
    /// Возвращает текущий контекст проекта.
    /// </summary>
    public ProjectContext GetContext() => _context;

    /// <summary>
    /// Устанавливает путь к проекту.
    /// </summary>
    public void SetProjectPath(string path)
    {
        _context.ProjectPath = path;
        Logger.LogInformation("ContextAgent: Project path set to {Path}", path);
    }

    /// <summary>
    /// Кеширует структуру проекта.
    /// </summary>
    public void CacheProjectStructure(string structure)
    {
        _context.CachedProjectStructure = structure;
        _context.StructureLastUpdated = DateTime.UtcNow;
        Logger.LogInformation("ContextAgent: Project structure cached ({Length} chars)", structure.Length);
    }

    /// <summary>
    /// Регистрирует изменённый файл.
    /// </summary>
    public void RegisterFileModification(string filePath)
    {
        if (!_context.ModifiedFiles.Contains(filePath))
        {
            _context.ModifiedFiles.Add(filePath);
        }
    }

    /// <summary>
    /// Регистрирует ошибку.
    /// </summary>
    public void RegisterError()
    {
        _context.ErrorCount++;
    }

    /// <summary>
    /// Записывает взаимодействие в историю.
    /// </summary>
    public void RecordInteraction(string request, string response, bool success)
    {
        _context.RecordInteraction(request, response, success);
    }

    /// <summary>
    /// Устанавливает текущую фазу работы.
    /// </summary>
    public void SetPhase(WorkPhase phase)
    {
        _context.CurrentPhase = phase;
        Logger.LogDebug("ContextAgent: Phase changed to {Phase}", phase);
    }

    /// <summary>
    /// Формирует сводку контекста для включения в системный промпт оркестратора.
    /// </summary>
    public string GetContextSummary()
    {
        var summary = _context.GetContextSummary();

        // Добавляем структуру проекта, если она закеширована
        if (_context.CachedProjectStructure != null && _context.StructureLastUpdated.HasValue)
        {
            var age = DateTime.UtcNow - _context.StructureLastUpdated.Value;
            summary += $"\nProject structure (cached {age.TotalMinutes:F0} min ago):\n{_context.CachedProjectStructure}";
        }

        // Добавляем историю последних взаимодействий
        if (_context.History.Count > 0)
        {
            summary += "\n\nRecent interactions:";
            foreach (var record in _context.History.TakeLast(3))
            {
                var status = record.Success ? "OK" : "FAIL";
                summary += $"\n  [{record.Timestamp:HH:mm}] {status}: {record.Request.Truncate(60)}";
            }
        }

        return summary;
    }

    /// <summary>
    /// Сбрасывает счётчик ошибок для новой сессии.
    /// </summary>
    public void ResetErrors()
    {
        _context.ErrorCount = 0;
    }
}