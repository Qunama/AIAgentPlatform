// ProjectAIAgent.Core/Services/LlmService.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp.Models;
using Polly;
using Polly.Retry;

namespace ProjectAIAgent.Core.Services;

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
        _ollamaClient = ollamaClient;
        _options = options;
        _logger = logger;

        var opts = _options.Value;
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(),
                MaxRetryAttempts = opts.MaxRetries,
                Delay = TimeSpan.FromSeconds(opts.RetryDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retry {Attempt}/{MaxAttempts} calling Ollama. Error: {Error}",
                        args.AttemptNumber, opts.MaxRetries,
                        args.Outcome.Exception?.GetType().Name ?? "unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(TimeSpan.FromSeconds(opts.TotalTimeoutSeconds))
            .Build();
    }

    public Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        return GenerateAsync(systemPrompt, userMessage, _options.Value.Model, cancellationToken);
    }

    public async Task<string> GenerateAsync(
        string systemPrompt,
        string userMessage,
        string model,
        CancellationToken cancellationToken = default)
    {
        // Если передан ключ из ModelByTask — используем соответствующую модель
        var resolvedModel = _options.Value.ModelByTask.TryGetValue(model, out var mapped)
            ? mapped
            : model;

        var fullPrompt = $"<|system|>\n{systemPrompt}\n<|end|>\n" +
                         $"<|user|>\n{userMessage}\n<|end|>\n" +
                         $"<|assistant|>\n";

        var request = new GenerateRequest
        {
            Model = resolvedModel,
            Prompt = fullPrompt,
            Stream = false,
            Options = new RequestOptions
            {
                Temperature = (float)_options.Value.Temperature,
                TopP = (float)_options.Value.TopP,
                NumPredict = _options.Value.MaxTokens
            }
        };

        _logger.LogDebug("Calling Ollama. Model={Model}, Temp={Temp}, MaxTokens={MaxTokens}",
            resolvedModel, _options.Value.Temperature, _options.Value.MaxTokens);

        try
        {
            var response = await _resiliencePipeline.ExecuteAsync(
                async ct => await _ollamaClient.GenerateAsync(request, ct).ConfigureAwait(false),
                cancellationToken);

            _logger.LogDebug("Ollama response: {Length} chars", response?.Length ?? 0);
            return response ?? string.Empty;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Ollama request cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama API error (all retries exhausted)");
            throw new InvalidOperationException(
                $"Failed to get LLM response after {_options.Value.MaxRetries} attempts: {ex.Message}", ex);
        }
    }
}