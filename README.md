AIAgentPlatform — AI-агент для работы с проектами (.NET 8)

МИССИЯ ПРОЕКТА
Создать AI-агента на .NET 8, который получает запросы на естественном языке, самостоятельно вносит правки в указанный пользователем .NET проект, автоматически ведёт и читает документацию, понимая общую картину работы.

ТЕКУЩАЯ СТАДИЯ: Этап 1 завершён. Этап 2 завершён. Этап 3 завершён. Этап 4 завершён. Этап 5 завершён. Этап 6 завершён. Этап 7 — следующий.

ЧТО УЖЕ СДЕЛАНО (ЭТАП 1: АРХИТЕКТУРА И ПОДГОТОВКА ОКРУЖЕНИЯ)
- Создано .NET 8 решение с проектами ProjectAIAgent.Host (Web Application) и ProjectAIAgent.Core (Class Library).
- Настроены и проверены все интеграции:
  - Ollama (локальная LLM qwen2.5-coder:7b-instruct) через Docker.
  - Qdrant (векторная БД) через Docker.
- Написан проверочный код в Program.cs, подтверждающий сквозную работу всей цепочки.
- Проверка пройдена успешно: модель отвечает на тестовые запросы.

ЧТО СДЕЛАНО (ЭТАП 2: ПРОЕКТИРОВАНИЕ СИСТЕМЫ АГЕНТОВ)
- Создан базовый класс BaseAgent в ProjectAIAgent.Core.Agents.
- Определён интерфейс IAgentTool с ToolResult, AgentToolAttribute.
- Реализован ToolRegistry с авторегистрацией через рефлексию (AddAgentTools).
- Созданы четыре агента: OrchestratorAgent, CodeEditorAgent, DocumentationAgent, ContextAgent.
- Реализованы базовые инструменты: ReadFileTool, WriteFileTool, ProjectStructureTool.
- Написаны системные промпты (orchestrator.txt, code-editor.txt, documentation.txt).
- Создан AgentWorkerService с двумя режимами (A — Агент, T — Тестирование).

ЧТО СДЕЛАНО (ЭТАП 3: ИНТЕГРАЦИЯ ЛОКАЛЬНОЙ МОДЕЛИ)
- Реализован ILlmService / LlmService с прямыми HTTP-вызовами /api/generate и Polly-ретраями.
- Реализован LlmResponseParser: извлечение кода из Markdown, JSON-планов, ConvertJsonElement.
- Настроены параметры генерации: Temperature 0.2, TopP 0.9, MaxTokens 4096, MaxRetries 3.
- Создан PromptTesterService для ручного тестирования промптов.
- Интегрирована LLM в OrchestratorAgent: цикл "LLM -> JSON -> Инструмент -> Результат -> LLM -> Report".
- Доработаны все три системных промпта по результатам тестирования.

ЧТО СДЕЛАНО (ЭТАП 4: РЕАЛИЗАЦИЯ MEMORY И ДОКУМЕНТАЦИИ)
- Реализован DocumentationService: поиск .md/.txt, разбиение на чанки по ##, векторизация.
- Реализован IOllamaEmbeddingClient / OllamaEmbeddingClient для /api/embeddings.
- Реализован IQdrantService / QdrantService: создание коллекций, вставка/поиск/удаление точек.
- Создан DocumentationIndexerService: полная индексация документации при старте агента.
- Создан DocumentationWatcherService: отслеживание изменений через FileSystemWatcher с дебаунсом.
- Реализован ReadDocumentationTool: семантический поиск по документации через Qdrant.
- Реализован UpdateDocumentationTool: обновление/создание секций с автореиндексацией.
- Реализован ContextAgent с ProjectContext: история взаимодействий, фазы работы, кеш структуры.
- Интегрирован ContextAgent в OrchestratorAgent.

