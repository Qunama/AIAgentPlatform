// ProjectAIAgent.Core/Services/IQdrantService.cs
namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Сервис для работы с Qdrant — создание коллекций, вставка и поиск точек.
/// </summary>
public interface IQdrantService
{
    /// <summary>
    /// Создаёт коллекцию, если она ещё не существует.
    /// </summary>
    Task EnsureCollectionExistsAsync(string collectionName, int vectorSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Вставляет точки (чанки с векторами) в коллекцию.
    /// </summary>
    Task UpsertPointsAsync(string collectionName, List<QdrantPoint> points, CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет семантический поиск по коллекции.
    /// </summary>
    Task<List<QdrantSearchResult>> SearchAsync(
        string collectionName,
        float[] queryVector,
        int topK = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет все точки из коллекции.
    /// </summary>
    Task ClearCollectionAsync(string collectionName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Точка для вставки в Qdrant.
/// </summary>
public class QdrantPoint
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public float[] Vector { get; set; } = Array.Empty<float>();
    public Dictionary<string, object>? Payload { get; set; }
}

/// <summary>
/// Результат поиска в Qdrant.
/// </summary>
public class QdrantSearchResult
{
    public string Id { get; set; } = string.Empty;
    public float Score { get; set; }
    public Dictionary<string, object>? Payload { get; set; }
}