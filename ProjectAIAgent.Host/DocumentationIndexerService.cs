// ProjectAIAgent.Host/DocumentationIndexerService.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Models;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Host;

/// <summary>
/// Фоновая служба, которая при старте приложения индексирует документацию проекта
/// и сохраняет чанки в Qdrant.
/// </summary>
public class DocumentationIndexerService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<QdrantOptions> _qdrantOptions;
    private readonly IOptions<AgentOptions> _agentOptions;
    private readonly ILogger<DocumentationIndexerService> _logger;

    public DocumentationIndexerService(
        IServiceProvider serviceProvider,
        IOptions<QdrantOptions> qdrantOptions,
        IOptions<AgentOptions> agentOptions,
        ILogger<DocumentationIndexerService> logger)
    {
        _serviceProvider = serviceProvider;
        _qdrantOptions = qdrantOptions;
        _agentOptions = agentOptions;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var projectPath = _agentOptions.Value.ProjectPath;

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            _logger.LogWarning(
                "ProjectPath is not configured. Skipping documentation indexing. " +
                "Set 'Agent:ProjectPath' in appsettings.json.");
            return;
        }

        if (!Directory.Exists(projectPath))
        {
            _logger.LogWarning(
                "ProjectPath '{Path}' does not exist. Skipping documentation indexing.",
                projectPath);
            return;
        }

        try
        {
            _logger.LogInformation("Starting documentation indexing for project: {Path}", projectPath);

            using var scope = _serviceProvider.CreateScope();

            var docService = scope.ServiceProvider.GetRequiredService<IDocumentationService>();
            var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();
            var qdrantOpts = _qdrantOptions.Value;

            // 1. Создать коллекцию в Qdrant, если её нет
            await qdrantService.EnsureCollectionExistsAsync(
                qdrantOpts.CollectionName,
                qdrantOpts.VectorSize,
                cancellationToken);

            // 2. Проиндексировать документацию
            var chunks = await docService.IndexDocumentationAsync(projectPath);

            if (chunks.Count == 0)
            {
                _logger.LogInformation("No documentation files found in {Path}", projectPath);
                return;
            }

            // 3. Очистить старые точки
            await qdrantService.ClearCollectionAsync(qdrantOpts.CollectionName, cancellationToken);

            // 4. Вставить новые точки
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
                        ["last_modified"] = c.LastModified.ToString("O")
                    }
                })
                .ToList();

            await qdrantService.UpsertPointsAsync(qdrantOpts.CollectionName, points, cancellationToken);

            _logger.LogInformation(
                "Documentation indexing complete. {Count} chunks saved to Qdrant collection '{Collection}'.",
                points.Count, qdrantOpts.CollectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index documentation");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}