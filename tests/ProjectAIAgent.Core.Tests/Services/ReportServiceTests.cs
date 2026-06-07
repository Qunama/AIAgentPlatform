// tests/ProjectAIAgent.Core.Tests/Services/ReportServiceTests.cs
using Xunit;
using ProjectAIAgent.Core.Services;

namespace ProjectAIAgent.Core.Tests.Services;

public class ReportServiceTests
{
    private readonly ReportService _reportService = new();

    [Fact]
    public void GenerateReport_Success_ContainsAllSections()
    {
        var data = new ReportData
        {
            UserRequest = "Тестовый запрос",
            Message = "Всё готово",
            Success = true,
            ModifiedFiles = new List<string> { "file1.cs", "file2.cs" },
            ValidationResult = "✅ Сборка успешна",
            ToolsUsed = new List<string> { "read_file", "write_file" },
            Duration = TimeSpan.FromSeconds(3.5),
            LlmCallCount = 4,
            ToolCallCount = 2
        };

        var report = _reportService.GenerateReport(data);

        Assert.Contains("Тестовый запрос", report);
        Assert.Contains("Всё готово", report);
        Assert.Contains("УСПЕШНО", report);
        Assert.Contains("file1.cs", report);
        Assert.Contains("file2.cs", report);
        Assert.Contains("Сборка успешна", report);
        Assert.Contains("read_file", report);
        Assert.Contains("write_file", report);
    }

    [Fact]
    public void GenerateReport_Failure_ContainsErrors()
    {
        var data = new ReportData
        {
            UserRequest = "Запрос",
            Message = "Ошибка",
            Success = false,
            Errors = new List<string> { "Ошибка 1", "Ошибка 2" },
            Duration = TimeSpan.FromSeconds(1),
            LlmCallCount = 1,
            ToolCallCount = 0
        };

        var report = _reportService.GenerateReport(data);

        Assert.Contains("С ОШИБКАМИ", report);
        Assert.Contains("Ошибка 1", report);
        Assert.Contains("Ошибка 2", report);
    }

    [Fact]
    public void GenerateReport_EmptyData_DoesNotThrow()
    {
        var data = new ReportData
        {
            UserRequest = "",
            Message = "",
            Duration = TimeSpan.Zero
        };

        var report = _reportService.GenerateReport(data);
        Assert.NotNull(report);
    }

    [Fact]
    public void GenerateShortReport_Success_ReturnsOneLiner()
    {
        var data = new ReportData
        {
            Message = "Готово",
            Success = true,
            ModifiedFiles = new List<string> { "a.cs", "b.cs" }
        };

        var report = _reportService.GenerateShortReport(data);

        Assert.Contains("✅", report);
        Assert.Contains("Готово", report);
        Assert.Contains("2", report);
    }

    [Fact]
    public void GenerateShortReport_Failure_ReturnsOneLiner()
    {
        var data = new ReportData
        {
            Message = "Провал",
            Success = false
        };

        var report = _reportService.GenerateShortReport(data);

        Assert.Contains("❌", report);
        Assert.Contains("Провал", report);
    }
}