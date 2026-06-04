// ProjectAIAgent.Core/Tools/GitDiffTool.cs
using System.Text;
using System.Text.Json;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Core.Tools;

[AgentTool("git_diff", "Shows Git changes in the project. " +
    "Can display status (modified/new/deleted files), diff of unstaged changes, and recent commit history.")]
public class GitDiffTool : IAgentTool
{
    private readonly ILogger<GitDiffTool> _logger;
    private readonly string _projectRoot;

    public string ToolName => "git_diff";

    public string Description => "Shows Git changes in the project repository. " +
        "Operations: 'status' — list modified/new/deleted files; " +
        "'diff' — show unstaged changes; " +
        "'log' — show recent commit history.";

    public string ParametersSchema => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            operation = new
            {
                type = "string",
                description = "Git operation to perform: 'status', 'diff', or 'log'"
            },
            file_path = new
            {
                type = "string",
                description = "Specific file to show diff for (optional, only for 'diff' operation)"
            },
            max_commits = new
            {
                type = "integer",
                description = "Maximum number of commits to show for 'log' operation (default: 10)"
            }
        },
        required = new[] { "operation" }
    });

    public GitDiffTool(ILogger<GitDiffTool> logger, IOptions<AgentOptions> options)
    {
        _logger = logger;
        _projectRoot = options.Value.ProjectPath;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("operation", out var opObj) || opObj is not string operation)
        {
            return Task.FromResult(ToolResult.Fail("Parameter 'operation' is required. Valid values: 'status', 'diff', 'log'"));
        }

        try
        {
            var repoPath = FindGitRepository();
            if (repoPath == null)
            {
                return Task.FromResult(ToolResult.Fail(
                    "No Git repository found in project path or its parent directories. " +
                    "Ensure the project is initialized with Git."));
            }

            using var repo = new Repository(repoPath);

            return operation.ToLowerInvariant() switch
            {
                "status" => Task.FromResult(GetStatus(repo)),
                "diff" => Task.FromResult(GetDiff(repo, parameters)),
                "log" => Task.FromResult(GetLog(repo, parameters)),
                _ => Task.FromResult(ToolResult.Fail(
                    $"Unknown operation: '{operation}'. Valid values: 'status', 'diff', 'log'"))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Git operation '{Operation}'", operation);
            return Task.FromResult(ToolResult.Fail($"Git operation failed: {ex.Message}"));
        }
    }

    private ToolResult GetStatus(Repository repo)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Git Status");
        sb.AppendLine("==========");

        var status = repo.RetrieveStatus();

        if (!status.Any())
        {
            sb.AppendLine("Working directory clean. No changes.");
        }
        else
        {
            // Группируем по состоянию
            var modified = status.Where(s => s.State == FileStatus.ModifiedInWorkdir).ToList();
            var added = status.Where(s => s.State == FileStatus.NewInWorkdir).ToList();
            var deleted = status.Where(s => s.State == FileStatus.DeletedFromWorkdir).ToList();
            var renamed = status.Where(s => s.State == FileStatus.RenamedInWorkdir).ToList();
            var untracked = status.Where(s => s.State == FileStatus.NewInWorkdir && !added.Contains(s)).ToList();
            var staged = status.Where(s =>
                s.State == FileStatus.ModifiedInIndex ||
                s.State == FileStatus.NewInIndex ||
                s.State == FileStatus.DeletedFromIndex).ToList();

            if (staged.Any())
            {
                sb.AppendLine($"Staged ({staged.Count}):");
                foreach (var file in staged)
                    sb.AppendLine($"  S  {file.FilePath}");
            }

            if (modified.Any())
            {
                sb.AppendLine($"Modified ({modified.Count}):");
                foreach (var file in modified)
                    sb.AppendLine($"  M  {file.FilePath}");
            }

            if (added.Any())
            {
                sb.AppendLine($"Added ({added.Count}):");
                foreach (var file in added)
                    sb.AppendLine($"  A  {file.FilePath}");
            }

            if (deleted.Any())
            {
                sb.AppendLine($"Deleted ({deleted.Count}):");
                foreach (var file in deleted)
                    sb.AppendLine($"  D  {file.FilePath}");
            }

            if (renamed.Any())
            {
                sb.AppendLine($"Renamed ({renamed.Count}):");
                foreach (var file in renamed)
                    sb.AppendLine($"  R  {file.FilePath}");
            }

            if (untracked.Any())
            {
                sb.AppendLine($"Untracked ({untracked.Count}):");
                foreach (var file in untracked)
                    sb.AppendLine($"  ?  {file.FilePath}");
            }

            sb.AppendLine();
            sb.AppendLine($"Total changes: {status.Count()}");
        }

        _logger.LogInformation("Git status: {Count} changes", status.Count());

        return ToolResult.Ok(sb.ToString(), new Dictionary<string, object>
        {
            ["total_changes"] = status.Count(),
            ["modified_count"] = status.Count(s => s.State == FileStatus.ModifiedInWorkdir),
            ["added_count"] = status.Count(s => s.State == FileStatus.NewInWorkdir),
            ["deleted_count"] = status.Count(s => s.State == FileStatus.DeletedFromWorkdir),
            ["untracked_count"] = status.Count(s => s.State == FileStatus.NewInWorkdir)
        });
    }

    private ToolResult GetDiff(Repository repo, Dictionary<string, object> parameters)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Git Diff (unstaged changes)");
        sb.AppendLine("===========================");

        // Если указан конкретный файл — показываем diff только для него
        if (parameters.TryGetValue("file_path", out var fpObj) && fpObj is string filePath && !string.IsNullOrWhiteSpace(filePath))
        {
            var changes = repo.Diff.Compare<TreeChanges>();
            var fileChanges = changes.Where(c =>
                c.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase) ||
                c.OldPath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

            if (!fileChanges.Any())
            {
                return ToolResult.Ok($"No changes found for file: {filePath}");
            }

            foreach (var change in fileChanges)
            {
                var patch = repo.Diff.Compare<Patch>(repo.Head.Tip.Tree, DiffTargets.WorkingDirectory, new[] { change.Path });
                sb.AppendLine($"--- {change.OldPath}");
                sb.AppendLine($"+++ {change.Path}");
                sb.AppendLine(patch.Content);
            }
        }
        else
        {
            // Показываем все изменения
            var patch = repo.Diff.Compare<Patch>(repo.Head.Tip.Tree, DiffTargets.WorkingDirectory);
            sb.AppendLine(patch.Content);

            if (string.IsNullOrWhiteSpace(patch.Content))
            {
                sb.AppendLine("No unstaged changes.");
            }
        }

        var output = sb.ToString();
        if (output.Length > 3000)
        {
            output = output[..3000] + $"\n... [truncated, total {output.Length} chars]";
        }

        _logger.LogInformation("Git diff generated ({Length} chars)", output.Length);

        return ToolResult.Ok(output, new Dictionary<string, object>
        {
            ["diff_length"] = output.Length,
            ["truncated"] = output.Length >= 3000
        });
    }

    private ToolResult GetLog(Repository repo, Dictionary<string, object> parameters)
    {
        var maxCommits = 10;
        if (parameters.TryGetValue("max_commits", out var mcObj))
        {
            maxCommits = mcObj switch
            {
                int i => Math.Clamp(i, 1, 50),
                string s when int.TryParse(s, out var parsed) => Math.Clamp(parsed, 1, 50),
                _ => 10
            };
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Git Log (last {maxCommits} commits)");
        sb.AppendLine("==============================");

        var commits = repo.Commits.Take(maxCommits);

        foreach (var commit in commits)
        {
            sb.AppendLine($"commit {commit.Sha[..7]}");
            sb.AppendLine($"Author: {commit.Author.Name} <{commit.Author.Email}>");
            sb.AppendLine($"Date:   {commit.Author.When:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"    {commit.MessageShort}");
            sb.AppendLine();
        }

        _logger.LogInformation("Git log: {Count} commits", commits.Count());

        return ToolResult.Ok(sb.ToString(), new Dictionary<string, object>
        {
            ["commits_shown"] = commits.Count(),
            ["total_commits"] = repo.Commits.Count()
        });
    }

    private string? FindGitRepository()
    {
        var path = _projectRoot ?? Directory.GetCurrentDirectory();

        while (!string.IsNullOrEmpty(path))
        {
            var gitDir = Path.Combine(path, ".git");
            if (Directory.Exists(gitDir) || File.Exists(gitDir))
            {
                return path;
            }

            var parent = Directory.GetParent(path);
            path = parent?.FullName ?? string.Empty;
        }

        return null;
    }
}