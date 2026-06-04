// ProjectAIAgent.Core/Tools/UpdateDocumentationTool.cs
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Models;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Core.Tools;

[AgentTool("update_documentation", "Updates or creates a section in a documentation file. " +
    "After updating, re-indexes the file in Qdrant so search results stay current.")]
public class UpdateDocumentationTool : IAgentTool
{
    private readonly IDocumentationService _documentationService;
    private readonly IOllamaEmbeddingClient _embeddingClient;
    private readonly IQdrantService _qdrantService;
    private readonly IOptions<AgentOptions> _agentOptions;
    private readonly IOptions<QdrantOptions> _qdrantOptions;
    private readonly ILogger<UpdateDocumentationTool> _logger;

    public string ToolName => "update_documentation";

    public string Description => "Updates or creates a documentation section in a .md or .txt file. " +
        "After writing, re-indexes the file so the changes are searchable. " +
        "If the file doesn't exist, it will be created. " +
        "If the section already exists (## Section Name), it will be replaced.";

    public string ParametersSchema => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            file_path = new
            {
                type = "string",
                description = "Path to the documentation file (relative to project root)"
            },
            section = new
            {
                type = "string",
                description = "Section name (without ## prefix). If empty, content is appended to the end of the file."
            },
            content = new
            {
                type = "string",
                description = "Content to write in the section (markdown format)"
            }
        },
        required = new[] { "file_path", "content" }
    });

    public UpdateDocumentationTool(
        IDocumentationService documentationService,
        IOllamaEmbeddingClient embeddingClient,
        IQdrantService qdrantService,
        IOptions<AgentOptions> agentOptions,
        IOptions<QdrantOptions> qdrantOptions,
        ILogger<UpdateDocumentationTool> logger)
    {
        _documentationService = documentationService ?? throw new ArgumentNullException(nameof(documentationService));
        _embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _agentOptions = agentOptions ?? throw new ArgumentNullException(nameof(agentOptions));
        _qdrantOptions = qdrantOptions ?? throw new ArgumentNullException(nameof(qdrantOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("file_path", out var pathObj) || pathObj is not string filePath)
            return ToolResult.Fail("Parameter 'file_path' is required");

        if (!parameters.TryGetValue("content", out var contentObj) || contentObj is not string content)
            return ToolResult.Fail("Parameter 'content' is required");

        var section = parameters.TryGetValue("section", out var sectionObj) && sectionObj is string s
            ? s.Trim()
            : null;

        try
        {
            var projectRoot = _agentOptions.Value.ProjectPath;
            if (string.IsNullOrWhiteSpace(projectRoot))
                return ToolResult.Fail("ProjectPath is not configured in Agent settings");

            var fullPath = GetFullPath(projectRoot, filePath);

            // Читаем или создаём файл
            string existingContent;
            bool isNewFile;

            if (File.Exists(fullPath))
            {
                existingContent = await File.ReadAllTextAsync(fullPath);
                isNewFile = false;
            }
            else
            {
                existingContent = string.Empty;
                isNewFile = true;
            }

            // Обновляем контент
            string newContent;
            if (!string.IsNullOrEmpty(section))
            {
                newContent = ReplaceOrAddSection(existingContent, section, content);
            }
            else
            {
                // Без секции — дописываем в конец
                newContent = string.IsNullOrEmpty(existingContent)
                    ? content
                    : existingContent + "\n\n" + content;
            }

            // Записываем файл
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(fullPath, newContent);

            _logger.LogInformation(
                "Updated documentation file: {Path} (new={IsNew}, section={Section})",
                fullPath, isNewFile, section ?? "(none)");

            // Реиндексация файла
            var metadata = new Dictionary<string, object>
            {
                ["file_path"] = filePath,
                ["is_new_file"] = isNewFile,
                ["section"] = section ?? "(none)",
                ["content_length"] = content.Length
            };

            try
            {
                var relativePath = Path.GetRelativePath(projectRoot, fullPath);
                var chunks = _documentationService.ChunkDocument(newContent, relativePath);

                if (chunks.Count > 0)
                {
                    await _documentationService.EmbedChunksAsync(chunks);

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

                    // Удаляем старые точки для этого файла и вставляем новые
                    // (упрощённо — вставляем с тем же ID для перезаписи)
                    await _qdrantService.UpsertPointsAsync(
                        _qdrantOptions.Value.CollectionName, points);

                    metadata["reindexed_chunks"] = points.Count;
                    _logger.LogInformation(
                        "Re-indexed {Count} chunks for file {Path}", points.Count, fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to re-index file {Path} after update", fullPath);
                metadata["reindex_error"] = ex.Message;
            }

            return ToolResult.Ok(
                isNewFile
                    ? $"Created new documentation file '{filePath}' with {(string.IsNullOrEmpty(section) ? "content" : $"section '{section}'")}"
                    : $"Updated section '{section ?? "(end of file)"}' in '{filePath}'",
                metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update documentation file {Path}", filePath);
            return ToolResult.Fail($"Failed to update documentation: {ex.Message}");
        }
    }

    /// <summary>
    /// Заменяет существующую секцию ## sectionName или добавляет новую в конец файла.
    /// </summary>
    private static string ReplaceOrAddSection(string content, string sectionName, string newSectionContent)
    {
        // Ищем секцию с таким же заголовком
        var pattern = $@"(^##\s+{Regex.Escape(sectionName)}\s*$.*?)(?=^##\s|\z)";
        var match = Regex.Match(content, pattern, RegexOptions.Multiline | RegexOptions.Singleline);

        if (match.Success)
        {
            // Заменяем существующую секцию
            var replacement = $"## {sectionName}\n\n{newSectionContent}";
            return content.Replace(match.Value, replacement);
        }
        else
        {
            // Добавляем новую секцию в конец
            var newSection = $"\n\n## {sectionName}\n\n{newSectionContent}";
            return content.TrimEnd() + newSection;
        }
    }

    private static string GetFullPath(string projectRoot, string path)
    {
        if (Path.IsPathRooted(path))
            return path;
        return Path.GetFullPath(Path.Combine(projectRoot, path));
    }
}