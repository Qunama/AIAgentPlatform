// ProjectAIAgent.Core/Services/BuildValidationService.cs
using System.Text;
using CliWrap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Сервис валидации изменений через dotnet build.
/// Запускает сборку, анализирует ошибки, управляет повторными попытками.
/// </summary>
public class BuildValidationService
{
    private readonly IOptions<AgentOptions> _agentOptions;
    private readonly ILogger<BuildValidationService> _logger;

    public int MaxBuildAttempts => _agentOptions.Value.MaxRetries;
    public List<BuildAttempt> Attempts { get; } = new();

    public BuildValidationService(
        IOptions<AgentOptions> agentOptions,
        ILogger<BuildValidationService> logger)
    {
        _agentOptions = agentOptions;
        _logger = logger;
    }

    public async Task<BuildResult> BuildAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var attempt = new BuildAttempt
        {
            AttemptNumber = Attempts.Count + 1,
            StartedAt = DateTime.UtcNow
        };

        _logger.LogInformation(
            "Build attempt {Attempt}/{Max}: {Path}",
            attempt.AttemptNumber, MaxBuildAttempts, projectPath);

        try
        {
            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            var result = await Cli.Wrap("dotnet")
                .WithArguments("build")
                .WithWorkingDirectory(projectPath)
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(cancellationToken);

            attempt.CompletedAt = DateTime.UtcNow;
            attempt.ExitCode = result.ExitCode;
            attempt.StdOut = stdOut.ToString();
            attempt.StdErr = stdErr.ToString();
            attempt.Success = result.ExitCode == 0;

            if (attempt.Success)
            {
                _logger.LogInformation(
                    "Build succeeded (attempt {Attempt}, {Time}ms)",
                    attempt.AttemptNumber, result.RunTime.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "Build failed (attempt {Attempt}, exit code {ExitCode})",
                    attempt.AttemptNumber, result.ExitCode);
            }

            Attempts.Add(attempt);

            return new BuildResult
            {
                Success = attempt.Success,
                ExitCode = result.ExitCode,
                Output = stdOut.ToString(),
                Errors = stdErr.ToString(),
                AttemptNumber = attempt.AttemptNumber,
                RemainingAttempts = MaxBuildAttempts - attempt.AttemptNumber,
                ShouldRetry = !attempt.Success && attempt.AttemptNumber < MaxBuildAttempts,
                FormattedErrors = ExtractBuildErrors(stdErr.ToString())
            };
        }
        catch (Exception ex)
        {
            attempt.CompletedAt = DateTime.UtcNow;
            attempt.Success = false;
            attempt.StdErr = ex.Message;
            Attempts.Add(attempt);

            _logger.LogError(ex, "Build execution failed");

            return new BuildResult
            {
                Success = false,
                ExitCode = -1,
                Output = string.Empty,
                Errors = ex.Message,
                AttemptNumber = attempt.AttemptNumber,
                RemainingAttempts = MaxBuildAttempts - attempt.AttemptNumber,
                ShouldRetry = attempt.AttemptNumber < MaxBuildAttempts,
                FormattedErrors = $"Build execution error: {ex.Message}"
            };
        }
    }

    private static string ExtractBuildErrors(string stdErr)
    {
        if (string.IsNullOrWhiteSpace(stdErr))
            return "No error details available.";

        var lines = stdErr.Split('\n');
        var errorLines = lines
            .Where(l => l.Contains("error CS") || l.Contains("error MSB"))
            .Take(10)
            .ToList();

        if (errorLines.Count == 0)
        {
            errorLines = lines.TakeLast(10).ToList();
        }

        return string.Join("\n", errorLines);
    }

    /// <summary>
    /// Извлекает пути к файлам с ошибками из вывода сборки.
    /// </summary>
    public static List<string> ExtractFailedFiles(string stdErr)
    {
        var files = new List<string>();
        if (string.IsNullOrWhiteSpace(stdErr)) return files;

        var matches = Regex.Matches(stdErr, @"([^\s(]+\.cs)\(\d+");
        foreach (Match match in matches)
        {
            var file = match.Groups[1].Value;
            if (!files.Contains(file)) files.Add(file);
        }
        return files;
    }

    public void Reset()
    {
        Attempts.Clear();
        _logger.LogDebug("Build validation history reset");
    }
}

public class BuildResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Errors { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public int RemainingAttempts { get; set; }
    public bool ShouldRetry { get; set; }
    public string FormattedErrors { get; set; } = string.Empty;

    public string GetSummary()
    {
        if (Success)
            return $"✅ Build successful (attempt {AttemptNumber})";
        else if (ShouldRetry)
            return $"❌ Build failed (attempt {AttemptNumber}). {RemainingAttempts} attempts remaining.\n\nErrors:\n{FormattedErrors}";
        else
            return $"❌ Build failed after {AttemptNumber} attempts. All retries exhausted.\n\nErrors:\n{FormattedErrors}";
    }
}

public class BuildAttempt
{
    public int AttemptNumber { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ExitCode { get; set; }
    public bool Success { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
}