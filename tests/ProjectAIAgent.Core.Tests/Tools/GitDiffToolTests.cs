// tests/ProjectAIAgent.Core.Tests/Tools/GitDiffToolTests.cs
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProjectAIAgent.Core.Services;
using ProjectAIAgent.Core.Tools;

namespace ProjectAIAgent.Core.Tests.Tools;

public class GitDiffToolTests
{
    private readonly IOptions<AgentOptions> _agentOptions;

    public GitDiffToolTests()
    {
        // Указываем корень репозитория AIAgentPlatform (там есть .git)
        var repoRoot = FindRepositoryRoot();
        var options = new AgentOptions { ProjectPath = repoRoot ?? Directory.GetCurrentDirectory() };
        _agentOptions = Options.Create(options);
    }

    [Fact]
    public void ExecuteAsync_Status_ReturnsOk()
    {
        var logger = Mock.Of<ILogger<GitDiffTool>>();
        var tool = new GitDiffTool(logger, _agentOptions);

        var result = tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["operation"] = "status"
        }).Result;

        Assert.True(result.Success);
    }

    [Fact]
    public void ExecuteAsync_Log_ReturnsCommits()
    {
        var logger = Mock.Of<ILogger<GitDiffTool>>();
        var tool = new GitDiffTool(logger, _agentOptions);

        var result = tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["operation"] = "log",
            ["max_commits"] = 3
        }).Result;

        Assert.True(result.Success);
    }

    [Fact]
    public void ExecuteAsync_UnknownOperation_ReturnsError()
    {
        var logger = Mock.Of<ILogger<GitDiffTool>>();
        var tool = new GitDiffTool(logger, _agentOptions);

        var result = tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["operation"] = "invalid_op"
        }).Result;

        Assert.False(result.Success);
    }

    [Fact]
    public void ExecuteAsync_MissingOperation_ReturnsError()
    {
        var logger = Mock.Of<ILogger<GitDiffTool>>();
        var tool = new GitDiffTool(logger, _agentOptions);

        var result = tool.ExecuteAsync(new Dictionary<string, object>()).Result;

        Assert.False(result.Success);
    }

    private static string? FindRepositoryRoot()
    {
        var path = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(path))
        {
            if (Directory.Exists(Path.Combine(path, ".git")))
                return path;
            var parent = Directory.GetParent(path);
            path = parent?.FullName ?? string.Empty;
        }
        return null;
    }
}