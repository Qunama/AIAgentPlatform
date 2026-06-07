// ProjectAIAgent.Host/DocumentationIndexerService.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Models;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Host;

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

        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
        {
            _logger.LogWarning("ProjectPath not configured or not found. Skipping indexing.");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var docService = scope.ServiceProvider.GetRequiredService<IDocumentationService>();
            var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();
            var qdrantOpts = _qdrantOptions.Value;

            // === Индексация документации ===
            _logger.LogInformation("Indexing documentation...");
            await qdrantService.EnsureCollectionExistsAsync(qdrantOpts.CollectionName, qdrantOpts.VectorSize, cancellationToken);

            var docChunks = await docService.IndexDocumentationAsync(projectPath);
            if (docChunks.Count > 0)
            {
                await qdrantService.ClearCollectionAsync(qdrantOpts.CollectionName, cancellationToken);
                var docPoints = docChunks.Where(c => c.Embedding != null).Select(c => new QdrantPoint
                {
                    Id = c.Id, Vector = c.Embedding!,
                    Payload = new Dictionary<string, object>
                    {
                        ["content"] = c.Content, ["file_path"] = c.FilePath,
                        ["section"] = c.Section ?? "", ["chunk_index"] = c.ChunkIndex
                    }
                }).ToList();
                await qdrantService.UpsertPointsAsync(qdrantOpts.CollectionName, docPoints, cancellationToken);
                _logger.LogInformation("Documentation: {Count} chunks indexed", docPoints.Count);
            }

            // === Индексация исходного кода ===
            _logger.LogInformation("Indexing source code...");
            var codeCollection = qdrantOpts.CollectionName + "_code";
            await qdrantService.EnsureCollectionExistsAsync(codeCollection, qdrantOpts.VectorSize, cancellationToken);

            var codeChunks = await docService.IndexCodebaseAsync(projectPath);
            if (codeChunks.Count > 0)
            {
                await qdrantService.ClearCollectionAsync(codeCollection, cancellationToken);
                var codePoints = codeChunks.Where(c => c.Embedding != null).Select(c => new QdrantPoint
                {
                    Id = c.Id, Vector = c.Embedding!,
                    Payload = new Dictionary<string, object>
                    {
                        ["content"] = c.Content, ["file_path"] = c.FilePath,
                        ["namespace"] = c.Namespace ?? "", ["class_name"] = c.ClassName ?? "",
                        ["method_name"] = c.MethodName ?? "", ["signature"] = c.Signature ?? "",
                        ["start_line"] = c.StartLine, ["end_line"] = c.EndLine
                    }
                }).ToList();
                await qdrantService.UpsertPointsAsync(codeCollection, codePoints, cancellationToken);
                _logger.LogInformation("Source code: {Count} chunks indexed", codePoints.Count);
            }

            _logger.LogInformation("Indexing complete. Docs: {DocCount}, Code: {CodeCount}",
                docChunks.Count, codeChunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Indexing failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}