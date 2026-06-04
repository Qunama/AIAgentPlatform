// ProjectAIAgent.Host/OllamaApiClientWrapper.cs
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OllamaSharp.Models;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Host;

/// <summary>
/// Реализация IOllamaApiClient через прямые HTTP-вызовы к Ollama API /api/generate.
/// </summary>
public class OllamaApiClientWrapper : ProjectAIAgent.Core.Services.IOllamaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaApiClientWrapper> _logger;

    public OllamaApiClientWrapper(HttpClient httpClient, ILogger<OllamaApiClientWrapper> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(
        GenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        // Диагностика: выводим URL, на который стучимся
        var fullUrl = $"{_httpClient.BaseAddress}api/generate";
        _logger.LogInformation("📡 Отправка запроса к Ollama: {Url}", fullUrl);
        _logger.LogInformation("📋 Модель: {Model}, длина промпта: {Length} символов", 
            request.Model, request.Prompt?.Length ?? 0);

        // Формируем тело запроса
        object finalRequestBody;
        if (request.Options != null)
        {
            finalRequestBody = new
            {
                model = request.Model,
                prompt = request.Prompt,
                stream = false,
                options = new Dictionary<string, object>
                {
                    ["temperature"] = request.Options.Temperature,
                    ["top_p"] = request.Options.TopP,
                    ["num_predict"] = request.Options.NumPredict
                }
            };
        }
        else
        {
            finalRequestBody = new
            {
                model = request.Model,
                prompt = request.Prompt,
                stream = false
            };
        }

        var json = JsonSerializer.Serialize(finalRequestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Вызов API
        var response = await _httpClient.PostAsync("/api/generate", content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "❌ Ollama вернул {StatusCode} {ReasonPhrase}. Тело ответа: {ErrorBody}",
                (int)response.StatusCode, response.ReasonPhrase, errorBody);
        }
        
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogDebug("📥 Получен ответ от Ollama длиной {Length} символов", responseJson.Length);

        using var doc = JsonDocument.Parse(responseJson);
        var generatedText = doc.RootElement.GetProperty("response").GetString();

        return generatedText ?? string.Empty;
    }
}