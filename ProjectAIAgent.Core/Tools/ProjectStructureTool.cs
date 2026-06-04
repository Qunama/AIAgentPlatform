// ProjectAIAgent.Core/Tools/ProjectStructureTool.cs
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Core.Tools;

[AgentTool("project_structure", "Analyzes the project directory structure. Returns a tree of files and directories.")]
public class ProjectStructureTool : IAgentTool
{
    private readonly ILogger<ProjectStructureTool> _logger;
    private readonly string _projectRoot;
    
    public string ToolName => "project_structure";
    public string Description => "Scans the project directory and returns its structure. " +
        "Useful for understanding the layout of the project before making changes.";
    public string ParametersSchema => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            path = new
            {
                type = "string",
                description = "Directory to scan (default: project root)"
            },
            max_depth = new
            {
                type = "integer",
                description = "Maximum directory depth to scan (default: 5)"
            }
        },
        required = Array.Empty<string>()
    });
    
    public ProjectStructureTool(ILogger<ProjectStructureTool> logger, IOptions<AgentOptions> options)
    {
        _logger = logger;
        _projectRoot = options.Value.ProjectPath;
    }
    
    public Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var root = parameters.TryGetValue("path", out var p) && p is string path
            ? GetFullPath(path)
            : (_projectRoot ?? Directory.GetCurrentDirectory());
            
        var maxDepth = parameters.TryGetValue("max_depth", out var d) && d is int depth
            ? depth : 5;
        
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Project structure for: {root}");
            sb.AppendLine("---");
            
            BuildTree(root, "", 0, maxDepth, sb);
            
            var output = sb.ToString();
            _logger.LogInformation("Scanned project structure at {Root}, depth {Depth}", root, maxDepth);
            
            return Task.FromResult(ToolResult.Ok(output, new Dictionary<string, object>
            {
                ["root"] = root,
                ["max_depth"] = maxDepth
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan project structure");
            return Task.FromResult(ToolResult.Fail($"Failed to scan: {ex.Message}"));
        }
    }
    
    private void BuildTree(string dir, string indent, int currentDepth, int maxDepth, StringBuilder sb)
    {
        if (currentDepth > maxDepth)
        {
            sb.AppendLine($"{indent}...");
            return;
        }
        
        try
        {
            // Сортируем: сначала директории, потом файлы
            var entries = Directory.GetFileSystemEntries(dir)
                .OrderBy(e => (File.GetAttributes(e) & FileAttributes.Directory) == 0)
                .ThenBy(e => Path.GetFileName(e));
            
            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                var isDir = (File.GetAttributes(entry) & FileAttributes.Directory) != 0;
                
                // Исключаем служебные директории
                if (isDir && (name == "bin" || name == "obj" || name == ".git" || name == "node_modules"))
                {
                    sb.AppendLine($"{indent}├── {name}/ [hidden]");
                    continue;
                }
                
                sb.Append(isDir ? $"{indent}├── {name}/" : $"{indent}├── {name}");
                
                if (!isDir)
                {
                    try
                    {
                        var info = new FileInfo(entry);
                        sb.Append($" ({FormatSize(info.Length)})");
                    }
                    catch { /* ignore */ }
                }
                
                sb.AppendLine();
                
                if (isDir)
                {
                    BuildTree(entry, indent + "│   ", currentDepth + 1, maxDepth, sb);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            sb.AppendLine($"{indent}[access denied]");
        }
    }
    
    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
    };
    
    private string GetFullPath(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        var root = _projectRoot ?? Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(root, path));
    }
}