AIAgentPlatform — AI-агент для работы с проектами (.NET 8)

МИССИЯ ПРОЕКТА
Создать AI-агента на .NET 8, который получает запросы на естественном языке, самостоятельно вносит правки в указанный пользователем .NET проект, автоматически ведёт и читает документацию, понимая общую картину работы.

ТЕКУЩАЯ СТАДИЯ: Этап 1 завершён. Этап 2 завершён. Этап 3 завершён. Этап 4 — следующий.

ЧТО УЖЕ СДЕЛАНО (ЭТАП 1: АРХИТЕКТУРА И ПОДГОТОВКА ОКРУЖЕНИЯ)
- Создано .NET 8 решение с проектами ProjectAIAgent.Host (Worker Service) и ProjectAIAgent.Core (Class Library).
- Настроены и проверены все интеграции:
  - Ollama (локальная LLM qwen2.5-coder:7b-instruct) через Docker.
  - Qdrant (векторная БД) через Docker.
- Написан проверочный код в Program.cs, подтверждающий сквозную работу всей цепочки.
- Проверка пройдена успешно: модель отвечает на тестовые запросы.

ЧТО СДЕЛАНО (ЭТАП 2: ПРОЕКТИРОВАНИЕ СИСТЕМЫ АГЕНТОВ)
- Создан базовый класс BaseAgent в ProjectAIAgent.Core.Agents. Предоставляет общую функциональность: загрузку системных промптов из файлов, доступ к инструментам через DI, кеширование промптов.
- Определён интерфейс IAgentTool с контрактом для всех инструментов. Включает ToolName, Description, ParametersSchema (JSON Schema для LLM) и метод ExecuteAsync. Создан вспомогательный класс ToolResult для стандартизации ответов инструментов (Success, Output, Error, Metadata).
- Создан атрибут AgentToolAttribute для автоматической регистрации инструментов с именем и описанием.
- Реализован ToolRegistry — центральный реестр инструментов. Автоматически собирает все реализации IAgentTool через DI. Предоставляет методы GetTool, ExecuteToolAsync и GetToolsDescription для генерации описания инструментов в системных промптах.
- Реализовано расширение AddAgentTools для автоматической регистрации всех инструментов из сборки через рефлексию.
- Созданы заготовки четырёх агентов:
  - OrchestratorAgent — принимает запросы, планирует и делегирует задачи. Имеет доступ к ToolRegistry для динамического формирования промптов с описанием инструментов.
  - CodeEditorAgent — специализируется на чтении и изменении файлов. Предоставляет методы ReadFileAsync и WriteFileAsync, которые внутри вызывают соответствующие инструменты.
  - DocumentationAgent — агент для работы с документацией (заглушка, функционал на Этапе 4).
  - ContextAgent — агент управления контекстом проекта (заглушка, функционал на Этапе 4).
- Реализованы три базовых инструмента:
  - ReadFileTool — читает содержимое файла. Поддерживает относительные и абсолютные пути. Возвращает контент и метаданные (размер, дата изменения, количество строк). Валидирует входные параметры.
  - WriteFileTool — записывает содержимое в файл. Автоматически создаёт родительские директории при необходимости. Отслеживает, был ли файл новым или изменён существующий.
  - ProjectStructureTool — сканирует структуру проекта и возвращает дерево файлов и директорий. Поддерживает ограничение глубины сканирования. Исключает служебные директории (bin, obj, .git, node_modules). Форматирует размеры файлов в человекочитаемом виде.
- Написаны системные промпты для агентов, хранящиеся в отдельных файлах в Core/Prompts/:
  - orchestrator.txt — определяет рабочий процесс оркестратора: запрос, загрузка контекста, планирование, делегирование, валидация, документирование, отчёт. Содержит правила валидации через dotnet build и ограничение на количество попыток исправления ошибок (до 3 раз).
  - code-editor.txt — правила для редактирования кода: сохранение стиля, минимальные изменения, XML-документация, современные C# практики.
  - documentation.txt — правила для работы с документацией: язык (русский основной, английские резюме), актуализация при изменениях API.
- Создан AgentWorkerService в Host-проекте — фоновая служба, запускающая агента. Реализует интерактивный цикл обработки пользовательских запросов в консоли. Выводит информацию о зарегистрированных инструментах при старте. Поддерживает два режима: режим Агента (полный цикл оркестрации) и режим Тестирования промптов (прямой вызов LLM).
- Создан ILlmService как абстракция над Ollama (заглушка на Этапе 2, реализован на Этапе 3).
- Создан класс LlmService с реальной логикой (на Этапе 3).
- Создан класс OllamaOptions для конфигурации Ollama (BaseUrl, Model, MaxTokens, Temperature, TopP, TimeoutSeconds, MaxRetries, RetryDelaySeconds, TotalTimeoutSeconds).
- Создан интерфейс IOllamaApiClient как тонкая обёртка для HttpClient.
- Написаны юнит-тесты для инструментов:
  - ReadFileToolTests: тест на успешное чтение файла, тест на отсутствующий файл, тест на отсутствующий параметр path.
  - WriteFileToolTests: тест на успешную запись файла, тест на создание вложенных директорий.
- Обнаружена проблема с пакетом Microsoft.Agents.Hosting — данный NuGet-пакет не существует. Принято решение использовать собственную оркестрацию через AgentWorkerService и OrchestratorAgent вместо внешнего фреймворка.
- Сборка проходит успешно (0 ошибок, 3 предупреждения CS8601 о возможно nullable-ссылках в OllamaApiClientWrapper, которые будут исправлены при финальной чистке кода).
- В процессе исправления ошибок сборки добавлены необходимые NuGet-пакеты: Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.DependencyInjection.Abstractions, OllamaSharp, Polly.Core в Core-проект; Microsoft.Extensions.Http в Host-проект.

