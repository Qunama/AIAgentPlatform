// ProjectAIAgent.Core/Tools/SearchCodebaseTool.cs
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Core.Tools;

[AgentTool("search_codebase", "Searches the project source code using semantic search. " +
    "Finds relevant classes, methods, and code snippets based on a natural language query.")]
public class SearchCodebaseTool : IAgentTool
{
    private readonly IOllamaEmbeddingClient _embeddingClient;
    private readonly IQdrantService _qdrantService;
    private readonly IOptions<QdrantOptions> _qdrantOptions;
    private readonly ILogger<SearchCodebaseTool> _logger;

    public string ToolName => "search_codebase";
    public string Description => "Semantically searches the project source code. " +
        "Finds classes, methods, and code snippets matching the query. " +
        "Use this to understand where functionality is implemented before making changes.";

    public string ParametersSchema => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            query = new
            {
                type = "string",
                description = "What to search for (e.g., 'UserService.CreateUser method', 'email validation', 'logging configuration')"
            },
            top_k = new
            {
                type = "integer",
                description = "Number of results (default: 5, max: 10)"
            }
        },
        required = new[] { "query" }
    });

    public SearchCodebaseTool(
        IOllamaEmbeddingClient embeddingClient,
        IQdrantService qdrantService,
        IOptions<QdrantOptions> qdrantOptions,
        ILogger<SearchCodebaseTool> logger)
    {
        _embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _qdrantOptions = qdrantOptions ?? throw new ArgumentNullException(nameof(qdrantOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("query", out var queryObj) || queryObj is not string query)
            return ToolResult.Fail("Parameter 'query' is required");

        if (string.IsNullOrWhiteSpace(query))
            return ToolResult.Fail("Parameter 'query' cannot be empty");

        var topK = 5;
        if (parameters.TryGetValue("top_k", out var tk) && tk is int k)
            topK = Math.Clamp(k, 1, 10);

        try
        {
            _logger.LogInformation("Searching codebase: '{Query}'", query);

            var queryVector = await _embeddingClient.GetEmbeddingAsync(query);
            var collectionName = _qdrantOptions.Value.CollectionName + "_code";
            var results = await _qdrantService.SearchAsync(collectionName, queryVector, topK);

            if (results.Count == 0)
                return ToolResult.Ok("No matching code found.", new Dictionary<string, object> { ["query"] = query, ["count"] = 0 });

            var sb = new StringBuilder();
            sb.AppendLine($"Code search results for: '{query}'");
            sb.AppendLine("==============================");

            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                sb.AppendLine($"--- Result {i + 1} (score: {r.Score:F3}) ---");
                if (r.Payload != null)
                {
                    if (r.Payload.TryGetValue("file_path", out var fp)) sb.AppendLine($"File: {fp}");
                    if (r.Payload.TryGetValue("namespace", out var ns) && !string.IsNullOrEmpty(ns?.ToString()))
                        sb.AppendLine($"Namespace: {ns}");
                    if (r.Payload.TryGetValue("class_name", out var cn)) sb.AppendLine($"Class: {cn}");
                    if (r.Payload.TryGetValue("method_name", out var mn) && !string.IsNullOrEmpty(mn?.ToString()))
                        sb.AppendLine($"Method: {mn}");
                    if (r.Payload.TryGetValue("signature", out var sig) && !string.IsNullOrEmpty(sig?.ToString()))
                        sb.AppendLine($"Signature: {sig}");
                    if (r.Payload.TryGetValue("content", out var content))
                    {
                        var c = content?.ToString() ?? "";
                        if (c.Length > 500) c = c[..500] + "...";
                        sb.AppendLine(c);
                    }
                }
                sb.AppendLine();
            }

            return ToolResult.Ok(sb.ToString(), new Dictionary<string, object>
            {
                ["query"] = query, ["results_count"] = results.Count, ["top_score"] = results[0].Score
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code search failed");
            return ToolResult.Fail($"Search failed: {ex.Message}");
        }
    }
}