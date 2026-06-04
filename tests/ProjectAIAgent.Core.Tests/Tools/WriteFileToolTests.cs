// tests/ProjectAIAgent.Core.Tests/Tools/WriteFileToolTests.cs
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProjectAIAgent.Core.Services;
using ProjectAIAgent.Core.Tools;

namespace ProjectAIAgent.Core.Tests.Tools;

public class WriteFileToolTests
{
    private readonly IOptions<AgentOptions> _agentOptions;

    public WriteFileToolTests()
    {
        var options = new AgentOptions { ProjectPath = Path.GetTempPath() };
        _agentOptions = Options.Create(options);
    }

    [Fact]
    public async Task ExecuteAsync_WritesFile_Successfully()
    {
        var logger = Mock.Of<ILogger<WriteFileTool>>();
        var tool = new WriteFileTool(logger, _agentOptions);
        var tempFile = Path.GetTempFileName();

        try
        {
            var result = await tool.ExecuteAsync(new Dictionary<string, object>
            {
                ["path"] = tempFile,
                ["content"] = "new content"
            });

            Assert.True(result.Success);
            var written = await File.ReadAllTextAsync(tempFile);
            Assert.Equal("new content", written);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_CreatesDirectory_IfNeeded()
    {
        var logger = Mock.Of<ILogger<WriteFileTool>>();
        var tool = new WriteFileTool(logger, _agentOptions);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempFile = Path.Combine(tempDir, "subdir", "test.txt");

        try
        {
            var result = await tool.ExecuteAsync(new Dictionary<string, object>
            {
                ["path"] = tempFile,
                ["content"] = "content in nested dir"
            });

            Assert.True(result.Success);
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}