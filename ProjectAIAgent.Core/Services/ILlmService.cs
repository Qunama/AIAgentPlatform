// ProjectAIAgent.Core/Services/ILlmService.cs
namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Основной сервис для взаимодействия с LLM.
/// Инкапсулирует формирование промптов, вызов модели и парсинг ответов.
/// Используется всеми агентами для генерации текста и кода.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Отправляет системный промпт и сообщение пользователя в LLM и возвращает ответ.
    /// </summary>
    /// <param name="systemPrompt">Системный промпт, задающий роль и поведение модели</param>
    /// <param name="userMessage">Сообщение пользователя / запрос</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Полный текст ответа модели</returns>
    Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default);
}