ЧТО СДЕЛАНО (ЭТАП 5: РАЗРАБОТКА ОРКЕСТРАЦИИ)
- Реализован RunShellCommandTool: выполнение консольных команд через CliWrap с проверкой безопасности (белый список: dotnet, git, npm, docker и др.). Поддерживает валидацию через dotnet build и dotnet test.
- Реализован GitDiffTool: операции status (изменённые/добавленные/удалённые файлы), diff (просмотр изменений), log (история коммитов) через LibGit2Sharp.
- Создана модель ChangePlan: план изменений с шагами, статусами (PlanStatus, StepStatus), прогрессом и методом ToPromptString() для системного промпта.
- Доработан OrchestratorAgent для полного цикла оркестрации:
  - Explore (project_structure) -> Read (read_file) -> Write (write_file) -> Validate (dotnet build) -> Document (update_documentation) -> Show (git_diff) -> Report.
  - После каждого write_file автоматически предлагается dotnet build.
  - После каждой сборки анализируется результат: успех -> git_diff -> report; провал -> fix -> retry.
- Реализован BuildValidationService: запуск dotnet build через CliWrap, извлечение ошибок (CS/MSB), управление повторными попытками (до 3). BuildResult содержит Success, ExitCode, ShouldRetry, RemainingAttempts, FormattedErrors.
- Реализована система форматированных отчётов через ReportService:
  - Полный отчёт: запрос, статус, изменённые файлы, результат валидации, ошибки, использованные инструменты, статистика (длительность, вызовы LLM, вызовы инструментов).
  - Краткий отчёт: одна строка с иконкой и количеством изменённых файлов.
- Обновлён orchestrator.txt: workflow из 9 шагов, примеры для run_shell_command, git_diff, read_documentation.
- Всего в системе 7 инструментов, 4 агента, 4 фоновые службы.

ЧТО СДЕЛАНО (ЭТАП 6: CLI И API)
- Разработаны CLI-команды (System.CommandLine + Spectre.Console):
  - agent start — запуск агента в интерактивном режиме.
  - agent set-project <path> — установка пути к проекту.
  - agent request <description> — отправка запроса с форматированным выводом (панели, цвета).
  - agent status — текущий статус (путь, фаза, файлы, ошибки) в виде таблицы.
  - agent history — история взаимодействий в виде таблицы.
  - agent docs query <query> — семантический поиск по документации.
- Реализован Web API (ASP.NET Core):
  - POST /api/agent/set-project — установка пути к проекту.
  - POST /api/agent/request — отправка запроса агенту.
  - GET /api/agent/status — текущий статус агента.
  - GET /api/agent/history?limit=N — история взаимодействий.
- Добавлен Swagger/OpenAPI (Swashbuckle): документация API по /swagger, редирект с / на /Index.
- Реализован SignalR хаб (AgentHub) для real-time обновлений:
  - События: ProgressUpdate (смена фазы), ToolExecuted (выполнение инструмента), BuildCompleted (результат сборки), RequestCompleted (финальный результат), ErrorOccurred (ошибка).
  - SignalRLoggingService для отправки событий из OrchestratorAgent.
  - Хаб доступен по /hubs/agent.
- Создан Web UI (Razor Pages):
  - Страница Index.cshtml: тёмная тема, поле запроса, результат, лог выполнения в реальном времени, история.
  - Интеграция с SignalR: обновление лога и статуса без перезагрузки страницы.
  - Отправка запросов через fetch к /api/agent/request.
  - Статус-бейдж с пульсирующей анимацией при работе.
  - Адаптивная вёрстка для мобильных устройств.
  - Ctrl+Enter для быстрой отправки запроса.
- Проект переведён с Worker SDK на Web SDK для поддержки контроллеров, Razor Pages и SignalR.

ЧТО БУДЕТ СДЕЛАНО (ЭТАП 7: ТЕСТИРОВАНИЕ И УЛУЧШЕНИЕ)
Цель: Обеспечить надёжность, измерить качество работы агента.
Подзадачи:
1. Не выполнено: Юнит-тесты для каждого инструмента, парсинга ответов LLM, валидации изменений
2. Не выполнено: Интеграционные тесты (сквозной тест, тест индексации, тест с разными типами проектов)
3. Не выполнено: Создать тестовый проект-песочницу для безопасных экспериментов
4. Не выполнено: Метрики качества (процент успешных сборок, процент прохождения тестов, время выполнения)
5. Не выполнено: Система сбора обратной связи (оценка изменений, логирование ошибок)
6. Не выполнено: Итеративное улучшение промптов на основе метрик

