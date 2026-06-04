// ProjectAIAgent.Core/Tools/WriteFileTool.cs
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Core.Tools;

[AgentTool("write_file", "Writes content to a file. Creates directories if needed.")]
public class WriteFileTool : IAgentTool
{
    private readonly ILogger<WriteFileTool> _logger;
    private readonly string _projectRoot;
    
    public string ToolName => "write_file";
    public string Description => "Writes content to a file. Creates parent directories if they don't exist. Overwrites existing files.";
    public string ParametersSchema => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            path = new
            {
                type = "string",
                description = "Path to the file to write (relative to project root or absolute)"
            },
            content = new
            {
                type = "string",
                description = "Content to write to the file"
            }
        },
        required = new[] { "path", "content" }
    });
    
    public WriteFileTool(ILogger<WriteFileTool> logger, IOptions<AgentOptions> options)
    {
        _logger = logger;
        _projectRoot = options.Value.ProjectPath;
    }
    
    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("path", out var pathObj) || pathObj is not string path)
            return ToolResult.Fail("Parameter 'path' is required");
            
        if (!parameters.TryGetValue("content", out var contentObj) || contentObj is not string content)
            return ToolResult.Fail("Parameter 'content' is required");
        
        try
        {
            var fullPath = GetFullPath(path);
            var dir = Path.GetDirectoryName(fullPath);
            
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                _logger.LogInformation("Created directory: {Dir}", dir);
            }
            
            var previousContent = File.Exists(fullPath) 
                ? await File.ReadAllTextAsync(fullPath) 
                : null;
            
            await File.WriteAllTextAsync(fullPath, content);
            
            _logger.LogInformation("Written {Path} ({Length} chars)", fullPath, content.Length);
            
            return ToolResult.Ok($"Successfully wrote {content.Length} characters to {path}", 
                new Dictionary<string, object>
                {
                    ["path"] = fullPath,
                    ["size_bytes"] = content.Length,
                    ["is_new_file"] = previousContent == null,
                    ["was_modified"] = previousContent != content
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write file {Path}", path);
            return ToolResult.Fail($"Failed to write file: {ex.Message}");
        }
    }
    
    private string GetFullPath(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        var root = _projectRoot ?? Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(root, path));
    }
}