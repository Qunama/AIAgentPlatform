// ProjectAIAgent.Core/Services/QdrantOptions.cs
namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Конфигурация для подключения к Qdrant.
/// Связывается с секцией "Qdrant" в appsettings.json.
/// </summary>
public class QdrantOptions
{
    /// <summary>Endpoint Qdrant (по умолчанию http://localhost:6333)</summary>
    public string Endpoint { get; set; } = "http://localhost:6333";

    /// <summary>Размерность вектора (зависит от модели эмбеддингов)</summary>
    public int VectorSize { get; set; } = 3584;

    /// <summary>Название коллекции для хранения документации</summary>
    public string CollectionName { get; set; } = "project_docs";
}