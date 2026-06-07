// ProjectAIAgent.Core/Services/OllamaOptions.cs
namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Конфигурация для подключения к Ollama и параметров генерации.
/// Связывается с секцией "Ollama" в appsettings.json.
/// </summary>
public class OllamaOptions
{
    /// <summary>Базовый URL Ollama API (по умолчанию http://localhost:11434)</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Название модели (например, qwen2.5-coder:14b-instruct-q4_K_M)</summary>
    public string Model { get; set; } = "qwen2.5-coder:14b-instruct-q4_K_M";

    /// <summary>Максимальное количество токенов в ответе</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>Температура генерации (0.0 — детерминировано, 1.0 — творчески)</summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>Top-p сэмплирование (0.0 — 1.0)</summary>
    public double TopP { get; set; } = 0.9;

    /// <summary>Таймаут одного HTTP-запроса в секундах</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Максимальное количество повторных попыток при ошибке</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Базовая задержка между повторами в секундах (удваивается с каждой попыткой)</summary>
    public double RetryDelaySeconds { get; set; } = 2.0;

    /// <summary>Общий таймаут на все попытки в секундах</summary>
    public int TotalTimeoutSeconds { get; set; } = 300;

    /// <summary>Модели для разных типов задач</summary>
    public Dictionary<string, string> ModelByTask { get; set; } = new()
    {
        ["default"] = "qwen2.5-coder:14b-instruct-q4_K_M",
        ["simple"] = "qwen2.5-coder:7b-instruct",
        ["refactoring"] = "deepseek-coder-v2:16b-lite-instruct-q4_K_M"
    };

    /// <summary>Модель для эмбеддингов (должна поддерживать embeddings capability)</summary>
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}