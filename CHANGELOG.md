# Changelog

All notable changes to AIAgentPlatform will be documented in this file.

## [1.1.0] — 2026-06-07

### Added
- search_codebase — семантический поиск по исходному коду (Qdrant)
- find_usages — поиск использований класса/метода
- Предварительное планирование в оркестраторе
- ChangePlan с отслеживанием прогресса
- Многофайловые изменения с проверкой зависимостей
- Валидация с извлечением файлов с ошибками
- 3 специализированных промпта (refactoring, bugfix, feature)
- Few-shot примеры во всех промптах
- Автовыбор модели (7B/14B/16B) по запросу
- VS Code расширение
- Embedding-модель nomic-embed-text (768)
- Коллекция project_docs_code для кода

### Changed
- Инструментов: 7 → 9
- Промптов: 3 → 6
- Моделей: 1 → 4 (включая embedding)
- Qdrant VectorSize: 3584 → 768

## [1.0.1] — 2026-06-07

### Fixed
- 300% CPU usage in Docker container (AgentWorkerService interactive loop without stdin)
- Ollama healthcheck failing due to missing curl in container

## [1.0.0] — 2026-06-07

### Added
- AI-агент на .NET 8 с локальной LLM (Ollama + qwen2.5-coder:7b-instruct)
- 4 агента: OrchestratorAgent, CodeEditorAgent, DocumentationAgent, ContextAgent
- 7 инструментов: read_file, write_file, project_structure, read_documentation, update_documentation, run_shell_command, git_diff
- Полный цикл оркестрации: Explore -> Read -> Write -> Validate -> Document -> Report
- Автоматическая индексация документации через Qdrant (векторный поиск)
- Семантический поиск по документации (ReadDocumentationTool)
- Обновление документации с автореиндексацией (UpdateDocumentationTool)
- Валидация изменений через dotnet build с ретраями (до 3 попыток)
- Контекстная память проекта (ContextAgent + ProjectContext)
- Форматированные отчёты (ReportService)
- Метрики качества (MetricsService)
- CLI-команды (System.CommandLine + Spectre.Console)
- Web API (ASP.NET Core): set-project, request, status, history
- Swagger/OpenAPI документация
- SignalR хаб для real-time обновлений
- Web UI (Razor Pages) с тёмной темой
- Тестовый проект-песочница (SandboxApp)
- 30 юнит-тестов (xUnit + Moq)
- Docker-контейнеризация (Dockerfile + Docker Compose)
- CI/CD пайплайн (GitHub Actions)
- Полная документация (README.md, USER_GUIDE.md, CONTRIBUTING.md)

### Technical Details
- .NET 8, C# 12
- Ollama (локальная LLM)
- Qdrant (векторная БД)
- Polly (ретраи)
- CliWrap (консольные команды)
- LibGit2Sharp (работа с Git)
- System.CommandLine + Spectre.Console (CLI)
- Swashbuckle.AspNetCore (Swagger)
- SignalR (real-time)
- xUnit + Moq (тестирование)