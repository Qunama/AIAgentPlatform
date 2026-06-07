# AIAgentPlatform

AI-агент на .NET 8 для работы с проектами. Получает запросы на естественном языке, самостоятельно вносит правки в указанный .NET проект, автоматически ведёт и читает документацию, понимая общую картину работы.

## Статус проекта

✅ Все 8 этапов завершены. Проект готов к использованию.

## Возможности

- **AI-агент** на основе локальной LLM (Ollama + qwen2.5-coder:7b-instruct)
- **Оркестрация**: автоматическое планирование и выполнение изменений
- **Инструменты**: чтение/запись файлов, структура проекта, Git, dotnet build
- **Документация**: автоматическая индексация и семантический поиск (Qdrant)
- **Интерфейсы**: CLI, Web API, Web UI с real-time обновлениями (SignalR)
- **Валидация**: автоматический dotnet build после изменений с ретраями
- **Метрики**: сбор и отображение метрик качества
- **Docker**: готовый образ с docker compose (ollama + qdrant + agent)

## Быстрый старт

### Требования

- Docker Desktop
- 8+ ГБ RAM (для модели qwen2.5-coder:7b-instruct)

### Запуск через Docker (рекомендуемый)

```bash
git clone https://github.com/Qunama/AIAgentPlatform.git
cd AIAgentPlatform/local-infra
docker compose up -d
docker exec -it ollama-server ollama pull qwen2.5-coder:7b-instruct
```

Агент доступен на http://localhost:5000/.

### Запуск для разработки

```bash
cd local-infra
docker compose up -d ollama qdrant
docker exec -it ollama-server ollama pull qwen2.5-coder:7b-instruct
cd ../ProjectAIAgent.Host
dotnet run
```

### CLI-команды

```bash
dotnet run -- set-project "D:\\repos\\MyProject"
dotnet run -- request "Добавь метод для валидации email"
dotnet run -- status
dotnet run -- history
dotnet run -- docs query "настройка логирования"
```

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
| Agent | ProjectPath | Путь к проекту | (пусто) |
| Agent | MaxRetries | Попыток сборки | 3 |
| Agent | WatchDocumentation | Отслеживание изменений | true |

## Архитектура

- **Host** (Web Application): API, CLI, Web UI, фоновые службы
- **Core** (Class Library): агенты, инструменты, сервисы, модели
- **4 агента**: OrchestratorAgent, CodeEditorAgent, DocumentationAgent, ContextAgent
- **7 инструментов**: read_file, write_file, project_structure, read_documentation, update_documentation, run_shell_command, git_diff
- **30 юнит-тестов**: xUnit + Moq

## Релизы

| Версия | Дата | Описание |
|--------|------|----------|
| [v1.0.1](https://github.com/Qunama/AIAgentPlatform/releases/tag/v1.0.1) | 2026-06-07 | Исправлена утечка CPU в Docker, healthcheck Ollama |
| [v1.0.0](https://github.com/Qunama/AIAgentPlatform/releases/tag/v1.0.0) | 2026-06-07 | Первый релиз |

История изменений: [CHANGELOG.md](CHANGELOG.md)

## Документация

- [Руководство пользователя](docs/USER_GUIDE.md)
- [Руководство контрибьютора](CONTRIBUTING.md)
- [Swagger API](http://localhost:5000/swagger) (после запуска)

## Лицензия

MIT