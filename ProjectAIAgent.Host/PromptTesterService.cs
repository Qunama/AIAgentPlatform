// ProjectAIAgent.Host/PromptTesterService.cs
using Microsoft.Extensions.Logging;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Host;

/// <summary>
/// Сервис для ручного тестирования промптов.
/// Позволяет напрямую отправлять запросы к LLM и видеть сырые ответы,
/// минуя оркестратор и агентов. Полезно для отладки и улучшения промптов.
/// </summary>
public class PromptTesterService
{
    private readonly ILlmService _llmService;
    private readonly PromptLoader _promptLoader;
    private readonly ILogger<PromptTesterService> _logger;

    public PromptTesterService(
        ILlmService llmService,
        PromptLoader promptLoader,
        ILogger<PromptTesterService> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _promptLoader = promptLoader ?? throw new ArgumentNullException(nameof(promptLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Запускает интерактивный цикл тестирования промптов.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine("  Режим тестирования промптов");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        // Выбираем роль (промпт) для тестирования
        var role = await SelectRoleAsync(cancellationToken);
        if (role == null) return;

        // Загружаем системный промпт
        var systemPrompt = await _promptLoader.LoadPromptAsync(role.Value.fileName);
        
        Console.WriteLine();
        Console.WriteLine("📄 Загружен системный промпт:");
        Console.WriteLine("----------------------------------------------");
        Console.WriteLine(systemPrompt);
        Console.WriteLine("----------------------------------------------");
        Console.WriteLine($"   Длина: {systemPrompt.Length} символов");
        Console.WriteLine();
        Console.WriteLine("Введите запрос к модели (или 'exit' для выхода)");
        Console.WriteLine("Специальные команды:");
        Console.WriteLine("  /system  — показать системный промпт");
        Console.WriteLine("  /stats   — статистика последнего ответа");
        Console.WriteLine("  /role    — сменить роль");
        Console.WriteLine();

        string? lastResponse = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("📝 User > ");
            var input = await Task.Run(() => Console.ReadLine(), cancellationToken);

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Trim().ToLowerInvariant() is "exit" or "quit" or "выход")
                break;

            // Специальные команды
            if (input == "/system")
            {
                Console.WriteLine("📄 Системный промпт:");
                Console.WriteLine(systemPrompt);
                continue;
            }

            if (input == "/stats" && lastResponse != null)
            {
                ShowStats(lastResponse, systemPrompt);
                continue;
            }

            if (input == "/role")
            {
                var newRole = await SelectRoleAsync(cancellationToken);
                if (newRole != null)
                {
                    systemPrompt = await _promptLoader.LoadPromptAsync(newRole.Value.fileName);
                    Console.WriteLine($"✅ Роль изменена на '{newRole.Value.displayName}'");
                }
                continue;
            }

            try
            {
                Console.WriteLine("⏳ Ожидание ответа от LLM...");
                Console.WriteLine();

                lastResponse = await _llmService.GenerateAsync(
                    systemPrompt, input, cancellationToken);

                Console.WriteLine("🤖 Ответ модели:");
                Console.WriteLine("----------------------------------------------");
                Console.WriteLine(lastResponse);
                Console.WriteLine("----------------------------------------------");
                Console.WriteLine($"   Длина ответа: {lastResponse?.Length ?? 0} символов");

                // Проверяем наличие кода в ответе
                if (LlmResponseParser.ContainsCode(lastResponse ?? string.Empty))
                {
                    var code = LlmResponseParser.ExtractCodeBlock(lastResponse!);
                    Console.WriteLine($"   📦 Извлечён код: {code.Length} символов");
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при тестовом запросе");
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
                Console.WriteLine();
            }
        }
    }

    private async Task<(string fileName, string displayName)?> SelectRoleAsync(
        CancellationToken cancellationToken)
    {
        Console.WriteLine("Выберите роль для тестирования:");
        Console.WriteLine("  1: OrchestratorAgent (orchestrator.txt)");
        Console.WriteLine("  2: CodeEditorAgent (code-editor.txt)");
        Console.WriteLine("  3: DocumentationAgent (documentation.txt)");
        Console.WriteLine("  4: Без системного промпта (только user-сообщение)");
        Console.Write("> ");

        var choice = await Task.Run(() => Console.ReadLine(), cancellationToken);

        return choice?.Trim() switch
        {
            "1" => ("orchestrator.txt", "OrchestratorAgent"),
            "2" => ("code-editor.txt", "CodeEditorAgent"),
            "3" => ("documentation.txt", "DocumentationAgent"),
            "4" => ("__empty__", "Без промпта"),
            _ => ("orchestrator.txt", "OrchestratorAgent") // По умолчанию
        };
    }

    private void ShowStats(string lastResponse, string systemPrompt)
    {
        Console.WriteLine();
        Console.WriteLine("📊 Статистика последнего ответа:");
        Console.WriteLine($"   Длина ответа:     {lastResponse.Length} символов");
        Console.WriteLine($"   Длина промпта:    {systemPrompt.Length} символов");
        Console.WriteLine($"   Количество строк: {lastResponse.Count(c => c == '\n') + 1}");
        Console.WriteLine($"   Содержит код:     {LlmResponseParser.ContainsCode(lastResponse)}");

        if (LlmResponseParser.ContainsCode(lastResponse))
        {
            var codeBlocks = LlmResponseParser.ExtractAllCodeBlocks(lastResponse);
            Console.WriteLine($"   Блоков кода:      {codeBlocks.Count}");
            for (int i = 0; i < codeBlocks.Count; i++)
            {
                Console.WriteLine($"     Блок {i + 1}: {codeBlocks[i].Length} символов");
            }
        }

        Console.WriteLine();
    }
}