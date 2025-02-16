using ChromaDB.Client;
using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using FuzzySharp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using System.Net.Http;

public class VectorDbService {
  private readonly ChromaClient _chromaClient;
  private readonly ChromaConfigurationOptions _configOptions;
  private readonly HttpClient _httpClient;
  private readonly string _collectionName;
  private readonly ChromaCollectionClient _collectionClient;
  private readonly ILocalEmbeddingService _embeddingService;
  private static EmbeddingConfig _embeddingConfig = new();
  private static readonly SemaphoreSlim _semaphore = new(1, 1);

  public VectorDbService(ILocalEmbeddingService embeddingService, IConfiguration configuration, IHttpClientFactory httpClientFactory) {
    _collectionName = configuration?["ChromaDB:CollectionName"] ?? "documents";
    _configOptions = new ChromaConfigurationOptions(uri: configuration?["ChromaDB:Uri"] ?? "http://localhost:8000/api/v1/");
    _httpClient = httpClientFactory.CreateClient();
    _chromaClient = new ChromaClient(_configOptions, _httpClient);
    _embeddingService = embeddingService;
    var collection = _chromaClient.GetOrCreateCollection(_collectionName).Result;
    _collectionClient = new ChromaCollectionClient(collection, _configOptions, _httpClient);
  }

  public async Task<bool> AddDocument(string text, EmbeddingConfig? config = null) {
    config ??= _embeddingConfig;
    try {
      var embeddings = new List<ReadOnlyMemory<float>>();
      var ids = new List<string>();
      var documents = new List<string>();
      var metadatas = new List<Dictionary<string, object>>();
      float[] embedding = await _embeddingService.GenerateEmbeddingAsync(text);
      embeddings.Add(new ReadOnlyMemory<float>(embedding));
      var sourceDocumentId = Guid.NewGuid().ToString();
      ids.Add(sourceDocumentId);
      documents.Add(text);
      metadatas.Add(new Dictionary<string, object> { { "IsSourceDocument", true } });

      var chunks = ChunkText(text, config);
      var chunkTasks = chunks.Select(async chunk =>
      {
        embedding = await _embeddingService.GenerateEmbeddingAsync(chunk);
        embeddings.Add(new ReadOnlyMemory<float>(embedding));
        var documentId = Guid.NewGuid().ToString();
        ids.Add(documentId);
        documents.Add(chunk);
        metadatas.Add(new Dictionary<string, object> { { "OriginalDocumentId", sourceDocumentId } });
      });
      await Task.WhenAll(chunkTasks);
      await _collectionClient.Add(ids, embeddings, documents: documents, metadatas: metadatas);
      Console.WriteLine($"[VectorDbService] Added {chunks.Count} chunked documents to ChromaDB.");
      return true;
    }
    catch(Exception ex) {
      Console.WriteLine($"[VectorDbService] Error adding document: {ex.Message}");
      return false;
    }
  }

  public List<string> ChunkText(string text, EmbeddingConfig config) {
    var chunks = new ConcurrentBag<string>();
    switch(config.ChunkingMethod) {
      case ChunkMethod.Paragraph:
        var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        Parallel.ForEach(paragraphs, paragraph => chunks.Add(paragraph));
        break;
      case ChunkMethod.Newline:
        var lines = text.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Parallel.ForEach(lines, line => chunks.Add(line));
        break;
      case ChunkMethod.Word:
        var words = text.Split(' ');
        Parallel.For(0, words.Length, i =>
        {
          if(i % (config.ChunkSize - config.Overlap) == 0) {
            var chunk = string.Join(" ", words.Skip(i).Take(config.ChunkSize));
            chunks.Add(chunk);
          }
        });
        break;
    }
    return [.. chunks];
  }

  public async Task<List<SearchResult>> SearchDocuments(float[] queryEmbedding, string queryText, int topResults = 5, bool includeOriginalText = true, bool includeOriginalDocumentText = false) {
    try {
      var queryEmbeddings = new List<ReadOnlyMemory<float>> { new ReadOnlyMemory<float>(queryEmbedding) };
      var queryResults = await _collectionClient.Query(queryEmbeddings, include: ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances | (includeOriginalText ? ChromaQueryInclude.Documents : ChromaQueryInclude.None));
      var resultsList = new List<SearchResult>();
      foreach(var result in queryResults) {
        foreach(var entry in result) {
          var searchResult = new SearchResult { Id = entry.Id, Distance = entry.Distance };
          if(includeOriginalText && entry.Document != null) {
            searchResult.OriginalText = entry.Document;
          }
          if(entry.Metadata != null && entry.Metadata.ContainsKey("OriginalDocumentId")) {
            searchResult.OriginalDocumentId = entry.Metadata["OriginalDocumentId"].ToString();
            if(includeOriginalDocumentText && searchResult.OriginalDocumentId != null) {
              var originalDocResult = await _collectionClient.Get([ searchResult.OriginalDocumentId ], include: ChromaGetInclude.Documents);
              if(originalDocResult != null && originalDocResult.Count > 0) {
                searchResult.OriginalDocumentText = originalDocResult.FirstOrDefault()?.Document ?? "[[ERROR]]";
              }
            }
          }
          resultsList.Add(searchResult);
        }
      }
      // Prioritize results that contain the exact query string or similar words
      resultsList = resultsList.OrderByDescending(r => r.OriginalText != null && r.OriginalText.Contains(queryText, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
          .ThenByDescending(r => r.Distance)
          .ThenByDescending(r => r.OriginalText != null ? Fuzz.Ratio(r.OriginalText, queryText) : 0) // Using FuzzySharp for text similarity
          .Take(topResults)
          .ToList();

      return resultsList;
    }
    catch(Exception ex) {
      Console.WriteLine($"[VectorDbService] Error searching documents: {ex.Message}");
      return new List<SearchResult> { new SearchResult { Id = "Error", Distance = -1 } };
    }
  }

  public async Task<List<SearchResult>> SearchDocuments(string query) {
    return await SearchDocuments(await _embeddingService.GenerateEmbeddingAsync(query), query);
  }

  public async Task<bool> ClearChromaDB() {
    try {
      await _semaphore.WaitAsync();
      await _chromaClient.DeleteCollection(_collectionClient.Collection.Name);
      Console.WriteLine("[VectorDbService] Cleared all documents from ChromaDB.");
      return true;
    }
    catch(Exception ex) {
      Console.WriteLine($"[VectorDbService] Error clearing ChromaDB: {ex.Message}");
      return false;
    }
    finally {
      _semaphore.Release();
    }
  }
}