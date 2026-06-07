// ProjectAIAgent.Core/Tools/FindUsagesTool.cs
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Services;
using ProjectAIAgent.Core.Models;

namespace ProjectAIAgent.Core.Tools;

[AgentTool("find_usages", "Finds all usages of a class, method, or property in the project source code. " +
    "Returns file paths and line numbers where the symbol is used.")]
public class FindUsagesTool : IAgentTool
{
    private readonly IOptions<AgentOptions> _agentOptions;
    private readonly ILogger<FindUsagesTool> _logger;

    public string ToolName => "find_usages";
    public string Description => "Finds all places in the codebase where a class, method, or property is used. " +
        "Use this BEFORE modifying a method to understand the impact of changes.";

    public string ParametersSchema => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            symbol = new
            {
                type = "string",
                description = "Class or method name to search for (e.g., 'UserService', 'CreateUser', 'IsValidEmail')"
            },
            file_pattern = new
            {
                type = "string",
                description = "Optional: limit search to specific file pattern (e.g., '*.cs', 'UserService.cs')"
            }
        },
        required = new[] { "symbol" }
    });

    public FindUsagesTool(IOptions<AgentOptions> agentOptions, ILogger<FindUsagesTool> logger)
    {
        _agentOptions = agentOptions;
        _logger = logger;
    }

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("symbol", out var symObj) || symObj is not string symbol)
            return Task.FromResult(ToolResult.Fail("Parameter 'symbol' is required"));

        if (string.IsNullOrWhiteSpace(symbol))
            return Task.FromResult(ToolResult.Fail("Parameter 'symbol' cannot be empty"));

        var filePattern = parameters.TryGetValue("file_pattern", out var fp) && fp is string p ? p : "*.cs";

        try
        {
            var projectPath = _agentOptions.Value.ProjectPath;
            if (string.IsNullOrWhiteSpace(projectPath))
                projectPath = Directory.GetCurrentDirectory();

            if (!Directory.Exists(projectPath))
                return Task.FromResult(ToolResult.Fail($"Project path not found: {projectPath}"));

            _logger.LogInformation("Finding usages of '{Symbol}' in {Path}", symbol, projectPath);

            var files = Directory.GetFiles(projectPath, filePattern, SearchOption.AllDirectories)
                .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\.git\\"))
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Usages of '{symbol}':");
            sb.AppendLine("========================");

            int totalUsages = 0;
            var results = new List<Dictionary<string, object>>();

            foreach (var file in files)
            {
                try
                {
                    var lines = File.ReadAllLines(file);
                    var fileUsages = new List<int>();

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].Contains(symbol))
                        {
                            fileUsages.Add(i + 1);
                            totalUsages++;
                        }
                    }

                    if (fileUsages.Count > 0)
                    {
                        var relativePath = Path.GetRelativePath(projectPath, file);
                        sb.AppendLine($"\n📁 {relativePath} ({fileUsages.Count} usages):");
                        foreach (var lineNum in fileUsages.Take(5))
                        {
                            sb.AppendLine($"   Line {lineNum}: {lines[lineNum - 1].Trim().Truncate(120)}");
                        }
                        if (fileUsages.Count > 5)
                            sb.AppendLine($"   ... and {fileUsages.Count - 5} more");

                        results.Add(new Dictionary<string, object>
                        {
                            ["file"] = relativePath,
                            ["count"] = fileUsages.Count,
                            ["lines"] = fileUsages.Take(5).ToList()
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read {File}", file);
                }
            }

            if (totalUsages == 0)
            {
                sb.AppendLine("\nNo usages found.");
            }
            else
            {
                sb.AppendLine($"\nTotal: {totalUsages} usages in {results.Count} files.");
            }

            return Task.FromResult(ToolResult.Ok(sb.ToString(), new Dictionary<string, object>
            {
                ["symbol"] = symbol,
                ["total_usages"] = totalUsages,
                ["files_scanned"] = files.Count,
                ["results"] = results
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find usages");
            return Task.FromResult(ToolResult.Fail($"Search failed: {ex.Message}"));
        }
    }
}