ПОЛНЫЙ ПЛАН ПРОЕКТА (8 ЭТАПОВ)

ЭТАП 1: АРХИТЕКТУРА И ПОДГОТОВКА ОКРУЖЕНИЯ — ЗАВЕРШЁН
ЭТАП 2: ПРОЕКТИРОВАНИЕ СИСТЕМЫ АГЕНТОВ — ЗАВЕРШЁН
ЭТАП 3: ИНТЕГРАЦИЯ ЛОКАЛЬНОЙ МОДЕЛИ — ЗАВЕРШЁН
ЭТАП 4: РЕАЛИЗАЦИЯ MEMORY И ДОКУМЕНТАЦИИ — ЗАВЕРШЁН
ЭТАП 5: РАЗРАБОТКА ОРКЕСТРАЦИИ — ЗАВЕРШЁН
ЭТАП 6: CLI И API — ЗАВЕРШЁН
ЭТАП 7: ТЕСТИРОВАНИЕ И УЛУЧШЕНИЕ — СЛЕДУЮЩИЙ
ЭТАП 8: ДОКУМЕНТИРОВАНИЕ И ДЕПЛОЙ

СТЕК И ЗАВИСИМОСТИ (ФИНАЛЬНАЯ ВЕРСИЯ ЭТАПА 6)

ИНФРАСТРУКТУРА
- Локальная LLM: Ollama (qwen2.5-coder:7b-instruct) — генерация кода, ответов и эмбеддингов
- Векторная БД: Qdrant (в Docker) — хранение и семантический поиск (коллекция project_docs, 3584, Cosine)
- Контейнеризация: Docker Compose — управление Ollama и Qdrant локально

.NET СТЕК
- Microsoft.AspNetCore.App (встроен в Web SDK) — Kestrel, контроллеры, Razor Pages, SignalR
- Microsoft.Extensions.Hosting — жизненный цикл приложения и фоновые службы
- Microsoft.Extensions.Logging.Console — вывод логов в консоль
- Microsoft.Extensions.Logging.Abstractions — абстракции логирования
- Microsoft.Extensions.DependencyInjection.Abstractions — DI-абстракции
- Microsoft.Extensions.Options — паттерн Options для конфигураций
- Microsoft.Extensions.Http — HttpClientFactory и AddHttpClient
- Polly.Core — политики ретраев и таймаутов
- OllamaSharp — модели и типы (GenerateRequest, RequestOptions)
- CliWrap — запуск внешних команд (dotnet build, dotnet test)
- LibGit2Sharp — работа с Git (status, diff, log)
- Microsoft.SemanticKernel (prerelease) — Memory и работа с эмбеддингами
- Microsoft.SemanticKernel.Plugins.Memory (prerelease) — работа с Memory Store
- Microsoft.SemanticKernel.Connectors.Qdrant (prerelease) — коннектор к Qdrant
- System.CommandLine — парсинг CLI-команд
- Spectre.Console — красивый консольный вывод (таблицы, панели, цвета)
- Swashbuckle.AspNetCore — Swagger/OpenAPI документация
- Microsoft.AspNetCore.SignalR — real-time уведомления
- Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation — Runtime-компиляция Razor Pages

ИСКЛЮЧЁННЫЕ ПАКЕТЫ
- Microsoft.Agents.Hosting — исключён, не существует в NuGet. Заменён собственной оркестрацией.

