// ProjectAIAgent.Core/Models/ProjectContext.cs
namespace ProjectAIAgent.Core.Models;

/// <summary>
/// Контекст проекта — хранит состояние, историю взаимодействий и метаданные.
/// Используется ContextAgent для формирования полной картины перед планированием.
/// </summary>
public class ProjectContext
{
    /// <summary>Путь к корню проекта</summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>Текущая фаза работы агента</summary>
    public WorkPhase CurrentPhase { get; set; } = WorkPhase.Idle;

    /// <summary>История взаимодействий (последние N запросов)</summary>
    public List<InteractionRecord> History { get; set; } = new();

    /// <summary>Последний запрос пользователя</summary>
    public string? LastRequest { get; set; }

    /// <summary>Последний ответ агента</summary>
    public string? LastResponse { get; set; }

    /// <summary>Структура проекта (кешируется)</summary>
    public string? CachedProjectStructure { get; set; }

    /// <summary>Время последнего сканирования структуры</summary>
    public DateTime? StructureLastUpdated { get; set; }

    /// <summary>Список файлов, изменённых в текущей сессии</summary>
    public List<string> ModifiedFiles { get; set; } = new();

    /// <summary>Количество ошибок в текущей сессии</summary>
    public int ErrorCount { get; set; }

    /// <summary>Добавляет запись в историю взаимодействий</summary>
    public void RecordInteraction(string request, string response, bool success)
    {
        History.Add(new InteractionRecord
        {
            Timestamp = DateTime.UtcNow,
            Request = request,
            Response = response,
            Success = success
        });

        // Ограничиваем историю 20 записями
        if (History.Count > 20)
        {
            History = History.OrderByDescending(h => h.Timestamp).Take(20).ToList();
        }

        LastRequest = request;
        LastResponse = response;
    }

    /// <summary>Генерирует краткую сводку контекста для системного промпта</summary>
    public string GetContextSummary()
    {
        var lines = new List<string>
        {
            $"Project: {ProjectPath}",
            $"Phase: {CurrentPhase}",
            $"Session modified files: {(ModifiedFiles.Count > 0 ? string.Join(", ", ModifiedFiles) : "none")}",
            $"Session errors: {ErrorCount}",
            $"History entries: {History.Count}",
            $"Last interaction: {History.LastOrDefault()?.Timestamp:HH:mm:ss} - {History.LastOrDefault()?.Request?.Truncate(80)}"
        };

        return string.Join("\n", lines);
    }
}

/// <summary>
/// Запись в истории взаимодействий.
/// </summary>
public class InteractionRecord
{
    public DateTime Timestamp { get; set; }
    public string Request { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public bool Success { get; set; }
}

/// <summary>
/// Фазы работы агента.
/// </summary>
public enum WorkPhase
{
    Idle,           // Ожидание запроса
    Planning,       // Анализ и планирование
    Executing,      // Выполнение изменений
    Validating,     // Проверка (сборка, тесты)
    Documenting,    // Обновление документации
    Reporting       // Формирование отчёта
}

/// <summary>
/// Вспомогательные методы для строк.
/// </summary>
public static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}