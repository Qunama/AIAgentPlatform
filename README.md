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
```bash

### Запуск инфраструктуры

```bash
cd local-infra
docker compose up -d
```bash

### Загрузка модели

```bash
docker exec -it ollama-server ollama pull qwen2.5-coder:7b-instruct
```bash

### Запуск агента

```bash
cd ProjectAIAgent.Host
dotnet run
```bash

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
```bash

### Web API

```bash
curl -X POST http://localhost:5000/api/agent/request -H "Content-Type: application/json" -d '{"query":"Покажи структуру проекта"}'
```bash

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

## Тестирование

```bash
dotnet test
```bash

30 юнит-тестов (xUnit + Moq) для инструментов и сервисов. Тестовый проект-песочница в `tests/ProjectAIAgent.Sandbox/`.

## Лицензия

MIT