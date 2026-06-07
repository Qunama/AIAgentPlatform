# Руководство пользователя AIAgentPlatform

## Начало работы

### 1. Запуск через Docker (рекомендуемый)

```bash
cd local-infra
docker compose up -d
```

Убедитесь, что все контейнеры запущены:
```bash
docker ps
# Должны быть: ollama-server, qdrant-server, aiagent-platform
```

### 2. Загрузка модели

```bash
docker exec -it ollama-server ollama pull qwen2.5-coder:7b-instruct
docker compose restart agent
```

Проверка:
```bash
docker exec -it ollama-server ollama list
```

### 3. Запуск для разработки (без Docker для агента)

```bash
cd local-infra
docker compose up -d ollama qdrant
docker exec -it ollama-server ollama pull qwen2.5-coder:7b-instruct
cd ..
cd ProjectAIAgent.Host
dotnet run
```

### 4. Указание проекта

Перед использованием агента укажите путь к .NET проекту.

**Через CLI:**
```bash
dotnet run -- set-project "D:\\repos\\MyProject"
```

**Через API:**
```bash
curl -X POST http://localhost:5000/api/agent/set-project -H "Content-Type: application/json" -d '{"path":"D:\\repos\\MyProject"}'
```

**Через appsettings.json:**
```json
"Agent": {
    "ProjectPath": "D:\\repos\\MyProject"
}
```

**В Docker:** путь монтируется через volume в docker-compose.yml:
```yaml
volumes:
  - ../tests/ProjectAIAgent.Sandbox/SandboxApp:/app/project
```

### 5. Отправка запросов

**Интерактивный режим:**
```bash
dotnet run
# Выбрать A, ввести запрос
```

**CLI (одна команда):**
```bash
dotnet run -- request "Добавь метод для валидации email в UserService"
```

**Web UI:**
Откройте http://localhost:5000/, введите запрос, нажмите Отправить или Ctrl+Enter.

## Примеры запросов

### Изучение проекта
- "Покажи структуру проекта"
- "Прочитай файл Program.cs"
- "Найди документацию по аутентификации"

### Внесение изменений
- "Добавь метод Multiply в Calculator"
- "Исправь деление на ноль в Calculator.Divide"
- "Создай класс EmailValidator с методом IsValidEmail"

### Валидация
- Агент автоматически запускает `dotnet build` после каждого изменения
- При ошибке сборки — анализирует и исправляет (до 3 попыток)

### Работа с Git
- "Покажи изменения в Git"
- "Покажи историю последних 5 коммитов"

## Интерфейсы

### CLI-команды

| Команда | Описание |
|---------|----------|
| `start` | Запуск в интерактивном режиме |
| `set-project <path>` | Установка пути к проекту |
| `request <description>` | Отправка запроса |
| `status` | Статус агента и метрики |
| `history` | История взаимодействий |
| `docs query <query>` | Поиск по документации |

### Web API

| Метод | URL | Описание |
|-------|-----|----------|
| POST | /api/agent/set-project | Установка пути |
| POST | /api/agent/request | Запрос агенту |
| GET | /api/agent/status | Статус и метрики |
| GET | /api/agent/history | История |

### Web UI

http://localhost:5000/ — тёмная тема с логом в реальном времени через SignalR.

### Swagger

http://localhost:5000/swagger — интерактивная документация API.

## Сборка Docker-образа

Dockerfile находится в корне проекта. Сборка через Docker Compose:

```bash
cd local-infra
docker compose build agent
```

Или напрямую:

```bash
docker build -t aiagent-platform .
```

## Метрики

Метрики собираются автоматически и доступны через:
- CLI: `dotnet run -- status`
- API: `GET /api/agent/status`
- Web UI: панель статуса

Отслеживаемые метрики:
- Успешность запросов (%)
- Среднее время выполнения
- Количество вызовов LLM и инструментов
- Успешность сборок
- Топ используемых инструментов

## Устранение неполадок

### Ошибка "model not found"
```bash
docker exec -it ollama-server ollama pull qwen2.5-coder:7b-instruct
```

### Ошибка "The path is empty"
Укажите ProjectPath в appsettings.json или через команду set-project.

### Ошибка "No Git repository found"
Инициализируйте Git в целевом проекте: `git init`

### Docker не видит Dockerfile
Dockerfile должен находиться в корне проекта (рядом с .sln файлом), а не в папке ProjectAIAgent.Host.

### Медленная работа
- Модель qwen2.5-coder:7b-instruct требует ~8 ГБ RAM
- При нехватке ресурсов используйте более лёгкую модель
- Измените модель в appsettings.json (Ollama:Model)