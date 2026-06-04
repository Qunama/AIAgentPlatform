// ProjectAIAgent.Core/Tools/ReadFileTool.cs
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Core.Tools;

[AgentTool("read_file", "Reads the content of a file at the specified path")]
public class ReadFileTool : IAgentTool
{
    private readonly ILogger<ReadFileTool> _logger;
    private readonly string _projectRoot;
    
    public string ToolName => "read_file";
    public string Description => "Reads the content of a file. Returns the file content as text.";
    public string ParametersSchema => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            path = new
            {
                type = "string",
                description = "Relative or absolute path to the file to read"
            }
        },
        required = new[] { "path" }
    });
    
    public ReadFileTool(ILogger<ReadFileTool> logger, IOptions<AgentOptions> options)
    {
        _logger = logger;
        _projectRoot = options.Value.ProjectPath;
    }
    
    public async Task<ToolResult> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("path", out var pathObj) || pathObj is not string path)
        {
            return ToolResult.Fail("Parameter 'path' is required and must be a string");
        }
        
        try
        {
            var fullPath = GetFullPath(path);
            
            if (!File.Exists(fullPath))
            {
                return ToolResult.Fail($"File not found: {fullPath}");
            }
            
            var content = await File.ReadAllTextAsync(fullPath);
            var fileInfo = new FileInfo(fullPath);
            
            _logger.LogInformation("Read {Path} ({Size} bytes)", fullPath, fileInfo.Length);
            
            return ToolResult.Ok(content, new Dictionary<string, object>
            {
                ["path"] = fullPath,
                ["size_bytes"] = fileInfo.Length,
                ["last_modified"] = fileInfo.LastWriteTimeUtc.ToString("O"),
                ["line_count"] = content.Count(c => c == '\n') + 1
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file {Path}", path);
            return ToolResult.Fail($"Failed to read file: {ex.Message}");
        }
    }
    
    private string GetFullPath(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        
        var root = _projectRoot ?? Directory.GetCurrentDirectory();
        return Path.GetFullPath(Path.Combine(root, path));
    }
}