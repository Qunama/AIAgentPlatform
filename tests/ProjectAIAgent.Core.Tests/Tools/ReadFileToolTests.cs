// tests/ProjectAIAgent.Core.Tests/Tools/ReadFileToolTests.cs
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using ProjectAIAgent.Core.Tools;

namespace ProjectAIAgent.Core.Tests.Tools;

public class ReadFileToolTests
{
    [Fact]
    public async Task ExecuteAsync_ValidFile_ReturnsContent()
    {
        // Arrange
        var logger = Mock.Of<ILogger<ReadFileTool>>();
        var tool = new ReadFileTool(logger);
        
        // Создаём временный файл
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "test content");
        
        try
        {
            // Act
            var result = await tool.ExecuteAsync(new Dictionary<string, object>
            {
                ["path"] = tempFile
            });
            
            // Assert
            Assert.True(result.Success);
            Assert.Equal("test content", result.Output);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
    
    [Fact]
    public async Task ExecuteAsync_FileNotFound_ReturnsError()
    {
        // Arrange
        var logger = Mock.Of<ILogger<ReadFileTool>>();
        var tool = new ReadFileTool(logger);
        
        // Act
        var result = await tool.ExecuteAsync(new Dictionary<string, object>
        {
            ["path"] = "/nonexistent/file.txt"
        });
        
        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error);
    }
    
    [Fact]
    public async Task ExecuteAsync_MissingPath_ReturnsError()
    {
        // Arrange
        var logger = Mock.Of<ILogger<ReadFileTool>>();
        var tool = new ReadFileTool(logger);
        
        // Act
        var result = await tool.ExecuteAsync(new Dictionary<string, object>());
        
        // Assert
        Assert.False(result.Success);
    }
}