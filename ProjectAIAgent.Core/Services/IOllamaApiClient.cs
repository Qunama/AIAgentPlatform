// ProjectAIAgent.Core/Services/IOllamaApiClient.cs
using OllamaSharp.Models;

namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Тонкая абстракция над Ollama API для генерации текста.
/// Позволяет подменять реальный клиент на mock в тестах.
/// </summary>
public interface IOllamaApiClient
{
    /// <summary>
    /// Отправляет запрос на генерацию текста и возвращает ответ модели.
    /// </summary>
    /// <param name="request">Запрос с моделью, промптом и параметрами генерации</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Ответ модели (объединённый из потока, если стриминг включён)</returns>
    Task<string> GenerateAsync(GenerateRequest request, CancellationToken cancellationToken = default);
}