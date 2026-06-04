// ProjectAIAgent.Core/Tools/ReadDocumentationTool.cs
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Core.Tools;

[AgentTool("read_documentation", "Searches the project documentation using semantic search. " +
    "Takes a natural language query and returns the most relevant documentation sections.")]
public class ReadDocumentationTool : IAgentTool
{
    private readonly IOllamaEmbeddingClient _embeddingClient;
    private readonly IQdrantService _qdrantService;
    private readonly IOptions<QdrantOptions> _qdrantOptions;
    private readonly ILogger<ReadDocumentationTool> _logger;

    public string ToolName => "read_documentation";
    public string Description => "Semantically searches the project documentation. " +
        "Converts the query to a vector and finds the most relevant documentation sections in Qdrant. " +
        "Returns the content of matching sections with file paths and relevance scores.";

    public string ParametersSchema => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            query = new
            {
                type = "string",
                description = "Natural language search query for the documentation"
            },
            top_k = new
            {
                type = "integer",
                description = "Number of results to return (default: 5, max: 10)"
            }
        },
        required = new[] { "query" }
    });

    public ReadDocumentationTool(
        IOllamaEmbeddingClient embeddingClient,
        IQdrantService qdrantService,
        IOptions<QdrantOptions> qdrantOptions,
        ILogger<ReadDocumentationTool> logger)
    {
        _embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _qdrantOptions = qdrantOptions ?? throw new ArgumentNullException(nameof(qdrantOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("query", out var queryObj) || queryObj is not string query)
        {
            return ToolResult.Fail("Parameter 'query' is required and must be a string");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResult.Fail("Parameter 'query' cannot be empty");
        }

        var topK = 5;
        if (parameters.TryGetValue("top_k", out var topKObj))
        {
            topK = topKObj switch
            {
                int i => Math.Clamp(i, 1, 10),
                string s when int.TryParse(s, out var parsed) => Math.Clamp(parsed, 1, 10),
                _ => 5
            };
        }

        try
        {
            _logger.LogInformation("Searching documentation for: '{Query}' (top_k={TopK})", query, topK);

            // 1. Векторизуем запрос
            var queryVector = await _embeddingClient.GetEmbeddingAsync(query);

            // 2. Ищем в Qdrant
            var collectionName = _qdrantOptions.Value.CollectionName;
            var results = await _qdrantService.SearchAsync(collectionName, queryVector, topK);

            if (results.Count == 0)
            {
                return ToolResult.Ok(
                    "No matching documentation found for your query.",
                    new Dictionary<string, object>
                    {
                        ["query"] = query,
                        ["results_count"] = 0
                    });
            }

            // 3. Форматируем результаты
            var output = FormatResults(query, results);

            _logger.LogInformation(
                "Documentation search returned {Count} results for query '{Query}'",
                results.Count, query);

            return ToolResult.Ok(output, new Dictionary<string, object>
            {
                ["query"] = query,
                ["results_count"] = results.Count,
                ["top_score"] = results[0].Score
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search documentation for query '{Query}'", query);
            return ToolResult.Fail($"Failed to search documentation: {ex.Message}");
        }
    }

    private static string FormatResults(string query, List<QdrantSearchResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Documentation search results for: '{query}'");
        sb.AppendLine("==============================================");

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var relevance = result.Score >= 0.9 ? "★★★★★" :
                            result.Score >= 0.8 ? "★★★★☆" :
                            result.Score >= 0.7 ? "★★★☆☆" :
                            result.Score >= 0.6 ? "★★☆☆☆" : "★☆☆☆☆";

            sb.AppendLine($"--- Result {i + 1} (score: {result.Score:F3}) {relevance} ---");

            if (result.Payload != null)
            {
                if (result.Payload.TryGetValue("file_path", out var filePath))
                    sb.AppendLine($"File: {filePath}");

                if (result.Payload.TryGetValue("section", out var section) && !string.IsNullOrEmpty(section?.ToString()))
                    sb.AppendLine($"Section: {section}");

                if (result.Payload.TryGetValue("content", out var content))
                {
                    var contentStr = content?.ToString() ?? string.Empty;
                    // Обрезаем слишком длинный контент
                    if (contentStr.Length > 1000)
                        contentStr = contentStr[..1000] + "...";
                    sb.AppendLine(contentStr);
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}