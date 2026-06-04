// ProjectAIAgent.Host/QdrantService.cs
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Host;

/// <summary>
/// Реализация IQdrantService через прямые HTTP-вызовы к Qdrant REST API.
/// </summary>
public class QdrantService : IQdrantService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<QdrantService> _logger;

    public QdrantService(
        HttpClient httpClient,
        ILogger<QdrantService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task EnsureCollectionExistsAsync(
        string collectionName, int vectorSize, CancellationToken cancellationToken = default)
    {
        // Проверяем, существует ли коллекция
        var checkResponse = await _httpClient.GetAsync(
            $"/collections/{collectionName}", cancellationToken);

        if (checkResponse.IsSuccessStatusCode)
        {
            _logger.LogInformation("Collection '{Collection}' already exists", collectionName);
            return;
        }

        // Создаём коллекцию
        var createBody = new
        {
            vectors = new
            {
                size = vectorSize,
                distance = "Cosine"
            }
        };

        var json = JsonSerializer.Serialize(createBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var createResponse = await _httpClient.PutAsync(
            $"/collections/{collectionName}", content, cancellationToken);

        createResponse.EnsureSuccessStatusCode();

        _logger.LogInformation(
            "Collection '{Collection}' created with vector size {Size}",
            collectionName, vectorSize);
    }

    /// <inheritdoc />
    public async Task UpsertPointsAsync(
        string collectionName, List<QdrantPoint> points, CancellationToken cancellationToken = default)
    {
        var pointsPayload = new
        {
            points = points.Select(p => new
            {
                id = p.Id,
                vector = p.Vector,
                payload = p.Payload
            })
        };

        var json = JsonSerializer.Serialize(pointsPayload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync(
            $"/collections/{collectionName}/points?wait=true", content, cancellationToken);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation(
            "Upserted {Count} points to collection '{Collection}'",
            points.Count, collectionName);
    }

    /// <inheritdoc />
    public async Task<List<QdrantSearchResult>> SearchAsync(
        string collectionName,
        float[] queryVector,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var searchBody = new
        {
            vector = queryVector,
            limit = topK,
            with_payload = true
        };

        var json = JsonSerializer.Serialize(searchBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"/collections/{collectionName}/points/search", content, cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);

        var results = new List<QdrantSearchResult>();
        var resultArray = doc.RootElement.GetProperty("result");

        foreach (var item in resultArray.EnumerateArray())
        {
            var result = new QdrantSearchResult
            {
                Id = item.GetProperty("id").GetString() ?? string.Empty,
                Score = item.GetProperty("score").GetSingle(),
                Payload = ParsePayload(item.GetProperty("payload"))
            };
            results.Add(result);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task ClearCollectionAsync(
        string collectionName, CancellationToken cancellationToken = default)
    {
        var deleteBody = new
        {
            filter = new { }
        };

        var json = JsonSerializer.Serialize(deleteBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"/collections/{collectionName}/points/delete?wait=true", content, cancellationToken);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Collection '{Collection}' cleared", collectionName);
    }

    private static Dictionary<string, object> ParsePayload(JsonElement payloadElement)
    {
        var payload = new Dictionary<string, object>();

        foreach (var prop in payloadElement.EnumerateObject())
        {
            payload[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => prop.Value.TryGetInt32(out var i) ? i : prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => prop.Value.GetRawText()
            };
        }

        return payload;
    }
}