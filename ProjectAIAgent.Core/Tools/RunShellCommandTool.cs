// ProjectAIAgent.Core/Tools/RunShellCommandTool.cs
using System.Text;
using System.Text.Json;
using CliWrap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Core.Tools;

[AgentTool("run_shell_command", "Executes a shell command in the project directory. " +
    "Useful for running dotnet build, dotnet test, and other CLI commands. " +
    "Returns stdout, stderr, and exit code.")]
public class RunShellCommandTool : IAgentTool
{
    private readonly ILogger<RunShellCommandTool> _logger;
    private readonly string _projectRoot;

    // Команды, которые разрешено выполнять (безопасность)
    private static readonly HashSet<string> AllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet", "git", "npm", "docker", "echo", "dir", "ls", "cat", "type"
    };

    public string ToolName => "run_shell_command";

    public string Description => "Executes a shell command and returns its output. " +
        "Supports: dotnet build, dotnet test, git status, and other safe CLI commands. " +
        "Returns stdout, stderr, and exit code.";

    public string ParametersSchema => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            command = new
            {
                type = "string",
                description = "The command to execute (e.g., 'dotnet build', 'dotnet test')"
            },
            working_directory = new
            {
                type = "string",
                description = "Working directory for the command. Default: project root. Use '.' for current."
            },
            timeout_seconds = new
            {
                type = "integer",
                description = "Maximum execution time in seconds (default: 120)"
            }
        },
        required = new[] { "command" }
    });

    public RunShellCommandTool(ILogger<RunShellCommandTool> logger, IOptions<AgentOptions> options)
    {
        _logger = logger;
        _projectRoot = options.Value.ProjectPath;
    }

    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("command", out var cmdObj) || cmdObj is not string command)
        {
            return ToolResult.Fail("Parameter 'command' is required and must be a string");
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return ToolResult.Fail("Parameter 'command' cannot be empty");
        }

        // Извлекаем имя программы (первое слово команды)
        var programName = command.Split(' ')[0];

        // Проверяем, что команда разрешена
        if (!AllowedCommands.Contains(programName))
        {
            return ToolResult.Fail(
                $"Command '{programName}' is not allowed. Allowed commands: {string.Join(", ", AllowedCommands)}");
        }

        // Рабочая директория
        var workingDir = _projectRoot ?? Directory.GetCurrentDirectory();
        if (parameters.TryGetValue("working_directory", out var wdObj) && wdObj is string wd && !string.IsNullOrWhiteSpace(wd))
        {
            workingDir = Path.IsPathRooted(wd) ? wd : Path.GetFullPath(Path.Combine(workingDir, wd));
        }

        if (!Directory.Exists(workingDir))
        {
            return ToolResult.Fail($"Working directory not found: {workingDir}");
        }

        // Таймаут
        var timeoutSeconds = 120;
        if (parameters.TryGetValue("timeout_seconds", out var tsObj))
        {
            timeoutSeconds = tsObj switch
            {
                int i => Math.Clamp(i, 10, 600),
                string s when int.TryParse(s, out var parsed) => Math.Clamp(parsed, 10, 600),
                _ => 120
            };
        }

        try
        {
            _logger.LogInformation("Executing command: '{Command}' in {WorkingDir}", command, workingDir);

            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            var args = ExtractArguments(command);

            var result = await Cli.Wrap(programName)
                .WithArguments(args)
                .WithWorkingDirectory(workingDir)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
                .WithValidation(CommandResultValidation.None) // Не падаем при ненулевом коде выхода
                .ExecuteAsync();

            var metadata = new Dictionary<string, object>
            {
                ["command"] = command,
                ["working_directory"] = workingDir,
                ["exit_code"] = result.ExitCode,
                ["run_time_ms"] = result.RunTime.TotalMilliseconds,
                ["success"] = result.ExitCode == 0
            };

            if (result.ExitCode == 0)
            {
                _logger.LogInformation(
                    "Command '{Command}' completed successfully in {Time}ms",
                    command, result.RunTime.TotalMilliseconds);

                return ToolResult.Ok(stdOut.ToString(), metadata);
            }
            else
            {
                var errorOutput = stdErr.Length > 0 ? stdErr.ToString() : stdOut.ToString();

                _logger.LogWarning(
                    "Command '{Command}' failed with exit code {ExitCode}",
                    command, result.ExitCode);

                return ToolResult.Fail(
                    $"Command failed with exit code {result.ExitCode}.\n\nOutput:\n{errorOutput}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command '{Command}'", command);
            return ToolResult.Fail($"Failed to execute command: {ex.Message}");
        }
    }

    /// <summary>
    /// Извлекает аргументы из полной строки команды (всё после первого пробела).
    /// </summary>
    private static string ExtractArguments(string fullCommand)
    {
        var spaceIndex = fullCommand.IndexOf(' ');
        return spaceIndex > 0 ? fullCommand[(spaceIndex + 1)..] : string.Empty;
    }
}