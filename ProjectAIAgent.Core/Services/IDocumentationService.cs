// ProjectAIAgent.Core/Services/IDocumentationService.cs
using ProjectAIAgent.Core.Models;

namespace ProjectAIAgent.Core.Services;

/// <summary>
/// Сервис для поиска, разбиения на чанки и векторизации документации проекта.
/// </summary>
public interface IDocumentationService
{
    /// <summary>
    /// Находит все .md и .txt файлы в проекте и разбивает их на чанки.
    /// </summary>
    /// <param name="rootPath">Корневая папка проекта для поиска</param>
    /// <returns>Список чанков без векторов</returns>
    Task<List<DocumentChunk>> DiscoverDocumentsAsync(string rootPath);

    /// <summary>
    /// Разбивает текст на чанки по секциям (заголовкам ##) или по размеру.
    /// </summary>
    /// <param name="content">Полный текст документа</param>
    /// <param name="filePath">Путь к файлу (для метаданных)</param>
    /// <param name="maxChunkSize">Максимальный размер чанка в символах</param>
    /// <returns>Список чанков</returns>
    List<DocumentChunk> ChunkDocument(string content, string filePath, int maxChunkSize = 2000);

    /// <summary>
    /// Векторизует список чанков через Ollama Embeddings API.
    /// </summary>
    /// <param name="chunks">Чанки для векторизации (поле Embedding будет заполнено)</param>
    Task EmbedChunksAsync(List<DocumentChunk> chunks);

    /// <summary>
    /// Полная индексация: поиск, разбиение, векторизация.
    /// </summary>
    /// <param name="rootPath">Корневая папка проекта</param>
    /// <returns>Список чанков с векторами, готовых для сохранения в Qdrant</returns>
    Task<List<DocumentChunk>> IndexDocumentationAsync(string rootPath);

    /// <summary>
    /// Находит все .cs файлы в проекте и разбивает их на чанки по классам и методам.
    /// </summary>
    Task<List<CodeChunk>> DiscoverCodeFilesAsync(string rootPath);

    /// <summary>
    /// Разбивает содержимое .cs файла на чанки по классам и методам.
    /// </summary>
    List<CodeChunk> ChunkCodeFile(string content, string filePath);

    /// <summary>
    /// Векторизует список чанков кода.
    /// </summary>
    Task EmbedCodeChunksAsync(List<CodeChunk> chunks);

    /// <summary>
    /// Полная индексация исходного кода.
    /// </summary>
    Task<List<CodeChunk>> IndexCodebaseAsync(string rootPath);
}