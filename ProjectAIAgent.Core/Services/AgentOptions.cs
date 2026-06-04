// ProjectAIAgent.Core/Services/AgentOptions.cs
namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Конфигурация агента. Связывается с секцией "Agent" в appsettings.json.
/// </summary>
public class AgentOptions
{
    /// <summary>
    /// Путь к корневой папке .NET-проекта, с которым работает агент.
    /// Все относительные пути в инструментах разрешаются относительно этой папки.
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>Максимальное количество попыток исправления ошибок сборки</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Требовать подтверждение пользователя перед записью файлов</summary>
    public bool RequireConfirmation { get; set; } = true;

    /// <summary>Автоматически создавать коммиты Git при изменениях</summary>
    public bool AutoCommit { get; set; } = false;

    /// <summary>Отслеживать изменения в документации через FileSystemWatcher</summary>
    public bool WatchDocumentation { get; set; } = true;
}