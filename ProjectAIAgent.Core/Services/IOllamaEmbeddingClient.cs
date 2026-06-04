// ProjectAIAgent.Core/Services/IOllamaEmbeddingClient.cs
namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Клиент для получения эмбеддингов через Ollama API (/api/embeddings).
/// </summary>
public interface IOllamaEmbeddingClient
{
    /// <summary>
    /// Получает векторное представление текста.
    /// </summary>
    /// <param name="text">Текст для векторизации</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Массив float — векторное представление текста</returns>
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получает векторные представления для нескольких текстов.
    /// </summary>
    Task<List<float[]>> GetEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default);
}