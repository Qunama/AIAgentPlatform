// ProjectAIAgent.Core/Agents/CodeEditorAgent.cs
using Microsoft.Extensions.Logging;
using ProjectAIAgent.Core.Tools;

namespace ProjectAIAgent.Core.Agents;

/// <summary>
/// Специализируется на чтении и изменении исходного кода проекта.
/// Инструменты: ReadFile, WriteFile, SearchCodebase, GitDiff.
/// </summary>
public class CodeEditorAgent : BaseAgent
{
    public override string Name => "CodeEditor";
    public override string Role => "CodeEditor";
    protected override string PromptResourceName => "code-editor.txt";
    
    public CodeEditorAgent(
        ILogger<CodeEditorAgent> logger,
        IServiceProvider serviceProvider) 
        : base(logger, serviceProvider)
    {
    }
    
    public async Task<string> ReadFileAsync(string path)
    {
        var tool = GetTool<ReadFileTool>();
        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["path"] = path
        });
        return result.Output;
    }
    
    public async Task<string> WriteFileAsync(string path, string content)
    {
        var tool = GetTool<WriteFileTool>();
        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["path"] = path,
            ["content"] = content
        });
        return result.Success ? "File written successfully" : result.Error!;
    }
}