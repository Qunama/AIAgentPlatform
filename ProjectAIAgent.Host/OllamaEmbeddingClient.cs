// ProjectAIAgent.Host/OllamaEmbeddingClient.cs
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Host;

/// <summary>
/// Реализация IOllamaEmbeddingClient через прямые HTTP-вызовы к Ollama API /api/embeddings.
/// </summary>
public class OllamaEmbeddingClient : IOllamaEmbeddingClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<OllamaOptions> _ollamaOptions;
    private readonly ILogger<OllamaEmbeddingClient> _logger;

    public OllamaEmbeddingClient(
        HttpClient httpClient,
        IOptions<OllamaOptions> ollamaOptions,
        ILogger<OllamaEmbeddingClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ollamaOptions = ollamaOptions ?? throw new ArgumentNullException(nameof(ollamaOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await GetEmbeddingsAsync(new List<string> { text }, cancellationToken);
        return results[0];
    }

    /// <inheritdoc />
    public async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        var results = new List<float[]>();

        foreach (var text in texts)
        {
            var requestBody = new
            {
                model = _ollamaOptions.Value.Model,
                prompt = text
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/embeddings", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(responseJson);
            var embeddingArray = doc.RootElement.GetProperty("embedding");

            var embedding = new float[embeddingArray.GetArrayLength()];
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = embeddingArray[i].GetSingle();
            }

            results.Add(embedding);
        }

        return results;
    }
}