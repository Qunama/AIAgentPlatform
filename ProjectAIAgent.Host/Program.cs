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
// 3. НАШИ АБСТРАКЦИИ
// ==========================================
builder.Services.AddSingleton<ILlmService, LlmService>();

// ==========================================
// 3.5. СЕРВИСЫ ДОКУМЕНТАЦИИ И ЭМБЕДДИНГОВ
// ==========================================
builder.Services.Configure<QdrantOptions>(
    builder.Configuration.GetSection("Qdrant"));
builder.Services.AddSingleton<IDocumentationService, DocumentationService>();
builder.Services.AddHttpClient<IOllamaEmbeddingClient, OllamaEmbeddingClient>(client =>
{
    var ollamaSection = builder.Configuration.GetSection("Ollama");
    var baseUrl = ollamaSection["BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(60); // Эмбеддинги могут выполняться дольше
});

// ==========================================
// 3.6. СЕРВИСЫ QDRANT
// ==========================================
builder.Services.AddHttpClient<IQdrantService, QdrantService>(client =>
{
    var qdrantSection = builder.Configuration.GetSection("Qdrant");
    var endpoint = qdrantSection["Endpoint"] ?? "http://localhost:6333";
    client.BaseAddress = new Uri(endpoint);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ==========================================
// 3.7. ИНДЕКСАЦИЯ ДОКУМЕНТАЦИИ ПРИ СТАРТЕ
// ==========================================
builder.Services.AddHostedService<DocumentationIndexerService>();

// ==========================================
// 3.5. СЕРВИСЫ ТЕСТИРОВАНИЯ ПРОМПТОВ
// ==========================================
builder.Services.AddSingleton<PromptLoader>();
builder.Services.AddSingleton<PromptTesterService>();

// ==========================================
// 4. ИНСТРУМЕНТЫ (автоматическая регистрация)
// ==========================================
builder.Services.AddAgentTools(typeof(BaseAgent).Assembly);

// ==========================================
// 5. АГЕНТЫ
// ==========================================
builder.Services.AddSingleton<OrchestratorAgent>();
builder.Services.AddSingleton<CodeEditorAgent>();
builder.Services.AddSingleton<DocumentationAgent>();
builder.Services.AddSingleton<ContextAgent>();

// ==========================================
// 6. ФОНОВАЯ СЛУЖБА (интерактивный цикл)
// ==========================================
builder.Services.AddHostedService<AgentWorkerService>();

// ==========================================
// 7. ЗАПУСК
// ==========================================
var host = builder.Build();
await host.RunAsync();