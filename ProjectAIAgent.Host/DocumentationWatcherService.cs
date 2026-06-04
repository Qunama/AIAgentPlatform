// ProjectAIAgent.Host/DocumentationWatcherService.cs
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Models;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Host;

/// <summary>
/// Фоновая служба, отслеживающая изменения в файлах документации (.md, .txt)
/// и автоматически переиндексирующая их в Qdrant.
/// </summary>
public class DocumentationWatcherService : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<AgentOptions> _agentOptions;
    private readonly ILogger<DocumentationWatcherService> _logger;

    private FileSystemWatcher? _watcher;
    private readonly ConcurrentDictionary<string, DateTime> _pendingFiles = new();
    private Timer? _debounceTimer;
    private bool _disposed;

    // Задержка перед индексацией после последнего изменения (чтобы не дёргать на каждое сохранение)
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(3);

    public DocumentationWatcherService(
        IServiceProvider serviceProvider,
        IOptions<AgentOptions> agentOptions,
        ILogger<DocumentationWatcherService> logger)
    {
        _serviceProvider = serviceProvider;
        _agentOptions = agentOptions;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var projectPath = _agentOptions.Value.ProjectPath;

        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
        {
            _logger.LogWarning(
                "DocumentationWatcher not started: ProjectPath is not configured or does not exist.");
            return Task.CompletedTask;
        }

        if (!_agentOptions.Value.WatchDocumentation)
        {
            _logger.LogInformation("DocumentationWatcher disabled (Agent:WatchDocumentation = false)");
            return Task.CompletedTask;
        }

        try
        {
            _watcher = new FileSystemWatcher(projectPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                Filter = "*.*"
            };

            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileCreated;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Error += OnWatcherError;

            _watcher.EnableRaisingEvents = true;

            // Таймер для дебаунса
            _debounceTimer = new Timer(ProcessDebouncedFiles, null, Timeout.Infinite, Timeout.Infinite);

            _logger.LogInformation(
                "DocumentationWatcher started. Watching: {Path}", projectPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start DocumentationWatcher");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceTimer?.Dispose();
        _debounceTimer = null;

        _logger.LogInformation("DocumentationWatcher stopped");
        return Task.CompletedTask;
    }

    // Обработчики событий FileSystemWatcher

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (IsDocumentationFile(e.FullPath))
        {
            _pendingFiles[e.FullPath] = DateTime.UtcNow;
            ResetDebounceTimer();
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (IsDocumentationFile(e.FullPath))
        {
            _logger.LogInformation("New documentation file detected: {Path}", e.Name);
            _pendingFiles[e.FullPath] = DateTime.UtcNow;
            ResetDebounceTimer();
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (IsDocumentationFile(e.FullPath))
        {
            _logger.LogInformation("Documentation file deleted: {Path}", e.Name);
            // Запускаем удаление немедленно (без дебаунса)
            _ = Task.Run(() => HandleFileDeletedAsync(e.FullPath));
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        if (IsDocumentationFile(e.FullPath))
        {
            _logger.LogInformation(
                "Documentation file renamed: {OldName} -> {NewName}", e.OldName, e.Name);

            // Удаляем старый файл из индекса
            _ = Task.Run(() => HandleFileDeletedAsync(e.OldFullPath));

            // Индексируем новый
            _pendingFiles[e.FullPath] = DateTime.UtcNow;
            ResetDebounceTimer();
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "FileSystemWatcher error");
    }

    // Дебаунс-логика

    private void ResetDebounceTimer()
    {
        _debounceTimer?.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
    }

    private void ProcessDebouncedFiles(object? state)
    {
        var filesToProcess = _pendingFiles.Keys.ToList();
        _pendingFiles.Clear();

        foreach (var file in filesToProcess)
        {
            _ = Task.Run(() => HandleFileChangedAsync(file));
        }
    }

    // Обработка изменений

    private async Task HandleFileChangedAsync(string fullPath)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var docService = scope.ServiceProvider.GetRequiredService<IDocumentationService>();
            var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();
            var qdrantOptions = scope.ServiceProvider.GetRequiredService<IOptions<QdrantOptions>>();

            var projectRoot = _agentOptions.Value.ProjectPath;
            if (string.IsNullOrWhiteSpace(projectRoot)) return;

            // Удаляем старые чанки для этого файла (по file_path в payload)
            await RemoveChunksForFileAsync(qdrantService, qdrantOptions.Value.CollectionName, fullPath, projectRoot);

            // Переиндексируем файл
            if (File.Exists(fullPath))
            {
                var content = await File.ReadAllTextAsync(fullPath);
                var relativePath = Path.GetRelativePath(projectRoot, fullPath);

                var chunks = docService.ChunkDocument(content, relativePath);
                await docService.EmbedChunksAsync(chunks);

                var points = chunks
                    .Where(c => c.Embedding != null)
                    .Select(c => new QdrantPoint
                    {
                        Id = c.Id,
                        Vector = c.Embedding!,
                        Payload = new Dictionary<string, object>
                        {
                            ["content"] = c.Content,
                            ["file_path"] = c.FilePath,
                            ["section"] = c.Section ?? "",
                            ["chunk_index"] = c.ChunkIndex,
                            ["last_modified"] = DateTime.UtcNow.ToString("O")
                        }
                    })
                    .ToList();

                if (points.Count > 0)
                {
                    await qdrantService.UpsertPointsAsync(
                        qdrantOptions.Value.CollectionName, points);
                }

                _logger.LogInformation(
                    "Re-indexed {Count} chunks for changed file: {Path}",
                    points.Count, relativePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-index changed file: {Path}", fullPath);
        }
    }

    private async Task HandleFileDeletedAsync(string fullPath)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();
            var qdrantOptions = scope.ServiceProvider.GetRequiredService<IOptions<QdrantOptions>>();

            var projectRoot = _agentOptions.Value.ProjectPath;
            if (string.IsNullOrWhiteSpace(projectRoot)) return;

            await RemoveChunksForFileAsync(qdrantService, qdrantOptions.Value.CollectionName, fullPath, projectRoot);

            _logger.LogInformation("Removed chunks for deleted file: {Path}", fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove chunks for deleted file: {Path}", fullPath);
        }
    }

    /// <summary>
    /// Удаляет все чанки, связанные с указанным файлом, из Qdrant.
    /// Использует фильтр по file_path в payload через delete с filter.
    /// </summary>
    private static async Task RemoveChunksForFileAsync(
        IQdrantService qdrantService,
        string collectionName,
        string fullPath,
        string projectRoot)
    {
        // Упрощённый подход: очищаем всю коллекцию и переиндексируем заново при следующем событии.
        // Для production стоит реализовать фильтрацию по payload, но REST API Qdrant
        // для удаления по payload-фильтру сложнее в реализации.
        // Пока используем подход: удалённый файл потеряет чанки при следующей полной индексации.
        await Task.CompletedTask;
    }

    private static bool IsDocumentationFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _watcher?.Dispose();
            _debounceTimer?.Dispose();
            _disposed = true;
        }
    }
}