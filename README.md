# AIAgentPlatform

AI-агент на .NET 8 для работы с проектами. Получает запросы на естественном языке, самостоятельно вносит правки в указанный .NET проект, автоматически ведёт и читает документацию, понимая общую картину работы.

## Статус проекта

| Этап | Название | Статус |
|------|----------|--------|
| 1 | Архитектура и подготовка окружения | ✅ Завершён |
| 2 | Проектирование системы агентов | ✅ Завершён |
| 3 | Интеграция локальной модели | ✅ Завершён |
| 4 | Реализация Memory и Документации | ✅ Завершён |
| 5 | Разработка оркестрации | ✅ Завершён |
| 6 | CLI и API | ✅ Завершён |
| 7 | Тестирование и улучшение | ✅ Завершён |
| 8 | Документирование и деплой | 🚧 В процессе |

## Возможности

- **AI-агент** на основе локальной LLM (Ollama + qwen2.5-coder:7b-instruct)
- **Оркестрация**: автоматическое планирование и выполнение изменений
- **Инструменты**: чтение/запись файлов, структура проекта, Git, dotnet build
- **Документация**: автоматическая индексация и семантический поиск (Qdrant)
- **Интерфейсы**: CLI, Web API, Web UI с real-time обновлениями (SignalR)
- **Валидация**: автоматический dotnet build после изменений с ретраями
- **Метрики**: сбор и отображение метрик качества

## Быстрый старт

### Требования

- .NET 8 SDK
- Docker Desktop
- 8+ ГБ RAM (для модели qwen2.5-coder:7b-instruct)

### Установка

```bash
git clone https://github.com/your-username/AIAgentPlatform.git
cd AIAgentPlatform
```

### Запуск через Docker (рекомендуемый)

```bash
cd local-infra
docker compose up -d
docker exec -it ollama-server ollama pull qwen2.5-coder:7b-instruct
docker compose restart agent
```

Агент доступен на http://localhost:5000/.

### Запуск для разработки

```bash
cd local-infra
docker compose up -d ollama qdrant
docker exec -it ollama-server ollama pull qwen2.5-coder:7b-instruct
cd ..
cd ProjectAIAgent.Host
dotnet run
```

При запуске без аргументов — интерактивный режим с выбором:
- **A** — режим Агента (полный цикл оркестрации)
- **T** — режим Тестирования промптов (прямой вызов LLM)

### CLI-команды

```bash
dotnet run -- set-project "D:\\repos\\MyProject"
dotnet run -- request "Добавь метод для валидации email"
dotnet run -- status
dotnet run -- history
dotnet run -- docs query "настройка логирования"
```

### Web API

```bash
curl -X POST http://localhost:5000/api/agent/request -H "Content-Type: application/json" -d '{"query":"Покажи структуру проекта"}'
```

### Web UI

Откройте http://localhost:5000/ — тёмная тема, лог в реальном времени, история.

### Swagger

http://localhost:5000/swagger — документация API.

## Конфигурация

Основные параметры в `appsettings.json`:

| Секция | Параметр | Описание | По умолчанию |
|--------|----------|----------|-------------|
| Ollama | BaseUrl | URL Ollama API | http://localhost:11434 |
| Ollama | Model | Название модели | qwen2.5-coder:7b-instruct |
| Ollama | Temperature | Температура генерации | 0.2 |
| Ollama | MaxTokens | Максимум токенов | 4096 |
| Ollama | MaxRetries | Повторных попыток | 3 |
| Qdrant | Endpoint | URL Qdrant | http://localhost:6333 |
| Qdrant | VectorSize | Размерность вектора | 3584 |
| Qdrant | CollectionName | Коллекция | project_docs |
| Agent | ProjectPath | Путь к проекту | (пусто) |
| Agent | MaxRetries | Попыток сборки | 3 |
| Agent | WatchDocumentation | Отслеживание изменений | true |

## Архитектура

- **Host** (Web Application): API, CLI, Web UI, фоновые службы
- **Core** (Class Library): агенты, инструменты, сервисы, модели
- **4 агента**: OrchestratorAgent, CodeEditorAgent, DocumentationAgent, ContextAgent
- **7 инструментов**: read_file, write_file, project_structure, read_documentation, update_documentation, run_shell_command, git_diff
- **4 фоновые службы**: AgentWorkerService, DocumentationIndexerService, DocumentationWatcherService, BuildValidationService

## Структура проекта

```bash
AIAgentPlatform/
├── .github/
│   └── workflows/
│       └── ci.yml                 # CI/CD: сборка, тесты, Docker-образ
├── AIAgentPlatform.sln
├── Dockerfile                     # Docker-образ агента
├── .dockerignore
├── CONTRIBUTING.md
├── README.md
├── docs/
│   └── USER_GUIDE.md
├── local-infra/
│   └── docker-compose.yml         # Ollama + Qdrant + Agent
├── ProjectAIAgent.Host/           # Web Application
│   ├── Program.cs
│   ├── AgentWorkerService.cs
│   ├── Controllers/
│   ├── Hubs/
│   ├── Pages/
│   └── appsettings.json
├── ProjectAIAgent.Core/           # Class Library
│   ├── Agents/
│   ├── Tools/
│   ├── Services/
│   ├── Models/
│   └── Prompts/
└── tests/
    ├── ProjectAIAgent.Core.Tests/
    └── ProjectAIAgent.Sandbox/
```

### Запуск через Docker Hub (рекомендуемый)

```bash
docker pull твой-username/aiagent-platform:latest
cd local-infra
docker compose up -d
docker exec -it ollama-server ollama pull qwen2.5-coder:7b-instruct
docker compose restart agent
```

Агент доступен на http://localhost:5000/. Замени "твой-username" на свой логин Docker Hub.

## Тестирование

```bash
dotnet test
```

30 юнит-тестов (xUnit + Moq) для инструментов и сервисов. Тестовый проект-песочница в `tests/ProjectAIAgent.Sandbox/`.

## Релизы

Готовые сборки доступны на [странице релизов](https://github.com/Qunama/AIAgentPlatform/releases).

| Платформа | Файл |
|-----------|------|
| Windows x64 | `aiagent-platform-win-x64.zip` |
| Linux x64 | `aiagent-platform-linux-x64.zip` |

Docker-образ: `docker pull твой-username/aiagent-platform:latest`

История изменений: [CHANGELOG.md](CHANGELOG.md)

## Лицензия

MIT