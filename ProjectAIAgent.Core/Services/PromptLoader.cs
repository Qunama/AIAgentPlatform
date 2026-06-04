// ProjectAIAgent.Core/Services/PromptLoader.cs
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Загружает системные промпты из файлов Prompts/*.txt.
/// Кеширует их в памяти для быстрого доступа.
/// </summary>
public class PromptLoader
{
    private readonly ConcurrentDictionary<string, string> _cache = new();
    private readonly ILogger<PromptLoader> _logger;

    public PromptLoader(ILogger<PromptLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Загружает содержимое файла промпта. Файлы ищутся относительно папки Prompts/.
    /// </summary>
    /// <param name="fileName">Имя файла (например, "orchestrator.txt")</param>
    /// <returns>Содержимое файла</returns>
    public async Task<string> LoadPromptAsync(string fileName)
    {
        // Особая обработка для пустого промпта
        if (fileName == "__empty__")
            return string.Empty;

        if (_cache.TryGetValue(fileName, out var cached))
            return cached;

        // Ищем файл относительно нескольких возможных путей
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "Prompts", fileName),
            // Для запуска из Host-проекта, когда Core — соседняя папка
            Path.Combine(Directory.GetCurrentDirectory(), "..", "ProjectAIAgent.Core", "Prompts", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "ProjectAIAgent.Core", "Prompts", fileName)
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                _logger.LogDebug("Загрузка промпта: {Path}", path);
                var content = await File.ReadAllTextAsync(path);
                _cache.TryAdd(fileName, content);
                return content;
            }
        }

        throw new FileNotFoundException(
            $"Prompt file '{fileName}' not found. Searched paths: {string.Join("; ", possiblePaths)}");
    }

    /// <summary>
    /// Очищает кеш промптов (полезно при изменении файлов во время отладки).
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _logger.LogInformation("Кеш промптов очищен");
    }
}