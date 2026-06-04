// ProjectAIAgent.Core/Services/LlmService.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp.Models;
using Polly;
using Polly.Retry;

namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Реализация ILlmService, взаимодействующая с Ollama через IOllamaApiClient.
/// Включает политику ретраев (повторных попыток) через Polly.
/// </summary>
public class LlmService : ILlmService
{
    private readonly IOllamaApiClient _ollamaClient;
    private readonly IOptions<OllamaOptions> _options;
    private readonly ILogger<LlmService> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public LlmService(
        IOllamaApiClient ollamaClient,
        IOptions<OllamaOptions> options,
        ILogger<LlmService> logger)
    {
        _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Строим пайплайн устойчивости: ретраи + таймаут
        var opts = _options.Value;
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                // Повторяем только при ошибках HTTP (5xx, таймауты) — не при 404
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(),
                MaxRetryAttempts = opts.MaxRetries,
                Delay = TimeSpan.FromSeconds(opts.RetryDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "🔄 Повторная попытка {Attempt}/{MaxAttempts} вызова Ollama. " +
                        "Ошибка: {Error}. Задержка: {Delay}с",
                        args.AttemptNumber,
                        opts.MaxRetries,
                        args.Outcome.Exception?.GetType().Name ?? "неизвестно",
                        args.RetryDelay.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(opts.TotalTimeoutSeconds))
            .Build();
    }

    /// <inheritdoc />
    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;

        // Формируем промпт в формате, который ожидает qwen2.5-coder
        var fullPrompt = $"<|system|>\n{systemPrompt}\n<|end|>\n" +
                         $"<|user|>\n{userMessage}\n<|end|>\n" +
                         $"<|assistant|>\n";

        var request = new GenerateRequest
        {
            Model = options.Model,
            Prompt = fullPrompt,
            Stream = false,
            Options = new RequestOptions
            {
                Temperature = (float)options.Temperature,
                TopP = (float)options.TopP,
                NumPredict = options.MaxTokens
            }
        };

        _logger.LogDebug(
            "Отправка запроса к Ollama. Model={Model}, Temperature={Temp}, MaxTokens={MaxTokens}",
            options.Model, options.Temperature, options.MaxTokens);

        try
        {
            // Выполняем запрос через Polly-пайплайн с ретраями и таймаутом
            var response = await _resiliencePipeline.ExecuteAsync(
                async ct => await _ollamaClient.GenerateAsync(request, ct).ConfigureAwait(false),
                cancellationToken);

            _logger.LogDebug(
                "Получен ответ от Ollama длиной {Length} символов",
                response?.Length ?? 0);

            return response ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Запрос к Ollama был отменён");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при вызове Ollama API (все ретраи исчерпаны)");
            throw new InvalidOperationException(
                $"Не удалось получить ответ от LLM после {options.MaxRetries} попыток: {ex.Message}", ex);
        }
    }
}