Ключевые решения Этапа 2:
- Каждый агент наследует BaseAgent и использует GetTool<T>() для доступа к инструментам через DI.
- Инструменты регистрируются автоматически через рефлексию (метод AddAgentTools), не требуя ручной регистрации каждого.
- Системные промпты хранятся в отдельных .txt файлах в Core/Prompts/ и загружаются при первом обращении с кешированием.
- ToolRegistry динамически генерирует описание инструментов для включения в системные промпты агентов через плейсхолдер {TOOLS}.
- В качестве агентного фреймворка используется собственное решение (AgentWorkerService + OrchestratorAgent) вместо несуществующего Microsoft.Agents.Hosting.
- Все инструменты возвращают унифицированный ToolResult, что упрощает обработку ответов агентами.
- Инструменты валидируют входные параметры и возвращают понятные сообщения об ошибках.

ЧТО СДЕЛАНО (ЭТАП 3: ИНТЕГРАЦИЯ ЛОКАЛЬНОЙ МОДЕЛИ)
- Реализован ILlmService с реальной логикой вызова Ollama. Сервис принимает системный промпт и сообщение пользователя, формирует промпт в нативном формате qwen2.5-coder (токены <|system|>, <|user|>, <|assistant|>, <|end|>) и возвращает ответ модели.
- Реализован парсинг ответов модели — класс LlmResponseParser:
  - ExtractCodeBlock / ExtractAllCodeBlocks — извлечение кода из Markdown-блоков (```csharp ... ```).
  - ExtractActionPlan / ExtractJson — извлечение структурированных JSON-планов из ответов (поддерживает чистый JSON, JSON в Markdown-блоках, JSON в тексте).
  - ExtractTextWithoutCode — получение текста без кодовых блоков.
  - ContainsCode — проверка наличия кода в ответе.
  - ConvertJsonElement — рекурсивное преобразование JsonElement в обычные типы C# для корректной работы с кириллицей.
- Настроены параметры генерации через OllamaOptions:
  - Temperature (0.2) — низкая для предсказуемой генерации кода.
  - TopP (0.9).
  - MaxTokens (4096).
  - TimeoutSeconds (120) — таймаут одного HTTP-запроса.
  - MaxRetries (3) — количество повторных попыток при ошибке.
  - RetryDelaySeconds (2.0) — базовая задержка между повторами (растёт экспоненциально).
  - TotalTimeoutSeconds (300) — общий таймаут на все попытки.
- Добавлена обработка ошибок и ретраи через Polly:
  - ResiliencePipeline с Retry + Timeout.
  - Повторяются только сетевые ошибки (HttpRequestException, TaskCanceledException). Ошибка 404 (модель не найдена) не ретраится.
  - Экспоненциальная задержка: 2с → 4с → 8с.
  - Логирование при каждом ретрае.
- Передан ProjectPath в инструменты через IOptions<AgentOptions>:
  - Создан класс AgentOptions (ProjectPath, MaxRetries, RequireConfirmation, AutoCommit, WatchDocumentation).
  - Все три инструмента (ReadFileTool, WriteFileTool, ProjectStructureTool) инжектят IOptions<AgentOptions> и инициализируют _projectRoot.
  - Предупреждения CS0649 устранены.
