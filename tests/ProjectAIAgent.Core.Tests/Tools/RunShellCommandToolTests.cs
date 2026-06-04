// tests/ProjectAIAgent.Core.Tests/Tools/RunShellCommandToolTests.cs
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProjectAIAgent.Core.Services;
using ProjectAIAgent.Core.Tools;

namespace ProjectAIAgent.Core.Tests.Tools;

public class RunShellCommandToolTests
{
    private readonly IOptions<AgentOptions> _agentOptions;

    public RunShellCommandToolTests()
    {
        var options = new AgentOptions { ProjectPath = Directory.GetCurrentDirectory() };
        _agentOptions = Options.Create(options);
    }

    [Fact]
    public async Task ExecuteAsync_AllowedCommand_Succeeds()
    {
        var logger = Mock.Of<ILogger<RunShellCommandTool>>();
        var tool = new RunShellCommandTool(logger, _agentOptions);

        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["command"] = "echo hello"
        });

        Assert.True(result.Success);
        Assert.Contains("hello", result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_DisallowedCommand_ReturnsError()
    {
        var logger = Mock.Of<ILogger<RunShellCommandTool>>();
        var tool = new RunShellCommandTool(logger, _agentOptions);

        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["command"] = "rm -rf /"
        });

        Assert.False(result.Success);
        Assert.Contains("not allowed", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCommand_ReturnsError()
    {
        var logger = Mock.Of<ILogger<RunShellCommandTool>>();
        var tool = new RunShellCommandTool(logger, _agentOptions);

        var result = await tool.ExecuteAsync(new Dictionary<string, object>());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidCommand_ReturnsError()
    {
        var logger = Mock.Of<ILogger<RunShellCommandTool>>();
        var tool = new RunShellCommandTool(logger, _agentOptions);

        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["command"] = "nonexistent_command_xyz"
        });

        Assert.False(result.Success);
    }
}