СТРУКТУРА РЕШЕНИЯ (ТЕКУЩАЯ, ПОСЛЕ ЭТАПА 6)
AIAgentPlatform/
├── AIAgentPlatform.sln
├── local-infra/
│   └── docker-compose.yml          # Ollama + Qdrant
├── ProjectAIAgent.Host/            # Web Application: API, CLI, Web UI, фоновые службы
│   ├── Program.cs                  # DI, конфигурация, middleware (секции 1-11)
│   ├── AgentWorkerService.cs       # Интерактивный цикл (режимы A / T)
│   ├── OllamaApiClientWrapper.cs   # HTTP-клиент /api/generate
│   ├── OllamaEmbeddingClient.cs    # HTTP-клиент /api/embeddings
│   ├── QdrantService.cs           # HTTP-клиент Qdrant REST API
│   ├── DocumentationIndexerService.cs  # Индексация документации при старте
│   ├── DocumentationWatcherService.cs  # Отслеживание изменений .md/.txt
│   ├── PromptTesterService.cs      # Тестирование промптов
│   ├── CliService.cs              # Обработка CLI-команд
│   ├── SignalRLoggingService.cs    # Отправка событий через SignalR
│   ├── Controllers/
│   │   └── AgentController.cs     # Web API: set-project, request, status, history
│   ├── Hubs/
│   │   └── AgentHub.cs            # SignalR хаб
│   ├── Pages/
│   │   ├── Index.cshtml           # Web UI (тёмная тема, лог, история)
│   │   └── Index.cshtml.cs        # Code-behind
│   ├── appsettings.json            # Конфигурация Ollama, Qdrant, Agent, Logging
│   └── ProjectAIAgent.Host.csproj
├── ProjectAIAgent.Core/            # Бизнес-логика, агенты, инструменты, сервисы
│   ├── Agents/
│   │   ├── BaseAgent.cs
│   │   ├── OrchestratorAgent.cs    # Полный цикл оркестрации с SignalR-уведомлениями
│   │   ├── CodeEditorAgent.cs
│   │   ├── DocumentationAgent.cs
│   │   └── ContextAgent.cs
│   ├── Tools/
│   │   ├── IAgentTool.cs           # Интерфейс, ToolResult, AgentToolAttribute
│   │   ├── ToolRegistry.cs         # Реестр с авторегистрацией
│   │   ├── ReadFileTool.cs
│   │   ├── WriteFileTool.cs
│   │   ├── ProjectStructureTool.cs
│   │   ├── ReadDocumentationTool.cs
│   │   ├── UpdateDocumentationTool.cs
│   │   ├── RunShellCommandTool.cs  # Выполнение команд с проверкой безопасности
│   │   └── GitDiffTool.cs         # Git status/diff/log
│   ├── Services/
│   │   ├── ILlmService.cs / LlmService.cs
│   │   ├── LlmResponseParser.cs
│   │   ├── IOllamaApiClient.cs
│   │   ├── IOllamaEmbeddingClient.cs
│   │   ├── OllamaOptions.cs
│   │   ├── AgentOptions.cs
│   │   ├── QdrantOptions.cs
│   │   ├── IDocumentationService.cs / DocumentationService.cs
│   │   ├── IQdrantService.cs
│   │   ├── BuildValidationService.cs  # dotnet build с ретраями
│   │   ├── ReportService.cs           # Форматированные отчёты
│   │   └── PromptLoader.cs
│   ├── Models/
│   │   ├── DocumentChunk.cs
│   │   ├── ProjectContext.cs        # Состояние, история, фазы
│   │   └── ChangePlan.cs            # План изменений с шагами
│   ├── Prompts/
│   │   ├── orchestrator.txt
│   │   ├── code-editor.txt
│   │   └── documentation.txt
│   └── ProjectAIAgent.Core.csproj
├── tests/
│   └── ProjectAIAgent.Core.Tests/
│       ├── Tools/
│       │   ├── ReadFileToolTests.cs
│       │   └── WriteFileToolTests.cs
│       └── ProjectAIAgent.Core.Tests.csproj
└── README.md

КОНФИГУРАЦИЯ (ТЕКУЩАЯ)

