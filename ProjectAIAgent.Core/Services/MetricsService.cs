// ProjectAIAgent.Core/Services/MetricsService.cs
using System.Collections.Concurrent;

namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Сервис для сбора и хранения метрик качества работы агента.
/// Метрики накапливаются между запросами и доступны для отображения.
/// </summary>
public class MetricsService
{
    private readonly ConcurrentBag<RequestMetrics> _history = new();

    /// <summary>Общее количество запросов</summary>
    public int TotalRequests => _history.Count;

    /// <summary>Количество успешных запросов</summary>
    public int SuccessfulRequests => _history.Count(m => m.Success);

    /// <summary>Количество проваленных запросов</summary>
    public int FailedRequests => _history.Count(m => !m.Success);

    /// <summary>Процент успешных запросов (0-100)</summary>
    public double SuccessRate => TotalRequests == 0 ? 100.0 : (double)SuccessfulRequests / TotalRequests * 100;

    /// <summary>Среднее время выполнения запроса</summary>
    public TimeSpan AverageDuration => TotalRequests == 0
        ? TimeSpan.Zero
        : TimeSpan.FromMilliseconds(_history.Average(m => m.DurationMs));

    /// <summary>Общее количество вызовов LLM</summary>
    public int TotalLlmCalls => _history.Sum(m => m.LlmCallCount);

    /// <summary>Общее количество вызовов инструментов</summary>
    public int TotalToolCalls => _history.Sum(m => m.ToolCallCount);

    /// <summary>Самые используемые инструменты</summary>
    public Dictionary<string, int> TopTools => _history
        .SelectMany(m => m.ToolsUsed)
        .GroupBy(t => t)
        .OrderByDescending(g => g.Count())
        .Take(5)
        .ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Количество успешных сборок</summary>
    public int SuccessfulBuilds => _history.Count(m => m.BuildSuccess);

    /// <summary>Общее количество попыток сборок</summary>
    public int TotalBuildAttempts => _history.Count(m => m.BuildAttempted);

    /// <summary>Процент успешных сборок</summary>
    public double BuildSuccessRate => TotalBuildAttempts == 0
        ? 100.0
        : (double)SuccessfulBuilds / TotalBuildAttempts * 100;

    /// <summary>Последние N метрик</summary>
    public IReadOnlyList<RequestMetrics> RecentMetrics => _history.TakeLast(10).ToList();

    /// <summary>
    /// Записывает метрики выполненного запроса.
    /// </summary>
    public void Record(RequestMetrics metrics)
    {
        _history.Add(metrics);
    }

    /// <summary>
    /// Возвращает сводку метрик в человекочитаемом виде.
    /// </summary>
    public string GetSummary()
    {
        return $@"
📊 МЕТРИКИ КАЧЕСТВА
═══════════════════════════════════════
📋 Запросов:        {TotalRequests} (✅ {SuccessfulRequests} / ❌ {FailedRequests})
📈 Успешность:      {SuccessRate:F1}%
⏱️ Среднее время:   {AverageDuration.TotalSeconds:F1} сек
🤖 Вызовов LLM:      {TotalLlmCalls}
🔧 Инструментов:     {TotalToolCalls}
🔨 Сборок:           {SuccessfulBuilds}/{TotalBuildAttempts} успешно ({BuildSuccessRate:F1}%)
🔝 Топ инструментов: {string.Join(", ", TopTools.Select(kv => $"{kv.Key}({kv.Value})"))}
═══════════════════════════════════════";
    }
}

/// <summary>
/// Метрики одного запроса.
/// </summary>
public class RequestMetrics
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Request { get; set; } = string.Empty;
    public bool Success { get; set; }
    public double DurationMs { get; set; }
    public int LlmCallCount { get; set; }
    public int ToolCallCount { get; set; }
    public List<string> ToolsUsed { get; set; } = new();
    public bool BuildAttempted { get; set; }
    public bool BuildSuccess { get; set; }
    public int ErrorCount { get; set; }
}