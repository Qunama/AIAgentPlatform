// ProjectAIAgent.Core/Models/ChangePlan.cs
using System.Text;

namespace ProjectAIAgent.Core.Models;

/// <summary>
/// План изменений — описывает, какие файлы нужно изменить, в каком порядке, и что именно сделать.
/// Формируется OrchestratorAgent на основе запроса пользователя и используется для делегирования.
/// </summary>
public class ChangePlan
{
    /// <summary>Уникальный идентификатор плана</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Исходный запрос пользователя, породивший план</summary>
    public string UserRequest { get; set; } = string.Empty;

    /// <summary>Краткое описание плана (одно предложение)</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Список шагов для выполнения</summary>
    public List<ChangeStep> Steps { get; set; } = new();

    /// <summary>Время создания плана</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Текущий статус выполнения плана</summary>
    public PlanStatus Status { get; set; } = PlanStatus.Created;

    /// <summary>Общее количество шагов</summary>
    public int TotalSteps => Steps.Count;

    /// <summary>Количество завершённых шагов</summary>
    public int CompletedSteps => Steps.Count(s => s.Status == StepStatus.Completed);

    /// <summary>Количество шагов с ошибками</summary>
    public int FailedSteps => Steps.Count(s => s.Status == StepStatus.Failed);

    /// <summary>Прогресс выполнения в процентах (0-100)</summary>
    public int ProgressPercent => TotalSteps == 0 ? 100 : (int)((double)CompletedSteps / TotalSteps * 100);

    /// <summary>
    /// Возвращает следующий невыполненный шаг или null, если все выполнены.
    /// </summary>
    public ChangeStep? GetNextPendingStep()
    {
        return Steps.FirstOrDefault(s => s.Status == StepStatus.Pending);
    }

    /// <summary>
    /// Отмечает шаг как выполненный.
    /// </summary>
    public void MarkStepCompleted(int stepIndex, string? result = null)
    {
        if (stepIndex >= 0 && stepIndex < Steps.Count)
        {
            Steps[stepIndex].Status = StepStatus.Completed;
            Steps[stepIndex].CompletedAt = DateTime.UtcNow;
            Steps[stepIndex].Result = result;
        }

        UpdateOverallStatus();
    }

    /// <summary>
    /// Отмечает шаг как проваленный.
    /// </summary>
    public void MarkStepFailed(int stepIndex, string error)
    {
        if (stepIndex >= 0 && stepIndex < Steps.Count)
        {
            Steps[stepIndex].Status = StepStatus.Failed;
            Steps[stepIndex].Error = error;
        }

        UpdateOverallStatus();
    }

    /// <summary>
    /// Отмечает шаг как пропущенный.
    /// </summary>
    public void MarkStepSkipped(int stepIndex, string reason)
    {
        if (stepIndex >= 0 && stepIndex < Steps.Count)
        {
            Steps[stepIndex].Status = StepStatus.Skipped;
            Steps[stepIndex].Error = reason;
        }

        UpdateOverallStatus();
    }

    private void UpdateOverallStatus()
    {
        if (Steps.Count == 0)
            Status = PlanStatus.Completed;
        else if (Steps.All(s => s.Status == StepStatus.Completed || s.Status == StepStatus.Skipped))
            Status = PlanStatus.Completed;
        else if (Steps.Any(s => s.Status == StepStatus.Failed))
            Status = PlanStatus.Failed;
        else if (Steps.Any(s => s.Status == StepStatus.InProgress || s.Status == StepStatus.Completed))
            Status = PlanStatus.InProgress;
        else
            Status = PlanStatus.Created;
    }

    /// <summary>
    /// Генерирует текстовое представление плана для системного промпта.
    /// </summary>
    public string ToPromptString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Change Plan [{Id}] — {Status}");
        sb.AppendLine($"Summary: {Summary}");
        sb.AppendLine($"Progress: {CompletedSteps}/{TotalSteps} steps completed ({ProgressPercent}%)");
        sb.AppendLine();

        for (int i = 0; i < Steps.Count; i++)
        {
            var step = Steps[i];
            var statusIcon = step.Status switch
            {
                StepStatus.Pending => "⏳",
                StepStatus.InProgress => "🔄",
                StepStatus.Completed => "✅",
                StepStatus.Failed => "❌",
                StepStatus.Skipped => "⏭️",
                _ => "❓"
            };

            sb.AppendLine($"{statusIcon} Step {i + 1}: [{step.ToolName}] {step.Description}");
            if (!string.IsNullOrEmpty(step.FilePath))
                sb.AppendLine($"   File: {step.FilePath}");
            if (!string.IsNullOrEmpty(step.Result))
                sb.AppendLine($"   Result: {step.Result}");
            if (!string.IsNullOrEmpty(step.Error))
                sb.AppendLine($"   Error: {step.Error}");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Один шаг в плане изменений — вызов конкретного инструмента с аргументами.
/// </summary>
public class ChangeStep
{
    /// <summary>Порядковый номер шага (0-based)</summary>
    public int Order { get; set; }

    /// <summary>Имя инструмента для вызова (read_file, write_file, run_shell_command, ...)</summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>Человекочитаемое описание шага</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Путь к файлу, с которым работает шаг (если применимо)</summary>
    public string? FilePath { get; set; }

    /// <summary>Аргументы для инструмента</summary>
    public Dictionary<string, object> Arguments { get; set; } = new();

    /// <summary>Текущий статус шага</summary>
    public StepStatus Status { get; set; } = StepStatus.Pending;

    /// <summary>Результат выполнения (если успешно)</summary>
    public string? Result { get; set; }

    /// <summary>Ошибка (если шаг провален)</summary>
    public string? Error { get; set; }

    /// <summary>Время завершения шага</summary>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Статус выполнения плана изменений.
/// </summary>
public enum PlanStatus
{
    Created,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Статус отдельного шага в плане.
/// </summary>
public enum StepStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}