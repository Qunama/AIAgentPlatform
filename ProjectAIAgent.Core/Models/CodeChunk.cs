// ProjectAIAgent.Core/Models/CodeChunk.cs
namespace ProjectAIAgent.Core.Models;

/// <summary>
/// Чанк исходного кода — фрагмент кода с метаданными о структуре.
/// Используется для индексации в Qdrant и семантического поиска по коду.
/// </summary>
public class CodeChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Content { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Namespace { get; set; }
    public string? ClassName { get; set; }
    public string? MethodName { get; set; }
    public string? Signature { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public float[]? Embedding { get; set; }

    public override string ToString() =>
        $"[{FilePath}] {Namespace}.{ClassName}.{MethodName}() (lines {StartLine}-{EndLine})";
}