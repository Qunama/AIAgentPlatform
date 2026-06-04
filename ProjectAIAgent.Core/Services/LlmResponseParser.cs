// ProjectAIAgent.Core/Services/LlmResponseParser.cs
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Статический хелпер для парсинга ответов LLM.
/// Извлекает код из Markdown-блоков, структурированные планы и чистый текст.
/// </summary>
public static class LlmResponseParser
{
    private static readonly Regex CodeBlockRegex = new(
        @"```(?:\w+)?\s*\n(.*?)\n```",
        RegexOptions.Singleline | RegexOptions.Compiled);

    // Ищем JSON-объект на верхнем уровне (не вложенный)
    private static readonly Regex JsonObjectRegex = new(
        @"\{[^{}]*\}",
        RegexOptions.Singleline | RegexOptions.Compiled);
    
    // Более сложный JSON с одним уровнем вложенности
    private static readonly Regex JsonNestedRegex = new(
        @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static string ExtractCodeBlock(string llmResponse)
    {
        if (string.IsNullOrWhiteSpace(llmResponse))
            return string.Empty;

        var match = CodeBlockRegex.Match(llmResponse);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    public static List<string> ExtractAllCodeBlocks(string llmResponse)
    {
        var blocks = new List<string>();
        if (string.IsNullOrWhiteSpace(llmResponse))
            return blocks;

        var matches = CodeBlockRegex.Matches(llmResponse);
        foreach (Match match in matches)
        {
            var code = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(code))
                blocks.Add(code);
        }
        return blocks;
    }

    /// <summary>
    /// Извлекает JSON из ответа LLM. 
    /// Пробует разные стратегии: чистый JSON, Markdown-блок, поиск в тексте.
    /// </summary>
    public static string? ExtractJson(string llmResponse)
    {
        if (string.IsNullOrWhiteSpace(llmResponse))
            return null;

        // 1. Если ответ уже является чистым JSON (начинается с { и заканчивается })
        var trimmed = llmResponse.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return trimmed;
        }

        // 2. Извлекаем из Markdown-блока ```json ... ```
        var codeBlocks = ExtractAllCodeBlocks(llmResponse);
        foreach (var block in codeBlocks)
        {
            var blockTrimmed = block.Trim();
            if (blockTrimmed.StartsWith('{') && blockTrimmed.EndsWith('}'))
            {
                return blockTrimmed;
            }
        }

        // 3. Ищем JSON-объект в тексте (сначала с вложенностью, потом простой)
        var nestedMatch = JsonNestedRegex.Match(llmResponse);
        if (nestedMatch.Success)
        {
            return nestedMatch.Value;
        }

        var simpleMatch = JsonObjectRegex.Match(llmResponse);
        if (simpleMatch.Success)
        {
            return simpleMatch.Value;
        }

        return null;
    }

    /// <summary>
    /// Извлекает структурированный план действий (JSON с полем "tool" или "action").
    /// </summary>
    public static Dictionary<string, object>? ExtractActionPlan(string llmResponse)
    {
        var json = ExtractJson(llmResponse);
        if (json == null)
            return null;

        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var result = new Dictionary<string, object>();

            foreach (var prop in root.EnumerateObject())
            {
                result[prop.Name] = ConvertJsonElement(prop.Value);
            }

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static object ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number => element.TryGetInt32(out var i) ? i : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => element.GetRawText()
        };
    }

    public static string ExtractTextWithoutCode(string llmResponse)
    {
        if (string.IsNullOrWhiteSpace(llmResponse))
            return string.Empty;
        return CodeBlockRegex.Replace(llmResponse, "").Trim();
    }

    public static bool ContainsCode(string llmResponse)
    {
        return CodeBlockRegex.IsMatch(llmResponse);
    }
}