appsettings.json (Host):
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "qwen2.5-coder:7b-instruct",
    "MaxTokens": 4096,
    "Temperature": 0.2,
    "TopP": 0.9,
    "TimeoutSeconds": 120,
    "MaxRetries": 3,
    "RetryDelaySeconds": 2.0,
    "TotalTimeoutSeconds": 300
  },
  "Qdrant": {
    "Endpoint": "http://localhost:6333",
    "VectorSize": 3584,
    "CollectionName": "project_docs"
  },
  "Agent": {
    "ProjectPath": "",
    "MaxRetries": 3,
    "RequireConfirmation": false,
    "AutoCommit": false,
    "WatchDocumentation": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "ProjectAIAgent.Core.Services": "Debug",
      "ProjectAIAgent.Host.OllamaApiClientWrapper": "Information"
    }
  }
}

ПРИНЯТЫЕ АРХИТЕКТУРНЫЕ РЕШЕНИЯ

1. Разделение на Host (Web Application) и Core (Class Library)
2. Ollama как единственный AI-бэкенд (прямые HTTP-вызовы /api/generate и /api/embeddings)
3. Прямой доступ к Ollama API через HTTP (OllamaSharp только для моделей)
4. Semantic Kernel только для Memory, не для оркестрации
5. Собственная оркестрация (AgentWorkerService + OrchestratorAgent)
6. Qdrant как постоянное векторное хранилище (3584, Cosine)
7. Git как система контроля версий изменений
8. Обязательная валидация через dotnet build (BuildValidationService, до 3 попыток)
9. Tool-based архитектура с авторегистрацией (7 инструментов)
10. Prompt-driven поведение агентов (3 промпта, PromptLoader, PromptTesterService)
11. Polly для устойчивости к сбоям (LlmService)
12. Два режима AgentWorkerService (A — Агент, T — Тестирование)
13. Автоматическая индексация документации (старт + FileSystemWatcher с дебаунсом)
14. Контекстная память проекта (ContextAgent + ProjectContext)
15. Форматированные отчёты (ReportService)
16. Три интерфейса взаимодействия: CLI, Web API, Web UI
17. Real-time уведомления через SignalR

СПОСОБЫ ВЗАИМОДЕЙСТВИЯ С АГЕНТОМ

1. Интерактивный режим: dotnet run (без аргументов) -> выбор A (Агент) или T (Тестирование)
2. CLI-команды: dotnet run -- <command> [args] (start, set-project, request, status, history, docs query)
3. Web API: HTTP-запросы к http://localhost:5000/api/agent/*
4. Web UI: http://localhost:5000/ (Razor Pages с SignalR)
5. Swagger: http://localhost:5000/swagger (документация API)

ИНСТРУМЕНТЫ (7)

read_file, write_file, project_structure, read_documentation, update_documentation, run_shell_command, git_diff

АГЕНТЫ (4)

OrchestratorAgent, CodeEditorAgent, DocumentationAgent, ContextAgent

ФОНОВЫЕ СЛУЖБЫ (4)

AgentWorkerService, DocumentationIndexerService, DocumentationWatcherService, BuildValidationService (встроен в OrchestratorAgent)

ТЕКУЩИЕ ПРЕДУПРЕЖДЕНИЯ СБОРКИ
- CS8601 (3 предупреждения): OllamaApiClientWrapper.cs — nullable-ссылки.
- CS8602/CS8604 (10 предупреждений): OrchestratorAgent.cs — reflection-вызовы SignalR.
Все косметические, будут исправлены на Этапе 7.

БЛИЖАЙШИЕ ШАГИ (ПЛАН НА ЭТАП 7)

1. Юнит-тесты для новых инструментов: ReadDocumentationTool, UpdateDocumentationTool, RunShellCommandTool, GitDiffTool
2. Юнит-тесты для сервисов: LlmResponseParser, BuildValidationService, ReportService
3. Интеграционные тесты: сквозной тест оркестрации, тест индексации документации
4. Создать тестовый проект-песочницу для безопасных экспериментов
5. Исправить все предупреждения сборки (CS8601, CS8602, CS8604)
6. Метрики качества и итеративное улучшение промптов