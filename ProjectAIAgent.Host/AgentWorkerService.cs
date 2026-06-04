// ProjectAIAgent.Host/AgentWorkerService.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectAIAgent.Core.Agents;
using ProjectAIAgent.Core.Tools;

namespace ProjectAIAgent.Host;

/// <summary>
/// Фоновая служба, реализующая интерактивный цикл обработки запросов пользователя.
/// Поддерживает два режима: агент (полный цикл) и тестирование промптов (прямой LLM).
/// </summary>
public class AgentWorkerService : BackgroundService
{
    private readonly OrchestratorAgent _orchestrator;
    private readonly ToolRegistry _toolRegistry;
    private readonly PromptTesterService _promptTester;
    private readonly ILogger<AgentWorkerService> _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    public AgentWorkerService(
        OrchestratorAgent orchestrator,
        ToolRegistry toolRegistry,
        PromptTesterService promptTester,
        ILogger<AgentWorkerService> logger,
        IHostApplicationLifetime appLifetime)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _promptTester = promptTester ?? throw new ArgumentNullException(nameof(promptTester));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 AI Agent Platform запущен");

        // Выводим список зарегистрированных инструментов
        var tools = _toolRegistry.GetAllTools();
        _logger.LogInformation("📦 Зарегистрировано {Count} инструментов:", tools.Count);
        foreach (var tool in tools)
        {
            _logger.LogInformation("   • {Name}: {Description}", tool.Key, tool.Value.Description);
        }

        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine("  AI Agent Platform");
        Console.WriteLine("==============================================");
        Console.WriteLine("Выберите режим:");
        Console.WriteLine("  A — режим Агента (полный цикл оркестрации)");
        Console.WriteLine("  T — тестирование промптов (прямой LLM)");
        Console.Write("> ");

        var modeChoice = await Task.Run(() => Console.ReadLine(), stoppingToken);
        var mode = modeChoice?.Trim().ToUpperInvariant();

        if (mode == "T")
        {
            await _promptTester.RunAsync(stoppingToken);
        }
        else
        {
            await RunAgentModeAsync(stoppingToken);
        }

        _appLifetime.StopApplication();
    }

    private async Task RunAgentModeAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine("  Режим Агента");
        Console.WriteLine("  Введите запрос или 'exit' для выхода");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        while (!stoppingToken.IsCancellationRequested)
        {
            Console.Write("📝 Запрос > ");
            var input = await Task.Run(() => Console.ReadLine(), stoppingToken);

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Trim().ToLowerInvariant() is "exit" or "quit" or "выход")
            {
                _logger.LogInformation("👋 Завершение работы по команде пользователя");
                break;
            }

            try
            {
                Console.WriteLine("⏳ Обработка запроса...");
                var response = await _orchestrator.ProcessRequestAsync(input);

                Console.WriteLine();
                Console.WriteLine("🤖 Ответ агента:");
                Console.WriteLine(response);
                Console.WriteLine();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке запроса");
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
                Console.WriteLine();
            }
        }
    }
}