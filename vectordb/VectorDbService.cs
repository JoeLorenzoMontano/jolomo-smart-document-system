using ChromaDB.Client;
using FuzzySharp;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;

public class VectorDbService {
  private readonly ChromaClient _chromaClient;
  private readonly ChromaConfigurationOptions _configOptions;
  private readonly HttpClient _httpClient;
  private readonly string _collectionName;
  private readonly ChromaCollectionClient _collectionClient;
  private readonly ILocalEmbeddingService _embeddingService;
  private readonly RedisCacheService _cacheService;
  private static EmbeddingConfig _embeddingConfig = new();
  private static readonly SemaphoreSlim _semaphore = new(1, 1);

  public VectorDbService(ILocalEmbeddingService embeddingService, IConfiguration configuration, IHttpClientFactory httpClientFactory, RedisCacheService cacheService) {
    _collectionName = configuration?["ChromaDB:CollectionName"] ?? "documents";
    _configOptions = new ChromaConfigurationOptions(uri: configuration?["ChromaDB:Uri"] ?? "http://localhost:8000/api/v1/");
    _httpClient = httpClientFactory.CreateClient();
    _chromaClient = new ChromaClient(_configOptions, _httpClient);
    _embeddingService = embeddingService;
    _cacheService = cacheService;
    var collection = _chromaClient.GetOrCreateCollection(_collectionName).Result;
    _collectionClient = new ChromaCollectionClient(collection, _configOptions, _httpClient);
  }

  public async Task<bool> AddDocument(string text, Dictionary<string, object>? dictMetaData = null, EmbeddingConfig? config = null) {
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
      dictMetaData ??= [];
      dictMetaData.Add("IsSourceDocument", true);
      metadatas.Add(dictMetaData);
      // Cache original document text
      await _cacheService.SetAsync($"doc:{sourceDocumentId}", text);
      var chunks = ChunkText(text, config);
      foreach(var chunk in chunks) {
        embedding = await _embeddingService.GenerateEmbeddingAsync(chunk);
        embeddings.Add(new ReadOnlyMemory<float>(embedding));
        var documentId = Guid.NewGuid().ToString();
        ids.Add(documentId);
        documents.Add(chunk);
        metadatas.Add(new Dictionary<string, object> {
          { "OriginalDocumentId", sourceDocumentId } 
        });
        // Cache chunk embeddings
        await _cacheService.SetAsync($"embedding:{documentId}", JsonSerializer.Serialize(embedding));
      }
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
        var paragraphs = text.Split(["\n \n"], StringSplitOptions.RemoveEmptyEntries);
        Parallel.ForEach(paragraphs, paragraph => chunks.Add(paragraph));
        break;
      case ChunkMethod.Newline:
        var lines = text.Split(["\n"], StringSplitOptions.RemoveEmptyEntries);
        Parallel.ForEach(lines, line => chunks.Add(line));
        break;
      case ChunkMethod.Word:
        var words = text.Split(' ');
        Parallel.For(0, words.Length, i => {
          if(i % (config.ChunkSize - config.Overlap) == 0) {
            var chunk = string.Join(" ", words.Skip(i).Take(config.ChunkSize));
            chunks.Add(chunk);
          }
        });
        break;
    }
    return [.. chunks];
  }

  public async Task<List<SearchResult>> SearchDocuments(float[] queryEmbedding, string queryText, int topResults = 5, bool includeOriginalText = true, bool includeOriginalDocumentText = true) {
    try {
      string cacheKey = $"search:{queryText}";
      var cachedResults = await _cacheService.GetAsync(cacheKey);
      if(!string.IsNullOrEmpty(cachedResults)) {
        return JsonSerializer.Deserialize<List<SearchResult>>(cachedResults) ?? [];
      }
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
              var originalDocText = await _cacheService.GetAsync($"doc:{searchResult.OriginalDocumentId}");
              if(string.IsNullOrEmpty(originalDocText)) {
                // If not found in Redis, retrieve from ChromaDB
                var originalDocResult = await _collectionClient.Get([searchResult.OriginalDocumentId], include: ChromaGetInclude.Documents);
                if(originalDocResult != null && originalDocResult.Count > 0) {
                  originalDocText = originalDocResult.FirstOrDefault()?.Document ?? "[[ERROR]]";
                  // Store in Redis for future requests
                  await _cacheService.SetAsync(cacheKey, originalDocText);
                }
                else {
                  originalDocText = "[[ERROR: Document not found]]";
                }
              }
              searchResult.OriginalDocumentText = originalDocText;
            }
          }
          resultsList.Add(searchResult);
        }
      }
      resultsList = resultsList.OrderByDescending(r => r.Distance)//TODO: Handle reranking differently
          .ThenByDescending(r => r.OriginalText != null ? Fuzz.Ratio(r.OriginalText, queryText) : 0)
          .Take(topResults)
          .ToList();
      // Cache search results
      await _cacheService.SetAsync(cacheKey, JsonSerializer.Serialize(resultsList));
      return resultsList;
    }
    catch(Exception ex) {
      Console.WriteLine($"[VectorDbService] Error searching documents: {ex.Message}");
      return new List<SearchResult> { new SearchResult { Id = "Error", Distance = -1 } };
    }
  }

  public async Task<List<SearchResult>> SearchDocuments(string query) {
    string embeddingCacheKey = $"embedding:{query}";
    // Check Redis for cached embedding
    var cachedEmbeddingJson = await _cacheService.GetAsync(embeddingCacheKey);
    float[] queryEmbedding;
    if(!string.IsNullOrEmpty(cachedEmbeddingJson)) {
      // Deserialize and use the cached embedding
      queryEmbedding = JsonSerializer.Deserialize<float[]>(cachedEmbeddingJson);
      Console.WriteLine("[VectorDbService] Retrieved query embedding from cache.");
    }
    else {
      // Generate new embedding if not in cache
      queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
      // Cache the new embedding for future queries
      await _cacheService.SetAsync(embeddingCacheKey, JsonSerializer.Serialize(queryEmbedding));
      Console.WriteLine("[VectorDbService] Cached new query embedding.");
    }
    return await SearchDocuments(queryEmbedding, query);
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

  public string ExtractRelevantContext(string query, List<SearchResult> results, int maxContextSize = 2000) {
    List<string> filteredSections = [];
    foreach(var result in results) {
      if(string.IsNullOrWhiteSpace(result.OriginalDocumentText))
        continue;
      var paragraphs = result.OriginalDocumentText.Split("\n \n");
      var relevantParagraphs = paragraphs
          .Where(p => Fuzz.PartialRatio(p, query) > 50)
          .ToList();
      filteredSections.AddRange(relevantParagraphs);
    }
    string context = string.Join("\n\n", filteredSections);
    return context.Length > maxContextSize ? context.Substring(0, maxContextSize) : context;
  }
}
