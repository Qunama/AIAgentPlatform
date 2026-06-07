// ProjectAIAgent.Core/Services/DocumentationService.cs
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectAIAgent.Core.Models;

namespace ProjectAIAgent.Core.Services;

public class DocumentationService : IDocumentationService
{
    private readonly IOllamaEmbeddingClient _embeddingClient;
    private readonly IOptions<AgentOptions> _agentOptions;
    private readonly ILogger<DocumentationService> _logger;

    private static readonly HashSet<string> DocumentationExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".txt", ".rst", ".adoc"
    };

    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs"
    };

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", "node_modules", ".vs", ".idea", "packages"
    };

    public DocumentationService(
        IOllamaEmbeddingClient embeddingClient,
        IOptions<AgentOptions> agentOptions,
        ILogger<DocumentationService> logger)
    {
        _embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
        _agentOptions = agentOptions ?? throw new ArgumentNullException(nameof(agentOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ==================== Документация ====================

        public async Task<List<DocumentChunk>> DiscoverDocumentsAsync(string rootPath)
    {
        var chunks = new List<DocumentChunk>();
        if (!Directory.Exists(rootPath)) { _logger.LogWarning("Root not found: {Path}", rootPath); return chunks; }

        _logger.LogInformation("Discovering documentation files in: {Path}", rootPath);
        var files = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(f => DocumentationExtensions.Contains(Path.GetExtension(f)))
            .Where(f => !IsExcludedDirectory(Path.GetRelativePath(rootPath, f)))
            .ToList();

        _logger.LogInformation("Found {Count} documentation files", files.Count);

        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var relativePath = Path.GetRelativePath(rootPath, file);
                chunks.AddRange(ChunkDocument(content, relativePath));
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to read {File}", file); }
        }

        _logger.LogInformation("Total doc chunks: {Count}", chunks.Count);
        return chunks;
    }

    public List<DocumentChunk> ChunkDocument(string content, string filePath, int maxChunkSize = 2000)
    {
        var chunks = new List<DocumentChunk>();
        if (string.IsNullOrWhiteSpace(content)) return chunks;

        var sections = Regex.Split(content, @"(?=^##\s)", RegexOptions.Multiline);
        int globalIndex = 0;

        foreach (var section in sections)
        {
            if (string.IsNullOrWhiteSpace(section)) continue;
            var sectionName = ExtractSectionName(section);

            if (section.Length <= maxChunkSize)
            {
                chunks.Add(new DocumentChunk
                {
                    Content = section.Trim(), FilePath = filePath,
                    Section = sectionName, ChunkIndex = globalIndex++
                });
            }
            else
            {
                foreach (var sub in SplitBySize(section, maxChunkSize))
                {
                    chunks.Add(new DocumentChunk
                    {
                        Content = sub.Trim(), FilePath = filePath,
                        Section = sectionName, ChunkIndex = globalIndex++
                    });
                }
            }
        }
        return chunks;
    }

    public async Task EmbedChunksAsync(List<DocumentChunk> chunks)
    {
        if (chunks.Count == 0) return;
        _logger.LogInformation("Embedding {Count} doc chunks...", chunks.Count);
        var texts = chunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingClient.GetEmbeddingsAsync(texts);
        for (int i = 0; i < chunks.Count; i++) chunks[i].Embedding = embeddings[i];
    }

    public async Task<List<DocumentChunk>> IndexDocumentationAsync(string rootPath)
    {
        _logger.LogInformation("Indexing documentation...");
        var chunks = await DiscoverDocumentsAsync(rootPath);
        if (chunks.Count > 0) await EmbedChunksAsync(chunks);
        _logger.LogInformation("Documentation indexed: {Count} chunks", chunks.Count);
        return chunks;
    }

    // ==================== Исходный код (.cs) ====================

    public async Task<List<CodeChunk>> DiscoverCodeFilesAsync(string rootPath)
    {
        var chunks = new List<CodeChunk>();
        if (!Directory.Exists(rootPath)) { _logger.LogWarning("Root not found: {Path}", rootPath); return chunks; }

        _logger.LogInformation("Discovering .cs files in: {Path}", rootPath);
        var files = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsExcludedDirectory(Path.GetRelativePath(rootPath, f)))
            .ToList();

        _logger.LogInformation("Found {Count} .cs files", files.Count);

        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var relativePath = Path.GetRelativePath(rootPath, file);
                chunks.AddRange(ChunkCodeFile(content, relativePath));
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to read {File}", file); }
        }

        _logger.LogInformation("Total code chunks: {Count}", chunks.Count);
        return chunks;
    }

    public List<CodeChunk> ChunkCodeFile(string content, string filePath)
    {
        var chunks = new List<CodeChunk>();
        if (string.IsNullOrWhiteSpace(content)) return chunks;

        var namespaceName = ExtractNamespace(content);
        var classMatches = Regex.Matches(content, @"(?:public|internal|private|protected)?\s*(?:static|abstract|sealed)?\s*class\s+(\w+)");

        foreach (Match classMatch in classMatches)
        {
            var className = classMatch.Groups[1].Value;
            var classStart = classMatch.Index;
            var classEnd = FindClassEnd(content, classStart);

            // Чанк для всего класса
            var classContent = content.Substring(classStart, classEnd - classStart);
            if (classContent.Length > 3000)
            {
                // Разбиваем на методы
                var methodMatches = Regex.Matches(classContent,
                    @"(?:public|internal|private|protected)?\s*(?:static|async|virtual|override)?\s*(?:\w+(?:<[^>]+>)?)\s+(\w+)\s*\([^)]*\)",
                    RegexOptions.Multiline);

                if (methodMatches.Count > 0)
                {
                    foreach (Match methodMatch in methodMatches)
                    {
                        var methodName = methodMatch.Groups[1].Value;
                        if (methodName == className) continue; // пропускаем конструктор

                        var sigStart = classStart + methodMatch.Index;
                        var sigEnd = classStart + methodMatch.Index + methodMatch.Length;
                        var signature = content.Substring(sigStart, sigEnd - sigStart).Trim();

                        chunks.Add(new CodeChunk
                        {
                            Content = signature,
                            FilePath = filePath,
                            Namespace = namespaceName,
                            ClassName = className,
                            MethodName = methodName,
                            Signature = signature,
                            StartLine = CountLines(content, 0, sigStart),
                            EndLine = CountLines(content, 0, sigEnd)
                        });
                    }
                }
                else
                {
                    // Нет методов — сохраняем класс целиком
                    chunks.Add(new CodeChunk
                    {
                        Content = classContent,
                        FilePath = filePath,
                        Namespace = namespaceName,
                        ClassName = className,
                        StartLine = CountLines(content, 0, classStart),
                        EndLine = CountLines(content, 0, classEnd)
                    });
                }
            }
            else
            {
                chunks.Add(new CodeChunk
                {
                    Content = classContent,
                    FilePath = filePath,
                    Namespace = namespaceName,
                    ClassName = className,
                    StartLine = CountLines(content, 0, classStart),
                    EndLine = CountLines(content, 0, classEnd)
                });
            }
        }

        // Если классов нет — сохраняем файл целиком
        if (classMatches.Count == 0)
        {
            chunks.Add(new CodeChunk
            {
                Content = content.Length > 3000 ? content[..3000] : content,
                FilePath = filePath,
                StartLine = 1,
                EndLine = CountLines(content, 0, content.Length)
            });
        }

        return chunks;
    }

    public async Task EmbedCodeChunksAsync(List<CodeChunk> chunks)
    {
        if (chunks.Count == 0) return;
        _logger.LogInformation("Embedding {Count} code chunks...", chunks.Count);
        var texts = chunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingClient.GetEmbeddingsAsync(texts);
        for (int i = 0; i < chunks.Count; i++) chunks[i].Embedding = embeddings[i];
    }

    public async Task<List<CodeChunk>> IndexCodebaseAsync(string rootPath)
    {
        _logger.LogInformation("Indexing codebase...");
        var chunks = await DiscoverCodeFilesAsync(rootPath);
        if (chunks.Count > 0) await EmbedCodeChunksAsync(chunks);
        _logger.LogInformation("Codebase indexed: {Count} chunks", chunks.Count);
        return chunks;
    }

    // ==================== Приватные хелперы ====================

    private static string? ExtractSectionName(string section)
    {
        var match = Regex.Match(section, @"^##\s*(.+)", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractNamespace(string content)
    {
        var match = Regex.Match(content, @"namespace\s+([^\s{]+)");
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static int FindClassEnd(string content, int classStart)
    {
        int braceCount = 0;
        bool started = false;
        for (int i = classStart; i < content.Length; i++)
        {
            if (content[i] == '{') { started = true; braceCount++; }
            else if (content[i] == '}') { braceCount--; if (started && braceCount == 0) return i + 1; }
        }
        return content.Length;
    }

    private static int CountLines(string content, int start, int end)
    {
        return content[start..Math.Min(end, content.Length)].Count(c => c == '\n') + 1;
    }

    private static List<string> SplitBySize(string text, int maxSize)
    {
        var parts = new List<string>();
        for (int i = 0; i < text.Length; i += maxSize)
            parts.Add(text.Substring(i, Math.Min(maxSize, text.Length - i)));
        return parts;
    }

    private static bool IsExcludedDirectory(string relativePath)
    {
        var parts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => ExcludedDirectories.Contains(p));
    }
}