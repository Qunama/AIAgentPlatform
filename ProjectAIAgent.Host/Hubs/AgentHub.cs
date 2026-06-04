// ProjectAIAgent.Host/Hubs/AgentHub.cs
using Microsoft.AspNetCore.SignalR;

namespace ProjectAIAgent.Host.Hubs;

/// <summary>
/// SignalR хаб для real-time уведомлений о работе агента.
/// Клиенты подписываются на события: прогресс, логи, результат, ошибки.
/// </summary>
public class AgentHub : Hub
{
    /// <summary>Вызывается при подключении клиента</summary>
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync("Connected", $"Connected to Agent Hub. ConnectionId: {Context.ConnectionId}");
    }

    /// <summary>Вызывается при отключении клиента</summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    // Методы для вызова СЕРВЕРОМ (не клиентом):

    /// <summary>Отправка прогресса выполнения</summary>
    public async Task SendProgress(string phase, string message)
    {
        await Clients.All.SendAsync("ProgressUpdate", new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            phase,
            message
        });
    }

    /// <summary>Отправка лог-сообщения</summary>
    public async Task SendLog(string level, string message)
    {
        await Clients.All.SendAsync("LogMessage", new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            level,
            message
        });
    }

    /// <summary>Отправка финального результата</summary>
    public async Task SendResult(bool success, string report)
    {
        await Clients.All.SendAsync("RequestCompleted", new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            success,
            report
        });
    }

    /// <summary>Отправка ошибки</summary>
    public async Task SendError(string error)
    {
        await Clients.All.SendAsync("ErrorOccurred", new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            error
        });
    }
}