- Создан консольный интерфейс для ручного тестирования промптов:
  - PromptTesterService — отдельный сервис для прямого общения с LLM в обход агентов.
  - PromptLoader — загрузка промптов из файлов Prompts/*.txt с кешированием.
  - AgentWorkerService переработан: при старте выбор режима A (Агент) или T (Тестирование промптов).
  - В тестовом режиме: выбор роли (Orchestrator/CodeEditor/Documentation), просмотр системного промпта, отправка запросов, просмотр сырых ответов, извлечение кода, статистика.
- Интегрирована LLM в OrchestratorAgent.ProcessRequestAsync:
  - Полный цикл оркестрации: Запрос → LLM → Парсинг JSON → Вызов инструмента → Результат → LLM → ... → Report.
  - conversationHistory (StringBuilder) накапливает историю диалога.
  - ExtractActionPlanRobust — извлечение JSON из ответа с поддержкой Markdown-блоков.
  - ExecuteToolFromPlan — вызов инструментов через ToolRegistry с поддержкой Dictionary<string, object> и JsonElement.
  - Ограничение результатов read_file до 3000 символов для предотвращения переполнения контекста.
  - Защита от бесконечного цикла: максимум 10 итераций.
  - Поддержка полей message/summary/report в ответах модели.
- Доработаны системные промпты на основе реального тестирования:
  - orchestrator.txt: добавлены примеры для project_structure, read_file, write_file; правило «Do NOT wrap JSON in triple-backtick blocks»; уточнение про язык ответа; явный path:"." для project_structure.
  - code-editor.txt: добавлены конкретные C# 12 практики (primary constructors, collection expressions, pattern matching); правило «NO TODO»; пример с email validation и XML-документацией.
  - documentation.txt: добавлена структура документации; пример документирования метода; правило «Never invent parameters/exceptions — mark unknown as [Неизвестно]»; инструкция запрашивать код через read_file в production-режиме.
- Результаты тестирования промптов:
  - Orchestrator: стабильно возвращает чистый JSON с правильными tool и args (10/10).
  - Code Editor: генерирует код в Markdown-блоках с XML-документацией и современным C# (9.5/10).
  - Documentation: честно отказывается документировать без доступа к коду, не выдумывает детали (10/10 после доработки).

ЧТО БУДЕТ СДЕЛАНО (ЭТАП 4: РЕАЛИЗАЦИЯ MEMORY И ДОКУМЕНТАЦИИ)
Цель: Создать систему автоматического чтения, индексации и обновления документации проекта.
Подзадачи:
1. Не выполнено: Реализовать DocumentationService в Core
2. Не выполнено: Индексация документации при старте агента (поиск .md, .txt, разбиение на чанки, векторизация через Ollama embeddings, сохранение в Qdrant с размерностью 3584)
3. Не выполнено: Реализовать ReadDocumentationTool (семантический поиск по документации)
4. Не выполнено: Реализовать UpdateDocumentationTool (добавление/обновление секций, реиндексация)
5. Не выполнено: Отслеживание изменений в документации через FileSystemWatcher
6. Не выполнено: Контекстная память проекта (история взаимодействий, текущая фаза, связь изменений с документацией)
Ключевые решения:
- Индексация запускается при старте и при обнаружении изменений в docs/
- Чанки документации сохраняются с метаданными (файл, секция, дата)
- Для поиска используется гибридный подход: семантический + ключевые слова

ПОЛНЫЙ ПЛАН ПРОЕКТА (8 ЭТАПОВ)

ЭТАП 1: АРХИТЕКТУРА И ПОДГОТОВКА ОКРУЖЕНИЯ — ЗАВЕРШЁН
Цель: Создать фундамент проекта, настроить все внешние зависимости, проверить их работоспособность.
Подзадачи:
1. Выполнено: Создание .NET 8 решения с проектами Host (Worker Service) и Core (Class Library)
2. Выполнено: Подключение NuGet-пакетов (Microsoft.Agents.Hosting исключён, SemanticKernel, OllamaSharp, CliWrap, LibGit2Sharp)
3. Выполнено: Настройка Docker-окружения (Ollama + Qdrant)
4. Выполнено: Загрузка модели qwen2.5-coder:7b-instruct в Ollama
5. Выполнено: Написание проверочного кода в Program.cs
6. Выполнено: Подтверждение сквозной работы: .NET -> Ollama -> ответ
7. Выполнено: Базовая регистрация OllamaApiClient в DI
Ключевые решения:
- В качестве LLM выбрана qwen2.5-coder:7b-instruct
- Для взаимодействия с Ollama используется прямой API через OllamaSharp (метод GenerateAsync)
- Semantic Kernel будет использоваться только для Memory (Этап 4)
- Размерность вектора для Qdrant: 3584

ЭТАП 2: ПРОЕКТИРОВАНИЕ СИСТЕМЫ АГЕНТОВ — ЗАВЕРШЁН
Цель: Спроектировать и реализовать базовую архитектуру агентов, их роли и инструменты.
Подзадачи:
1. Выполнено: Создать базовый класс BaseAgent в ProjectAIAgent.Core.Agents
2. Выполнено: Определить интерфейсы для инструментов IAgentTool
3. Выполнено: Реализовать систему регистрации и вызова инструментов (ToolRegistry, AddAgentTools)
4. Выполнено: Создать заготовки агентов: OrchestratorAgent, CodeEditorAgent, DocumentationAgent, ContextAgent
5. Выполнено: Реализовать базовые инструменты: ReadFileTool, WriteFileTool, ProjectStructureTool
6. Выполнено: Настроить собственную оркестрацию через AgentWorkerService (вместо Microsoft.Agents.Hosting)
7. Выполнено: Написать юнит-тесты для инструментов ReadFileTool и WriteFileTool
8. Выполнено: Создать системные промпты для агентов (orchestrator.txt, code-editor.txt, documentation.txt)
Ключевые решения:
- Каждый агент — отдельный класс, наследующий BaseAgent
- Инструменты регистрируются через DI автоматически и могут быть переиспользованы разными агентами
- Системные промпты для каждого агента хранятся в отдельных файлах .txt в Core/Prompts/
- Оркестрация выполняется собственным кодом, без внешнего агентного фреймворка

ЭТАП 3: ИНТЕГРАЦИЯ ЛОКАЛЬНОЙ МОДЕЛИ — ЗАВЕРШЁН
Цель: Настроить полноценное взаимодействие агентов с Ollama, оптимизировать промпты для генерации кода.
Подзадачи:
1. Выполнено: Реализовать ILlmService с реальной логикой вызова Ollama через прямые HTTP-вызовы /api/generate
2. Выполнено: Реализовать управление контекстным окном (история диалога через conversationHistory)
3. Выполнено: Разработать шаблоны системных промптов для каждого агента (редактирование кода, анализ документации, оркестратор)
4. Выполнено: Реализовать парсинг ответов модели (извлечение кода из Markdown-блоков, извлечение JSON-планов)
5. Выполнено: Настроить параметры генерации (temperature: 0.2, top_p: 0.9, max_tokens: 4096) через OllamaOptions
6. Выполнено: Добавить обработку ошибок и ретраи при сбоях Ollama (Polly, 3 попытки, экспоненциальная задержка)
7. Выполнено: Создать консольный интерфейс для ручного тестирования промптов (режим T в AgentWorkerService)
8. Выполнено: Передать ProjectPath в инструменты через IOptions<AgentOptions> (устранены CS0649)
Ключевые решения:
- ILlmService инкапсулирует всё взаимодействие с Ollama
- Для генерации кода используется низкая temperature (0.2) для предсказуемости
- Ответы модели парсятся: код извлекается из блоков ```csharp ... ```, JSON — из текста и Markdown-блоков
- Добавлен Polly ResiliencePipeline с ретраями и таймаутами
- OrchestratorAgent реализует цикл «LLM → JSON → Инструмент → Результат → LLM» с защитой от зацикливания
- Промпты протестированы вручную через тестовый режим и показывают стабильные результаты

ЭТАП 4: РЕАЛИЗАЦИЯ MEMORY И ДОКУМЕНТАЦИИ
Цель: Создать систему автоматического чтения, индексации и обновления документации проекта.
Подзадачи:
1. Не выполнено: Реализовать DocumentationService в Core
2. Не выполнено: Индексация документации при старте агента (поиск .md, .txt, разбиение на чанки, векторизация через Ollama embeddings, сохранение в Qdrant с размерностью 3584)
3. Не выполнено: Реализовать ReadDocumentationTool (семантический поиск по документации)
4. Не выполнено: Реализовать UpdateDocumentationTool (добавление/обновление секций, реиндексация)
5. Не выполнено: Отслеживание изменений в документации через FileSystemWatcher
6. Не выполнено: Контекстная память проекта (история взаимодействий, текущая фаза, связь изменений с документацией)
Ключевые решения:
- Индексация запускается при старте и при обнаружении изменений в docs/
- Чанки документации сохраняются с метаданными (файл, секция, дата)
- Для поиска используется гибридный подход: семантический + ключевые слова

ЭТАП 5: РАЗРАБОТКА ОРКЕСТРАЦИИ
Цель: Реализовать центральный цикл работы агента, связывающий все компоненты воедино.
Подзадачи:
1. Не выполнено: Реализовать основной цикл в OrchestratorAgent: Запрос -> Загрузка контекста -> Планирование -> Исполнение -> Валидация -> Документирование -> Отчёт
2. Не выполнено: Реализовать ProjectContext (путь к проекту, структура файлов, последние изменения, индекс документации)
3. Не выполнено: Создать ChangePlan (список файлов для изменения, описание изменений, порядок выполнения)
4. Не выполнено: Реализовать цепочку вызовов агентов через ToolRegistry
5. Не выполнено: Добавить валидацию после изменений (dotnet build, тесты, откат при ошибке)
6. Не выполнено: Реализовать систему отчётов пользователю
Ключевые решения:
- Оркестратор не редактирует код сам, только делегирует
- Каждое изменение проходит через dotnet build — это обязательный gate
- При провале сборки агент пытается исправить ошибки (до N попыток)
- Все изменения логируются с возможностью отката

ЭТАП 6: CLI И API
Цель: Создать удобные интерфейсы для взаимодействия с агентом.
Подзадачи:
1. Не выполнено: Разработать CLI-команды: agent start, agent set-project path, agent request "описание", agent status, agent history, agent docs query
2. Не выполнено: Реализовать Web API: POST /api/agent/set-project, POST /api/agent/request, GET /api/agent/status, GET /api/agent/history
3. Не выполнено: Добавить Swagger/OpenAPI документацию
4. Не выполнено: Реализовать SignalR хаб для real-time обновлений (прогресс, логи, ошибки)
5. Не выполнено: Создать простой Web UI (опционально, Razor Pages)
Ключевые решения:
- CLI и API используют один и тот же IAgentService
- SignalR для интерактивного режима
- Все эндпоинты требуют подтверждения для деструктивных операций (запись файлов)

ЭТАП 7: ТЕСТИРОВАНИЕ И УЛУЧШЕНИЕ
Цель: Обеспечить надёжность, измерить качество работы агента.
Подзадачи:
1. Не выполнено: Юнит-тесты для каждого инструмента, парсинга ответов LLM, валидации изменений
2. Не выполнено: Интеграционные тесты (сквозной тест, тест индексации, тест с разными типами проектов)
3. Не выполнено: Создать тестовый проект-песочницу для безопасных экспериментов
4. Не выполнено: Метрики качества (процент успешных сборок, процент прохождения тестов, время выполнения)
5. Не выполнено: Система сбора обратной связи (оценка изменений, логирование ошибок)
6. Не выполнено: Итеративное улучшение промптов на основе метрик
Ключевые решения:
- Тестовый проект-песочница — изолированная среда для тестов
- Все инструменты должны иметь юнит-тесты до интеграции
- Метрики собираются в Prometheus-формате (опционально)

ЭТАП 8: ДОКУМЕНТИРОВАНИЕ И ДЕПЛОЙ
Цель: Подготовить проект к использованию и распространению.
Подзадачи:
1. Не выполнено: Написать полную документацию (установка, пользователь, API reference, контрибьютор)
2. Не выполнено: Docker-контейнеризация самого агента (Dockerfile, Docker Compose с Ollama, Qdrant и агентом)
3. Не выполнено: CI/CD пайплайн (GitHub Actions: сборка, тестирование, публикация Docker-образа, линтинг)
4. Не выполнено: Подготовка релиза (CHANGELOG.md, SemVer, бинарники для ОС)
5. Не выполнено: Видео-демонстрация работы (опционально)
6. Не выполнено: Статья/презентация о проекте
Ключевые решения:
- Docker — основной способ распространения
- GitHub Actions для автоматизации
- README.md на русском и английском

СТЕК И ЗАВИСИМОСТИ (ФИНАЛЬНАЯ ВЕРСИЯ ЭТАПА 3)

ИНФРАСТРУКТУРА
- Локальная LLM: Ollama (qwen2.5-coder:7b-instruct) — генерация кода и ответов агентом
- Векторная БД: Qdrant (в Docker) — хранение документации и контекста проекта для семантического поиска
- Контейнеризация: Docker Compose — управление Ollama и Qdrant локально

.NET СТЕК
- Microsoft.Extensions.Hosting — проект Host — жизненный цикл приложения
- Microsoft.Extensions.Logging — проект Host и Core — логирование
- Microsoft.Extensions.Logging.Console — проект Host — вывод логов в консоль
- Microsoft.Extensions.Logging.Abstractions — проект Core — абстракции логирования
- Microsoft.Extensions.DependencyInjection.Abstractions — проект Core — DI-абстракции
- Microsoft.Extensions.Options — проект Core — паттерн Options для конфигураций
- Microsoft.Extensions.Http — проект Host — HttpClientFactory и AddHttpClient
- Polly.Core — проект Core — политики ретраев и таймаутов для вызовов Ollama
- Microsoft.SemanticKernel (prerelease) — проект Host — Memory и работа с эмбеддингами
- Microsoft.SemanticKernel.Plugins.Memory (prerelease) — проект Host — работа с Memory Store
- Microsoft.SemanticKernel.Connectors.Qdrant (prerelease) — проект Host — коннектор к Qdrant
- OllamaSharp — проект Host и Core — модели и типы для работы с Ollama API
- CliWrap — проект Core — удобный запуск внешних команд (dotnet build, тесты)
- LibGit2Sharp — проект Core — работа с Git (диффы, контроль версий)

ИСКЛЮЧЁННЫЕ ПАКЕТЫ
- Microsoft.Agents.Hosting — исключён, так как пакет не существует в NuGet. Заменён собственной оркестрацией через AgentWorkerService.

ПЛАНИРУЕМЫЕ ПАКЕТЫ ДЛЯ БУДУЩИХ ЭТАПОВ
- Microsoft.AspNetCore.SignalR — Этап 6 — Real-time уведомления
- Swashbuckle.AspNetCore — Этап 6 — Swagger для API
- Spectre.Console — Этап 6 — Красивый CLI
- System.CommandLine — Этап 6 — Парсинг CLI-команд
- xUnit + Moq — Этап 7 — Тестирование
- Prometheus.Client — Этап 7 — Метрики (опционально)

СТРУКТУРА РЕШЕНИЯ (ТЕКУЩАЯ, ПОСЛЕ ЭТАПА 3)
AIAgentPlatform/
├── AIAgentPlatform.sln
├── local-infra/
│   └── docker-compose.yml          # Ollama + Qdrant
├── ProjectAIAgent.Host/            # Точка входа, DI, конфигурация, оркестрация
│   ├── Program.cs                  # Регистрация агентов, инструментов, WorkerService
│   ├── AgentWorkerService.cs       # Фоновая служба с выбором режима (A — агент, T — тестирование)
│   ├── OllamaApiClientWrapper.cs   # Реализация IOllamaApiClient через прямые HTTP-вызовы /api/generate
│   ├── PromptTesterService.cs      # Сервис для ручного тестирования промптов
│   ├── appsettings.json            # Конфигурация Ollama (с параметрами генерации и ретраев), Qdrant, Agent
│   └── ProjectAIAgent.Host.csproj
├── ProjectAIAgent.Core/            # Бизнес-логика, агенты, инструменты
│   ├── Agents/
│   │   ├── BaseAgent.cs            # Базовый класс агента с загрузкой промптов и GetTool
│   │   ├── OrchestratorAgent.cs    # Оркестратор с циклом «LLM → JSON → Инструмент → Результат»
│   │   ├── CodeEditorAgent.cs      # Редактор кода с ReadFileAsync и WriteFileAsync
│   │   ├── DocumentationAgent.cs   # Заглушка агента документации
│   │   └── ContextAgent.cs         # Заглушка агента контекста
│   ├── Tools/
│   │   ├── IAgentTool.cs           # Интерфейс, ToolResult, AgentToolAttribute
│   │   ├── ToolRegistry.cs         # Реестр инструментов с авторегистрацией
│   │   ├── ReadFileTool.cs         # Чтение файлов (инжектирован AgentOptions)
│   │   ├── WriteFileTool.cs        # Запись файлов с созданием директорий (инжектирован AgentOptions)
│   │   └── ProjectStructureTool.cs # Сканирование структуры проекта (инжектирован AgentOptions)
│   ├── Services/
│   │   ├── ILlmService.cs          # Интерфейс LLM-сервиса
│   │   ├── LlmService.cs           # Реализация LLM-сервиса с Polly-пайплайном
│   │   ├── LlmResponseParser.cs    # Парсинг ответов LLM (код из Markdown, JSON-планы)
│   │   ├── IOllamaApiClient.cs     # Интерфейс клиента Ollama
│   │   ├── OllamaOptions.cs        # Конфигурация Ollama (полная)
│   │   ├── AgentOptions.cs         # Конфигурация агента (ProjectPath, MaxRetries, RequireConfirmation)
│   │   └── PromptLoader.cs         # Загрузка и кеширование промптов из файлов
│   ├── Prompts/
│   │   ├── orchestrator.txt        # Системный промпт оркестратора (улучшен)
│   │   ├── code-editor.txt         # Системный промпт редактора кода (улучшен)
│   │   └── documentation.txt       # Системный промпт документатора (улучшен)
│   └── ProjectAIAgent.Core.csproj
├── tests/                          # Тесты
│   └── ProjectAIAgent.Core.Tests/
│       ├── Tools/
│       │   ├── ReadFileToolTests.cs
│       │   └── WriteFileToolTests.cs
│       └── ProjectAIAgent.Core.Tests.csproj
└── README.md

ПЛАНИРУЕМАЯ ПОЛНАЯ СТРУКТУРА (ПОСЛЕ ВСЕХ ЭТАПОВ)
AIAgentPlatform/
├── AIAgentPlatform.sln
├── local-infra/
│   └── docker-compose.yml
├── src/
│   ├── ProjectAIAgent.Host/
│   │   ├── Program.cs
│   │   ├── AgentWorkerService.cs
│   │   ├── OllamaApiClientWrapper.cs
│   │   ├── PromptTesterService.cs
│   │   ├── appsettings.json
│   │   ├── Controllers/           # Web API (Этап 6)
│   │   ├── Hubs/                  # SignalR (Этап 6)
│   │   └── Dockerfile            # (Этап 8)
│   ├── ProjectAIAgent.Core/
│   │   ├── Agents/
│   │   │   ├── BaseAgent.cs
│   │   │   ├── OrchestratorAgent.cs
│   │   │   ├── CodeEditorAgent.cs
│   │   │   ├── DocumentationAgent.cs
│   │   │   └── ContextAgent.cs
│   │   ├── Tools/
│   │   │   ├── IAgentTool.cs
│   │   │   ├── ToolRegistry.cs
│   │   │   ├── ReadFileTool.cs
│   │   │   ├── WriteFileTool.cs
│   │   │   ├── ProjectStructureTool.cs
│   │   │   ├── SearchCodebaseTool.cs
│   │   │   ├── RunShellCommandTool.cs
│   │   │   ├── GitDiffTool.cs
│   │   │   ├── ReadDocumentationTool.cs
│   │   │   └── UpdateDocumentationTool.cs
│   │   ├── Services/
│   │   │   ├── ILlmService.cs
│   │   │   ├── LlmService.cs
│   │   │   ├── LlmResponseParser.cs
│   │   │   ├── IOllamaApiClient.cs
│   │   │   ├── OllamaOptions.cs
│   │   │   ├── AgentOptions.cs
│   │   │   ├── PromptLoader.cs
│   │   │   ├── IDocumentationService.cs
│   │   │   ├── DocumentationService.cs
│   │   │   └── IAgentService.cs
│   │   ├── Models/
│   │   │   ├── ProjectContext.cs
│   │   │   ├── ChangePlan.cs
│   │   │   ├── AgentRequest.cs
│   │   │   └── AgentResponse.cs
│   │   └── Prompts/
│   │       ├── orchestrator.txt
│   │       ├── code-editor.txt
│   │       └── documentation.txt
│   └── ProjectAIAgent.Cli/        # CLI-интерфейс (Этап 6)
├── tests/
│   ├── ProjectAIAgent.Core.Tests/
│   └── ProjectAIAgent.Integration.Tests/
├── docs/                          # Документация проекта (Этап 8)
├── .github/workflows/             # CI/CD (Этап 8)
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
    "Endpoint": "http://localhost:6333"
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

ПЛАНИРУЕМАЯ РАСШИРЕННАЯ КОНФИГУРАЦИЯ (БУДУЩИЕ ЭТАПЫ):
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
    "MaxRetries": 3,
    "AutoCommit": false,
    "RequireConfirmation": true,
    "ProjectPath": "",
    "WatchDocumentation": true
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "ProjectAIAgent": "Debug"
    }
  }
}

docker-compose.yml (local-infra):
Сервисы: ollama (порт 11434) и qdrant (порты 6333, 6334).
Модель Ollama: qwen2.5-coder:7b-instruct (загружена через docker exec).

ПРИНЯТЫЕ АРХИТЕКТУРНЫЕ РЕШЕНИЯ

1. Разделение на Host и Core
   - Host отвечает за инфраструктуру (DI, конфигурация, хостинг, оркестрация).
   - Core содержит всю бизнес-логику (агенты, инструменты, модели, сервисы).
   - Это обеспечивает тестируемость и возможность смены способа развёртывания.

2. Ollama как единственный AI-бэкенд
   - Выбор сделан для полной локальности и независимости.
   - Модель qwen2.5-coder:7b-instruct выбрана как основная (уже загружена в локальном Ollama, показала отличные результаты в тестах).
   - Важно: модель можно заменить на любую другую через appsettings.json.

3. Прямой доступ к Ollama API через HTTP
   - OllamaSharp используется только для моделей (GenerateRequest, RequestOptions).
   - Фактические вызовы выполняются через прямой HTTP POST на /api/generate в OllamaApiClientWrapper.
   - Причина: коннектор OllamaSharp в версии 5.x формирует URL, несовместимый с нашим эндпоинтом, что вызывало ошибку 404.
   - Прямые HTTP-вызовы гарантируют совместимость и повторяют успешный проверочный код Этапа 1.

4. Semantic Kernel используется ТОЛЬКО для Memory, не для оркестрации агентов
   - Оркестрацию агентов выполняет собственный AgentWorkerService с OrchestratorAgent.
   - SK выступает как библиотека для работы с Memory и текстовыми эмбеддингами.

5. Собственная оркестрация вместо Microsoft.Agents.Hosting
   - Пакет Microsoft.Agents.Hosting не существует в NuGet.
   - Вместо него реализован AgentWorkerService (BackgroundService), который запускает интерактивный цикл обработки запросов через OrchestratorAgent.
   - Решение даёт полный контроль над оркестрацией и не зависит от нестабильных/несуществующих внешних фреймворков.

6. Qdrant как постоянное векторное хранилище
   - Выбрано вместо In-Memory Store для сохранения индекса документации между перезапусками.
   - Размерность вектора: 3584 (соответствует embedding_length модели qwen2.5-coder).

7. Git как система контроля версий изменений агента
   - Все изменения агента идут через Git (через LibGit2Sharp).
   - Это даёт возможность отката и аудита изменений.

8. Обязательная валидация через dotnet build
   - Любое изменение кода должно проходить сборку.
   - При провале — автоматическая попытка исправления (до N раз).

9. Подтверждение деструктивных операций
   - Запись файлов требует подтверждения (флаг RequireConfirmation).
   - В автономном режиме может быть отключено.

10. Tool-based архитектура с авторегистрацией
    - Инструменты реализуют IAgentTool и регистрируются автоматически через рефлексию.
    - ToolRegistry предоставляет единую точку доступа ко всем инструментам.
    - Агенты получают инструменты через GetTool<T>() из DI-контейнера.

11. Prompt-driven поведение агентов
    - Системные промпты хранятся в отдельных .txt файлах.
    - Загружаются при первом обращении и кешируются (PromptLoader).
    - Содержат плейсхолдер {TOOLS} для динамического добавления описания доступных инструментов.
    - Протестированы через PromptTesterService и показывают стабильные результаты.

12. Polly для устойчивости к сбоям
    - LlmService использует ResiliencePipeline с Retry + Timeout.
    - Ретраи только для сетевых ошибок (не для 404).
    - Экспоненциальная задержка между попытками.

13. Два режима работы AgentWorkerService
    - Режим A (Агент): полный цикл оркестрации через OrchestratorAgent.
    - Режим T (Тестирование): прямое общение с LLM через PromptTesterService для отладки промптов.
    - Выбор режима при старте приложения.

АРХИТЕКТУРА АГЕНТОВ (ТЕКУЩАЯ, ПОСЛЕ ЭТАПА 3)

Агенты в пространстве имён ProjectAIAgent.Core.Agents:
- OrchestratorAgent — принимает запросы пользователя, формирует план, вызывает инструменты через ToolRegistry, возвращает отчёт. Реализован цикл: Запрос → LLM → Парсинг JSON → Инструмент → Результат → LLM → ... → Report. Защита от зацикливания (MaxIterations = 10).
- CodeEditorAgent — специализируется на чтении/изменении файлов проекта. Инструменты: ReadFile, WriteFile, SearchCodebase (будущий).
- DocumentationAgent — читает, анализирует и обновляет документацию (заглушка, Этап 4). Инструменты: ReadDocs, UpdateDocs, ProjectStructure (будущие).
- ContextAgent — управляет памятью, формирует понимание общей картины проекта (заглушка, Этап 4). Инструменты: SearchCodebase, ReadDocs (будущие).

Инструменты агентов (Tools) в ProjectAIAgent.Core.Tools:
- ReadFileTool — чтение содержимого файла. Зависимости: FileSystem, IOptions<AgentOptions>. Статус: реализован, протестирован.
- WriteFileTool — запись содержимого в файл. Зависимости: FileSystem, IOptions<AgentOptions>. Статус: реализован, протестирован.
- ProjectStructureTool — анализ структуры решения. Зависимости: FileSystem, IOptions<AgentOptions>. Статус: реализован.
- SearchCodebaseTool — семантический поиск по коду. Зависимость: Memory Store. Статус: не реализован (Этап 4).
- RunShellCommandTool — выполнение консольных команд. Зависимость: CliWrap. Статус: не реализован (Этап 5).
- GitDiffTool — просмотр изменений Git. Зависимость: LibGit2Sharp. Статус: не реализован (Этап 5).
- ReadDocumentationTool — чтение документации проекта. Зависимость: Memory Store. Статус: не реализован (Этап 4).
- UpdateDocumentationTool — обновление документации. Зависимости: Memory Store, FileSystem. Статус: не реализован (Этап 4).

Сервисы в ProjectAIAgent.Core.Services и ProjectAIAgent.Host:
- ILlmService / LlmService — генерация ответов через Ollama с Polly-ретраями. Статус: реализован.
- LlmResponseParser — парсинг ответов LLM (код из Markdown, JSON-планы). Статус: реализован.
- IOllamaApiClient / OllamaApiClientWrapper — HTTP-клиент для Ollama API. Статус: реализован.
- OllamaOptions — конфигурация Ollama (BaseUrl, Model, параметры генерации, ретраи). Статус: реализован.
- AgentOptions — конфигурация агента (ProjectPath, MaxRetries, RequireConfirmation). Статус: реализован.
- PromptLoader — загрузка и кеширование системных промптов. Статус: реализован.
- PromptTesterService — интерактивное тестирование промптов. Статус: реализован.

ТИПИЧНЫЙ WORKFLOW АГЕНТА (ЦЕЛЕВОЙ)

1. Пользователь: "Добавь метод для валидации email в UserService"
2. ContextAgent загружает контекст проекта (структура, документация)
3. OrchestratorAgent формирует ChangePlan:
   - Изменить: src/Services/UserService.cs
   - Обновить: docs/api/UserService.md
4. CodeEditorAgent:
   a. Читает UserService.cs через ReadFileTool
   b. Генерирует метод ValidateEmail
   c. Записывает изменения через WriteFileTool
5. RunShellCommandTool: dotnet build -> Успех
6. DocumentationAgent обновляет документацию
7. GitDiffTool показывает изменения пользователю
8. При подтверждении — коммит в Git
9. Пользователь получает отчёт

АЛЬТЕРНАТИВНЫЙ СЦЕНАРИЙ (С ОШИБКОЙ)
5. RunShellCommandTool: dotnet build -> Ошибка
6. Агент анализирует ошибку сборки
7. CodeEditorAgent исправляет код (попытка 2/3)
8. dotnet build -> Успех
9. Продолжение основного сценария

ТЕКУЩИЙ WORKFLOW (ПОСЛЕ ЭТАПА 3)

1. Пользователь вводит запрос в консоль (режим A).
2. AgentWorkerService передаёт запрос в OrchestratorAgent.ProcessRequestAsync.
3. OrchestratorAgent формирует системный промпт с описанием инструментов ({TOOLS}).
4. Запрос отправляется в LLM через LlmService → OllamaApiClientWrapper → HTTP POST /api/generate.
5. LlmResponseParser.ExtractActionPlanRobust извлекает JSON-план из ответа (поддерживает чистый JSON и JSON в Markdown-блоках).
6. Если action = "delegate" — ExecuteToolFromPlan вызывает указанный инструмент через ToolRegistry.ExecuteToolAsync.
7. Результат инструмента добавляется в conversationHistory.
8. Цикл повторяется (до 10 итераций), пока LLM не вернёт action = "report".
9. Пользователь получает финальный отчёт.

БЛИЖАЙШИЕ ШАГИ (ПЛАН НА ЭТАП 4)

1. Не выполнено: Реализовать DocumentationService в Core (поиск .md/.txt файлов, разбиение на чанки, векторизация через Ollama embeddings)
2. Не выполнено: Настроить сохранение чанков в Qdrant (коллекция project_docs, размерность вектора 3584)
3. Не выполнено: Реализовать ReadDocumentationTool (семантический поиск по индексированной документации)
4. Не выполнено: Реализовать UpdateDocumentationTool (добавление/обновление документации с реиндексацией)
5. Не выполнено: Настроить FileSystemWatcher для отслеживания изменений в документации
6. Не выполнено: Реализовать ContextAgent и ProjectContext (история взаимодействий, текущая фаза, связь изменений с документацией)

ТЕКУЩИЕ ПРЕДУПРЕЖДЕНИЯ СБОРКИ
- CS8601: Возможно, назначение-ссылка, допускающее значение NULL в OllamaApiClientWrapper.cs (строки 46, 47, 48). Косметические предупреждения, будут исправлены при финальной чистке кода.

КАК ЗАПУСТИТЬ ТЕКУЩУЮ ВЕРСИЮ
1. Запустить инфраструктуру (если ещё не запущена):
   cd local-infra
   docker compose up -d
2. Проверить, что модель загружена:
   docker exec -it ollama-server ollama list
3. Если модели qwen2.5-coder:7b-instruct нет, загрузить:
   docker exec -it ollama-server ollama pull qwen2.5-coder:7b-instruct
4. Запустить проект:
   cd ../ProjectAIAgent.Host
   dotnet run
Ожидаемый результат:
- Выбор режима: A (Агент) или T (Тестирование промптов).
- В режиме A: ввод запроса → цикл оркестрации → вызов инструментов → отчёт.
- В режиме T: выбор роли → ввод запроса → прямой ответ LLM с показом сырого ответа, извлечённого кода и статистики.
- Зарегистрировано 3 инструмента: read_file, write_file, project_structure.
- Рекомендуется указать "ProjectPath" в секции "Agent" в appsettings.json для корректной работы инструментов. Если путь пустой, инструменты могут падать с ошибкой "The path is empty" — в этом случае укажите путь к корню проекта или используйте "." в запросе.

ВАЖНЫЕ ЗАМЕТКИ
- Все пакеты Microsoft.SemanticKernel.* используют --prerelease версии. При проблемах совместимости в будущем нужно сверяться с официальной документацией.
- Модель qwen2.5-coder:7b-instruct требует около 8 ГБ RAM/VRAM. При нехватке ресурсов можно заменить на более лёгкую модель.
- Qdrant настроен с вектором размерности 3584 (соответствует embedding_length модели qwen2.5-coder). При смене модели может потребоваться корректировка этого параметра.
- Все изменения кода агента проходят через Git — это даёт возможность отката.
- В production-режиме рекомендуется включить RequireConfirmation: true.
- Пакет Microsoft.Agents.Hosting исключён из проекта, так как не существует в NuGet. Оркестрация выполняется собственным AgentWorkerService.
- Агенты DocumentationAgent и ContextAgent созданы как заглушки, их функционал будет реализован на Этапе 4.
- Инструменты ReadFileTool и WriteFileTool покрыты юнит-тестами. ProjectStructureTool пока без тестов.
- Для предотвращения переполнения контекстного окна результаты read_file обрезаются до 3000 символов.
- LlmResponseParser использует конвертацию JsonElement в обычные типы C# через ConvertJsonElement для корректной работы с кириллицей и другими символами Unicode.
- При ошибке 404 с сообщением "model not found" необходимо загрузить модель через docker exec -it ollama-server ollama pull qwen2.5-coder:7b-instruct.
- В тестовом режиме (T) можно переключать роли командой /role и просматривать системный промпт командой /system.