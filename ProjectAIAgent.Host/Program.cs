using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectAIAgent.Core.Agents;
using ProjectAIAgent.Core.Services;
using ProjectAIAgent.Core.Tools;
using ProjectAIAgent.Host;

var builder = Host.CreateApplicationBuilder(args);

// ==========================================
// 1. КОНФИГУРАЦИЯ
// ==========================================
builder.Services.Configure<OllamaOptions>(
    builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<AgentOptions>(
    builder.Configuration.GetSection("Agent"));
builder.Services.Configure<QdrantOptions>(
    builder.Configuration.GetSection("Qdrant"));

// ==========================================
// 2. HTTP-КЛИЕНТ ДЛЯ OLLAMA (прямые вызовы /api/generate)
// ==========================================
builder.Services.AddHttpClient<ProjectAIAgent.Core.Services.IOllamaApiClient, OllamaApiClientWrapper>(client =>
{
    var ollamaSection = builder.Configuration.GetSection("Ollama");
    var baseUrl = ollamaSection["BaseUrl"] ?? "http://localhost:11434";
    var timeoutSeconds = int.TryParse(ollamaSection["TimeoutSeconds"], out var ts) ? ts : 120;

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});

// ==========================================
// 3. LLM-СЕРВИС
// ==========================================
builder.Services.AddSingleton<ILlmService, LlmService>();

// ==========================================
// 4. СЕРВИСЫ ДОКУМЕНТАЦИИ И ЭМБЕДДИНГОВ
// ==========================================
builder.Services.AddSingleton<IDocumentationService, DocumentationService>();
builder.Services.AddHttpClient<IOllamaEmbeddingClient, OllamaEmbeddingClient>(client =>
{
    var ollamaSection = builder.Configuration.GetSection("Ollama");
    var baseUrl = ollamaSection["BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(60); // Эмбеддинги могут выполняться дольше
});

// ==========================================
// 5. СЕРВИСЫ QDRANT
// ==========================================
builder.Services.AddHttpClient<IQdrantService, QdrantService>(client =>
{
    var qdrantSection = builder.Configuration.GetSection("Qdrant");
    var endpoint = qdrantSection["Endpoint"] ?? "http://localhost:6333";
    client.BaseAddress = new Uri(endpoint);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ==========================================
// 6. СЕРВИСЫ ТЕСТИРОВАНИЯ ПРОМПТОВ
// ==========================================
builder.Services.AddSingleton<PromptLoader>();
builder.Services.AddSingleton<PromptTesterService>();

// ==========================================
// 7. ИНСТРУМЕНТЫ (автоматическая регистрация)
// ==========================================
builder.Services.AddAgentTools(typeof(BaseAgent).Assembly);

// ==========================================
// 8. АГЕНТЫ
// ==========================================
builder.Services.AddSingleton<OrchestratorAgent>();
builder.Services.AddSingleton<CodeEditorAgent>();
builder.Services.AddSingleton<DocumentationAgent>();
builder.Services.AddSingleton<ContextAgent>();

// ==========================================
// 9. ФОНОВЫЕ СЛУЖБЫ
// ==========================================
// Индексация документации при старте
builder.Services.AddHostedService<DocumentationIndexerService>();
// Отслеживание изменений в реальном времени
builder.Services.AddHostedService<DocumentationWatcherService>();
// Интерактивный цикл (режим Агента / Тестирования)
builder.Services.AddHostedService<AgentWorkerService>();

// ==========================================
// 10. ЗАПУСК
// ==========================================
var host = builder.Build();
await host.RunAsync();