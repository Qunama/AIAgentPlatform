// ProjectAIAgent.Core/Models/DocumentChunk.cs
namespace ProjectAIAgent.Core.Models;

/// <summary>
/// Чанк документации — фрагмент текста с метаданными.
/// Используется для индексации в Qdrant и семантического поиска.
/// </summary>
public class DocumentChunk
{
    /// <summary>Уникальный идентификатор чанка</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Текст чанка</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Путь к исходному файлу (относительно корня проекта)</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Название секции (заголовок ##), если есть</summary>
    public string? Section { get; set; }

    /// <summary>Порядковый номер чанка в файле</summary>
    public int ChunkIndex { get; set; }

    /// <summary>Дата последнего изменения файла</summary>
    public DateTime LastModified { get; set; }

    /// <summary>Размер чанка в символах</summary>
    public int Size => Content.Length;

    /// <summary>Векторное представление (заполняется после эмбеддинга)</summary>
    public float[]? Embedding { get; set; }

    public override string ToString() => $"[{FilePath}] {Section ?? $"Chunk {ChunkIndex}"} ({Size} chars)";
}