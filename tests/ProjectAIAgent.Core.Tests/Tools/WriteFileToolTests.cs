// tests/ProjectAIAgent.Core.Tests/Tools/WriteFileToolTests.cs
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectAIAgent.Core.Tools;

namespace ProjectAIAgent.Core.Tests.Tools;

public class WriteFileToolTests
{
    [Fact]
    public async Task ExecuteAsync_WritesFile_Successfully()
    {
        // Arrange
        var logger = Mock.Of<ILogger<WriteFileTool>>();
        var tool = new WriteFileTool(logger);
        var tempFile = Path.GetTempFileName();
        
        try
        {
            // Act
            var result = await tool.ExecuteAsync(new Dictionary<string, object>
            {
                ["path"] = tempFile,
                ["content"] = "new content"
            });
            
            // Assert
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
        // Arrange
        var logger = Mock.Of<ILogger<WriteFileTool>>();
        var tool = new WriteFileTool(logger);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempFile = Path.Combine(tempDir, "subdir", "test.txt");
        
        try
        {
            // Act
            var result = await tool.ExecuteAsync(new Dictionary<string, object>
            {
                ["path"] = tempFile,
                ["content"] = "content in nested dir"
            });
            
            // Assert
            Assert.True(result.Success);
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}