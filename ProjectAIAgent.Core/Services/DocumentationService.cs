// ProjectAIAgent.Core/Services/DocumentationService.cs
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Models;

namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Сервис для поиска, разбиения на чанки и векторизации документации проекта.
/// </summary>
public class DocumentationService : IDocumentationService
{
    private readonly IOllamaEmbeddingClient _embeddingClient;
    private readonly IOptions<AgentOptions> _agentOptions;
    private readonly ILogger<DocumentationService> _logger;

    // Расширения файлов, которые считаются документацией
    private static readonly HashSet<string> DocumentationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".txt", ".rst", ".adoc"
    };

    // Директории, которые исключаются из поиска
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", "node_modules", ".vs", ".idea", "packages"
    };

    public DocumentationService(
        IOllamaEmbeddingClient embeddingClient,
        IOptions<AgentOptions> agentOptions,
        ILogger<DocumentationService> logger)
    {
        _embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
        _agentOptions = agentOptions ?? throw new ArgumentNullException(nameof(agentOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<List<DocumentChunk>> DiscoverDocumentsAsync(string rootPath)
    {
        var chunks = new List<DocumentChunk>();

        if (!Directory.Exists(rootPath))
        {
            _logger.LogWarning("Root path not found: {Path}", rootPath);
            return chunks;
        }

        _logger.LogInformation("Discovering documentation files in: {Path}", rootPath);

        var files = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(f => DocumentationExtensions.Contains(Path.GetExtension(f)))
            .Where(f => !IsExcludedDirectory(Path.GetRelativePath(rootPath, f)))
            .ToList();

        _logger.LogInformation("Found {Count} documentation files", files.Count);

        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var relativePath = Path.GetRelativePath(rootPath, file);
                var fileChunks = ChunkDocument(content, relativePath);
                chunks.AddRange(fileChunks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read file {File}", file);
            }
        }

        _logger.LogInformation("Total chunks created: {Count}", chunks.Count);
        return chunks;
    }

    /// <inheritdoc />
    public List<DocumentChunk> ChunkDocument(string content, string filePath, int maxChunkSize = 2000)
    {
        var chunks = new List<DocumentChunk>();

        if (string.IsNullOrWhiteSpace(content))
            return chunks;

        var lastModified = DateTime.UtcNow; // Будет переопределено при чтении файла

        // Разбиваем по секциям (заголовки ##)
        var sections = Regex.Split(content, @"(?=^##\s)", RegexOptions.Multiline);

        int globalIndex = 0;

        foreach (var section in sections)
        {
            if (string.IsNullOrWhiteSpace(section))
                continue;

            var sectionName = ExtractSectionName(section);

            // Если секция меньше maxChunkSize — берём целиком
            if (section.Length <= maxChunkSize)
            {
                chunks.Add(new DocumentChunk
                {
                    Content = section.Trim(),
                    FilePath = filePath,
                    Section = sectionName,
                    ChunkIndex = globalIndex++,
                    LastModified = lastModified
                });
            }
            else
            {
                // Разбиваем большую секцию на части по размеру
                var subChunks = SplitBySize(section, maxChunkSize);
                foreach (var subChunk in subChunks)
                {
                    chunks.Add(new DocumentChunk
                    {
                        Content = subChunk.Trim(),
                        FilePath = filePath,
                        Section = sectionName,
                        ChunkIndex = globalIndex++,
                        LastModified = lastModified
                    });
                }
            }
        }

        return chunks;
    }

    /// <inheritdoc />
    public async Task EmbedChunksAsync(List<DocumentChunk> chunks)
    {
        if (chunks.Count == 0)
            return;

        _logger.LogInformation("Generating embeddings for {Count} chunks...", chunks.Count);

        var texts = chunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingClient.GetEmbeddingsAsync(texts);

        for (int i = 0; i < chunks.Count; i++)
        {
            chunks[i].Embedding = embeddings[i];
        }

        _logger.LogInformation("Embeddings generated for {Count} chunks", chunks.Count);
    }

    /// <inheritdoc />
    public async Task<List<DocumentChunk>> IndexDocumentationAsync(string rootPath)
    {
        _logger.LogInformation("Starting full documentation indexing...");

        var chunks = await DiscoverDocumentsAsync(rootPath);

        if (chunks.Count > 0)
        {
            await EmbedChunksAsync(chunks);
        }

        _logger.LogInformation("Indexing complete. {Count} chunks indexed", chunks.Count);
        return chunks;
    }

    // Приватные хелперы

    private static string? ExtractSectionName(string section)
    {
        var match = Regex.Match(section, @"^##\s*(.+)", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static List<string> SplitBySize(string text, int maxSize)
    {
        var parts = new List<string>();
        for (int i = 0; i < text.Length; i += maxSize)
        {
            var length = Math.Min(maxSize, text.Length - i);
            parts.Add(text.Substring(i, length));
        }
        return parts;
    }

    private static bool IsExcludedDirectory(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => ExcludedDirectories.Contains(p));
    }
}