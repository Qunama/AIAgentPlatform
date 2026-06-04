// tests/ProjectAIAgent.Core.Tests/Tools/ReadFileToolTests.cs
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProjectAIAgent.Core.Services;
using ProjectAIAgent.Core.Tools;

namespace ProjectAIAgent.Core.Tests.Tools;

public class ReadFileToolTests
{
    private readonly IOptions<AgentOptions> _agentOptions;

    public ReadFileToolTests()
    {
        var options = new AgentOptions { ProjectPath = Path.GetTempPath() };
        _agentOptions = Options.Create(options);
    }

    [Fact]
    public async Task ExecuteAsync_ValidFile_ReturnsContent()
    {
        var logger = Mock.Of<ILogger<ReadFileTool>>();
        var tool = new ReadFileTool(logger, _agentOptions);
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "test content");

        try
        {
            var result = await tool.ExecuteAsync(new Dictionary<string, object>
            {
                ["path"] = tempFile
            });

            Assert.True(result.Success);
            Assert.Equal("test content", result.Output);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsError()
    {
        var logger = Mock.Of<ILogger<ReadFileTool>>();
        var tool = new ReadFileTool(logger, _agentOptions);
        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["path"] = "/nonexistent/file.txt"
        });

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_MissingPath_ReturnsError()
    {
        var logger = Mock.Of<ILogger<ReadFileTool>>();
        var tool = new ReadFileTool(logger, _agentOptions);
        var result = await tool.ExecuteAsync(new Dictionary<string, object>());

        Assert.False(result.Success);
    }
}