// tests/ProjectAIAgent.Core.Tests/Services/LlmResponseParserTests.cs
using Xunit;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Core.Tests.Services;

public class LlmResponseParserTests
{
    [Fact]
    public void ExtractCodeBlock_ValidMarkdown_ReturnsCode()
    {
        var codeBlock = "```csharp\npublic class Test { }\n```";
        var response = "Here is the code:\n" + codeBlock;

        var code = LlmResponseParser.ExtractCodeBlock(response);

        Assert.Equal("public class Test { }", code);
    }

    [Fact]
    public void ExtractCodeBlock_NoMarkdown_ReturnsEmpty()
    {
        var response = "Just plain text, no code here.";

        var code = LlmResponseParser.ExtractCodeBlock(response);

        Assert.Equal(string.Empty, code);
    }

    [Fact]
    public void ExtractCodeBlock_EmptyString_ReturnsEmpty()
    {
        var code = LlmResponseParser.ExtractCodeBlock(string.Empty);
        Assert.Equal(string.Empty, code);
    }

    [Fact]
    public void ExtractAllCodeBlocks_MultipleBlocks_ReturnsAll()
    {
        var block1 = "```csharp\nclass A { }\n```";
        var block2 = "```csharp\nclass B { }\n```";
        var response = block1 + "\nSome text\n" + block2;

        var blocks = LlmResponseParser.ExtractAllCodeBlocks(response);

        Assert.Equal(2, blocks.Count);
        Assert.Contains("class A { }", blocks[0]);
        Assert.Contains("class B { }", blocks[1]);
    }

    [Fact]
    public void ExtractActionPlan_ValidJson_ReturnsDictionary()
    {
        var json = "{\"action\":\"delegate\",\"tool\":\"read_file\",\"args\":{\"path\":\"test.cs\"}}";

        var plan = LlmResponseParser.ExtractActionPlan(json);

        Assert.NotNull(plan);
        Assert.Equal("delegate", plan["action"]);
        Assert.Equal("read_file", plan["tool"]);
    }

    [Fact]
    public void ExtractActionPlan_JsonInMarkdownBlock_ReturnsDictionary()
    {
        var response = "```json\n{\"action\":\"report\",\"message\":\"Done\"}\n```";

        var plan = LlmResponseParser.ExtractActionPlan(response);

        Assert.NotNull(plan);
        Assert.Equal("report", plan["action"]);
    }

    [Fact]
    public void ExtractActionPlan_JsonWithCyrillic_ReturnsDictionary()
    {
        var json = "{\"action\":\"report\",\"message\":\"Структура проекта показана\"}";

        var plan = LlmResponseParser.ExtractActionPlan(json);

        Assert.NotNull(plan);
        Assert.Equal("Структура проекта показана", plan["message"]);
    }

    [Fact]
    public void ExtractActionPlan_InvalidJson_ReturnsNull()
    {
        var response = "Not a JSON at all";

        var plan = LlmResponseParser.ExtractActionPlan(response);

        Assert.Null(plan);
    }

    [Fact]
    public void ExtractJson_PureJson_ReturnsJsonString()
    {
        var json = "{\"key\":\"value\"}";

        var result = LlmResponseParser.ExtractJson(json);

        Assert.Equal(json, result);
    }

    [Fact]
    public void ContainsCode_HasCodeBlock_ReturnsTrue()
    {
        var response = "```csharp\nvar x = 1;\n```";

        Assert.True(LlmResponseParser.ContainsCode(response));
    }

    [Fact]
    public void ContainsCode_NoCodeBlock_ReturnsFalse()
    {
        Assert.False(LlmResponseParser.ContainsCode("Plain text"));
    }

    [Fact]
    public void ExtractTextWithoutCode_RemovesCodeBlock()
    {
        var response = "Before\n```csharp\ncode\n```\nAfter";

        var text = LlmResponseParser.ExtractTextWithoutCode(response);

        Assert.Contains("Before", text);
        Assert.Contains("After", text);
        Assert.DoesNotContain("code", text);
    }
}