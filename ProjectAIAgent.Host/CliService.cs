// ProjectAIAgent.Host/CliService.cs
using System.CommandLine;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Agents;
using ProjectAIAgent.Core.Services;
using Spectre.Console;
using ProjectAIAgent.Core.Models;

namespace ProjectAIAgent.Host;

/// <summary>
/// Сервис для обработки CLI-команд. Использует System.CommandLine и Spectre.Console.
/// </summary>
public class CliService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<AgentOptions> _agentOptions;

    public CliService(IServiceProvider serviceProvider, IOptions<AgentOptions> agentOptions)
    {
        _serviceProvider = serviceProvider;
        _agentOptions = agentOptions;
    }

    /// <summary>
    /// Создаёт и настраивает корневую команду CLI.
    /// </summary>
    public RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("AI Agent Platform — AI-агент для работы с .NET проектами");

        // agent start
        var startCommand = new Command("start", "Запустить агента в интерактивном режиме");
        startCommand.SetHandler(HandleStart);
        rootCommand.AddCommand(startCommand);

        // agent set-project
        var pathArg = new Argument<string>("path", "Путь к .NET проекту");
        var setProjectCommand = new Command("set-project", "Установить путь к проекту")
        {
            pathArg
        };
        setProjectCommand.SetHandler(HandleSetProject, pathArg);
        rootCommand.AddCommand(setProjectCommand);

        // agent request
        var requestArg = new Argument<string>("description", "Описание задачи для агента");
        var requestCommand = new Command("request", "Отправить запрос агенту")
        {
            requestArg
        };
        requestCommand.SetHandler(HandleRequest, requestArg);
        rootCommand.AddCommand(requestCommand);

        // agent status
        var statusCommand = new Command("status", "Показать текущий статус агента");
        statusCommand.SetHandler(HandleStatus);
        rootCommand.AddCommand(statusCommand);

        // agent history
        var historyCommand = new Command("history", "Показать историю взаимодействий");
        historyCommand.SetHandler(HandleHistory);
        rootCommand.AddCommand(historyCommand);

        // agent docs query
        var queryArg = new Argument<string>("query", "Поисковый запрос");
        var docsCommand = new Command("docs", "Поиск по документации проекта")
        {
            queryArg
        };
        docsCommand.SetHandler(HandleDocsQuery, queryArg);
        rootCommand.AddCommand(docsCommand);

        return rootCommand;
    }

    private async Task HandleStart()
    {
        AnsiConsole.MarkupLine("[bold green]🚀 Запуск агента в интерактивном режиме...[/]");
        // Передаём управление AgentWorkerService
        var worker = _serviceProvider.GetRequiredService<AgentWorkerService>();
        await worker.StartAsync(CancellationToken.None);
    }

    private void HandleSetProject(string path)
    {
        if (!Directory.Exists(path))
        {
            AnsiConsole.MarkupLine($"[red]❌ Путь не существует: {path}[/]");
            return;
        }

        _agentOptions.Value.ProjectPath = path;
        var contextAgent = _serviceProvider.GetRequiredService<ContextAgent>();
        contextAgent.SetProjectPath(path);

        AnsiConsole.MarkupLine($"[green]✅ Путь к проекту установлен: {path}[/]");
    }

    private async Task HandleRequest(string description)
    {
        AnsiConsole.MarkupLine($"[bold]📝 Запрос:[/] {description}");
        AnsiConsole.MarkupLine("[grey]⏳ Обработка...[/]");

        try
        {
            var orchestrator = _serviceProvider.GetRequiredService<OrchestratorAgent>();
            var result = await orchestrator.ProcessRequestAsync(description);

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Panel(result)
            {
                Header = new PanelHeader("🤖 Ответ агента"),
                Border = BoxBorder.Rounded
            });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка: {ex.Message}[/]");
        }
    }

    private void HandleStatus()
    {
        var contextAgent = _serviceProvider.GetRequiredService<ContextAgent>();
        var context = contextAgent.GetContext();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Параметр")
            .AddColumn("Значение");

        table.AddRow("Путь к проекту", context.ProjectPath ?? "[grey]не задан[/]");
        table.AddRow("Фаза", context.CurrentPhase.ToString());
        table.AddRow("Изменённых файлов", context.ModifiedFiles.Count.ToString());
        table.AddRow("Ошибок в сессии", context.ErrorCount.ToString());
        table.AddRow("Записей в истории", context.History.Count.ToString());

        AnsiConsole.Write(table);
    }

    private void HandleHistory()
    {
        var contextAgent = _serviceProvider.GetRequiredService<ContextAgent>();
        var context = contextAgent.GetContext();

        if (context.History.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]История пуста[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Время")
            .AddColumn("Статус")
            .AddColumn("Запрос")
            .AddColumn("Ответ");

        foreach (var record in context.History.TakeLast(10))
        {
            var status = record.Success ? "[green]✅[/]" : "[red]❌[/]";
            table.AddRow(
                record.Timestamp.ToString("HH:mm:ss"),
                status,
                record.Request.Truncate(50),
                record.Response.Truncate(50));
        }

        AnsiConsole.Write(table);
    }

    private async Task HandleDocsQuery(string query)
    {
        AnsiConsole.MarkupLine($"[bold]📚 Поиск документации:[/] {query}");

        try
        {
            var toolRegistry = _serviceProvider.GetRequiredService<ProjectAIAgent.Core.Tools.ToolRegistry>();
            var result = await toolRegistry.ExecuteToolAsync("read_documentation", new Dictionary<string, object>
            {
                ["query"] = query,
                ["top_k"] = 5
            });

            if (result.Success)
            {
                AnsiConsole.Write(new Panel(result.Output)
                {
                    Header = new PanelHeader("📖 Результаты поиска"),
                    Border = BoxBorder.Rounded
                });
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]❌ {result.Error}[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Ошибка: {ex.Message}[/]");
        }
    }
}