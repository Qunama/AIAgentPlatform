// ProjectAIAgent.Host/SignalRLoggingService.cs
using Microsoft.AspNetCore.SignalR;
using ProjectAIAgent.Host.Hubs;
using ProjectAIAgent.Core.Models;

namespace ProjectAIAgent.Host;

/// <summary>
/// Сервис для отправки событий оркестрации через SignalR.
/// Подписывается на ключевые моменты работы агента и транслирует их клиентам.
/// </summary>
public class SignalRLoggingService
{
    private readonly IHubContext<AgentHub> _hubContext;
    private readonly ILogger<SignalRLoggingService> _logger;

    public SignalRLoggingService(
        IHubContext<AgentHub> hubContext,
        ILogger<SignalRLoggingService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>Отправка обновления фазы</summary>
    public async Task SendPhaseUpdateAsync(WorkPhase phase)
    {
        var message = phase switch
        {
            WorkPhase.Planning => "Анализ запроса и планирование...",
            WorkPhase.Executing => "Выполнение инструментов...",
            WorkPhase.Validating => "Проверка изменений (сборка)...",
            WorkPhase.Documenting => "Обновление документации...",
            WorkPhase.Reporting => "Формирование отчёта...",
            WorkPhase.Idle => "Ожидание запроса",
            _ => phase.ToString()
        };

        await _hubContext.Clients.All.SendAsync("ProgressUpdate", new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            phase = phase.ToString(),
            message
        });
    }

    /// <summary>Отправка результата инструмента</summary>
    public async Task SendToolExecutionAsync(string toolName, bool success, string? summary = null)
    {
        await _hubContext.Clients.All.SendAsync("ToolExecuted", new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            tool = toolName,
            success,
            summary
        });
    }

    /// <summary>Отправка результата сборки</summary>
    public async Task SendBuildResultAsync(bool success, int attempt, string summary)
    {
        await _hubContext.Clients.All.SendAsync("BuildCompleted", new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            success,
            attempt,
            summary
        });
    }

    /// <summary>Отправка финального результата</summary>
    public async Task SendFinalResultAsync(bool success, string report)
    {
        await _hubContext.Clients.All.SendAsync("RequestCompleted", new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            success,
            report
        });
    }

    /// <summary>Отправка ошибки</summary>
    public async Task SendErrorAsync(string error)
    {
        await _hubContext.Clients.All.SendAsync("ErrorOccurred", new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            error
        });
    }

    /// <summary>Отправка лог-сообщения</summary>
    public async Task SendLogAsync(string level, string message)
    {
        await _hubContext.Clients.All.SendAsync("LogMessage", new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            level,
            message
        });
    }
}