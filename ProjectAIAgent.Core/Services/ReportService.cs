// ProjectAIAgent.Core/Services/ReportService.cs
using System.Text;
using ProjectAIAgent.Core.Models;

namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Сервис для генерации форматированных отчётов о работе агента.
/// Используется OrchestratorAgent для представления результатов пользователю.
/// </summary>
public class ReportService
{
    /// <summary>
    /// Генерирует полный отчёт о выполненной работе.
    /// </summary>
    public string GenerateReport(ReportData data)
    {
        var sb = new StringBuilder();

        // Заголовок
        sb.AppendLine("╔══════════════════════════════════════════╗");
        sb.AppendLine("║         AI AGENT — ОТЧЁТ О РАБОТЕ       ║");
        sb.AppendLine("╚══════════════════════════════════════════╝");
        sb.AppendLine();

        // Запрос
        sb.AppendLine("📋 ЗАПРОС:");
        sb.AppendLine($"   {data.UserRequest}");
        sb.AppendLine();

        // Статус
        var statusIcon = data.Success ? "✅ УСПЕШНО" : "❌ С ОШИБКАМИ";
        sb.AppendLine($"📊 СТАТУС: {statusIcon}");
        sb.AppendLine();

        // Изменённые файлы
        if (data.ModifiedFiles.Count > 0)
        {
            sb.AppendLine("📁 ИЗМЕНЁННЫЕ ФАЙЛЫ:");
            foreach (var file in data.ModifiedFiles)
            {
                sb.AppendLine($"   • {file}");
            }
            sb.AppendLine();
        }

        // Результат валидации
        if (!string.IsNullOrWhiteSpace(data.ValidationResult))
        {
            sb.AppendLine("🔧 ВАЛИДАЦИЯ:");
            sb.AppendLine($"   {data.ValidationResult}");
            sb.AppendLine();
        }

        // Ошибки
        if (data.Errors.Count > 0)
        {
            sb.AppendLine($"⚠️ ОШИБКИ ({data.Errors.Count}):");
            foreach (var error in data.Errors)
            {
                sb.AppendLine($"   • {error}");
            }
            sb.AppendLine();
        }

        // Результат
        sb.AppendLine("📝 РЕЗУЛЬТАТ:");
        sb.AppendLine($"   {data.Message}");
        sb.AppendLine();

        // Инструменты
        if (data.ToolsUsed.Count > 0)
        {
            sb.AppendLine("🔧 ИСПОЛЬЗОВАННЫЕ ИНСТРУМЕНТЫ:");
            foreach (var tool in data.ToolsUsed.Distinct())
            {
                sb.AppendLine($"   • {tool}");
            }
            sb.AppendLine();
        }

        // Статистика
        sb.AppendLine("📈 СТАТИСТИКА:");
        sb.AppendLine($"   Длительность: {data.Duration.TotalSeconds:F1} сек");
        sb.AppendLine($"   Вызовов LLM: {data.LlmCallCount}");
        sb.AppendLine($"   Вызовов инструментов: {data.ToolCallCount}");
        sb.AppendLine($"   Ошибок: {data.Errors.Count}");
        sb.AppendLine();

        sb.AppendLine("══════════════════════════════════════════");

        return sb.ToString();
    }

    /// <summary>
    /// Генерирует краткий отчёт (одна строка).
    /// </summary>
    public string GenerateShortReport(ReportData data)
    {
        var status = data.Success ? "✅" : "❌";
        var files = data.ModifiedFiles.Count > 0
            ? $" | Изменено файлов: {data.ModifiedFiles.Count}"
            : "";
        return $"{status} {data.Message}{files}";
    }
}

/// <summary>
/// Данные для генерации отчёта.
/// </summary>
public class ReportData
{
    /// <summary>Исходный запрос пользователя</summary>
    public string UserRequest { get; set; } = string.Empty;

    /// <summary>Финальное сообщение от LLM</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Успешно ли выполнен запрос</summary>
    public bool Success { get; set; } = true;

    /// <summary>Список изменённых файлов</summary>
    public List<string> ModifiedFiles { get; set; } = new();

    /// <summary>Результат валидации (сборки)</summary>
    public string? ValidationResult { get; set; }

    /// <summary>Список ошибок</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Список использованных инструментов</summary>
    public List<string> ToolsUsed { get; set; } = new();

    /// <summary>Общая длительность выполнения</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Количество вызовов LLM</summary>
    public int LlmCallCount { get; set; }

    /// <summary>Количество вызовов инструментов</summary>
    public int ToolCallCount { get; set; }
}