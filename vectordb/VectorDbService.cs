using ChromaDB.Client;
using FuzzySharp;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

  public async Task<bool> AddDocument(string text, Dictionary<string, object>? dictMetaData = null,
    EmbeddingConfig? config = null,
    Func<string, Task<string?>>? funcSummarizeChunk = null,
    Func<string, Task<List<string>?>>? funcGenQuestionsForChunk = null) {
    config ??= _embeddingConfig;
    funcSummarizeChunk ??= (str) => Task.FromResult<string?>(null);
    funcGenQuestionsForChunk ??= (str) => Task.FromResult<List<string>?>(null);
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
      var dictSourceDoc = dictMetaData?.Keys.ToDictionary(_ => _, _ => dictMetaData[_]) ?? new Dictionary<string, object>();
      dictSourceDoc.Add("IsSourceDocument", true);
      metadatas.Add(dictSourceDoc);
      await _cacheService.SetAsync($"doc:{sourceDocumentId}", text);
      var chunks = ChunkText(text, config);
      for(int i = 0; i < chunks.Count; i++) {
        var chunk = chunks[i];
        string summary = await funcSummarizeChunk(chunk) ?? "";
        List<string> questions = await funcGenQuestionsForChunk(chunk) ?? new List<string>();
        embedding = await _embeddingService.GenerateEmbeddingAsync(chunk);
        embeddings.Add(new ReadOnlyMemory<float>(embedding));
        var chunkDocumentId = Guid.NewGuid().ToString();
        ids.Add(chunkDocumentId);
        documents.Add(chunk);
        var dictChunkData = dictMetaData?.Keys.ToDictionary(_ => _, _ => dictMetaData[_]) ?? new Dictionary<string, object>();
        dictChunkData.Add("OriginalDocumentId", sourceDocumentId);
        dictChunkData.Add("ChunkDocumentId", chunkDocumentId);
        dictChunkData.Add("ChunkIdx", i);
        metadatas.Add(dictChunkData);
        await _cacheService.SetAsync($"embedding:{chunkDocumentId}", JsonSerializer.Serialize(embedding));
        // Store summary as its own entry
        var summaryEmbedding = await _embeddingService.GenerateEmbeddingAsync(summary);
        embeddings.Add(new ReadOnlyMemory<float>(summaryEmbedding));
        var summaryId = Guid.NewGuid().ToString();
        ids.Add(summaryId);
        documents.Add(summary);
        metadatas.Add(new Dictionary<string, object> {
                { "IsSummary", true },
                { "OriginalDocumentId", sourceDocumentId },
                { "ChunkDocumentId", chunkDocumentId }
            });
        // Store questions as separate entries
        foreach(var question in questions) {
          var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(question);
          embeddings.Add(new ReadOnlyMemory<float>(questionEmbedding));
          var questionId = Guid.NewGuid().ToString();
          ids.Add(questionId);
          documents.Add(question);
          metadatas.Add(new Dictionary<string, object> {
                    { "IsQuestion", true },
                    { "OriginalDocumentId", sourceDocumentId },
                    { "ChunkDocumentId", chunkDocumentId }
                });
        }
      }
      await _collectionClient.Add(ids, embeddings, documents: documents, metadatas: metadatas);
      Console.WriteLine($"[VectorDbService] Added {chunks.Count} chunked documents + summaries/questions to ChromaDB.");
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
      case ChunkMethod.Overlapping:
        return ChunkText(text);
    }
    return [.. chunks];
  }

  public static List<string> ChunkText(string text, int chunkSize = 300, int overlap = 50) {
    if(string.IsNullOrWhiteSpace(text))
      return new List<string>();
    var words = text.Split(' ');
    var chunks = new List<string>();
    for(int i = 0; i < words.Length; i += (chunkSize - overlap)) {
      var chunkWords = words.Skip(i).Take(chunkSize).ToArray();
      string chunk = string.Join(" ", chunkWords);
      // Ensure the chunk ends at a logical boundary (period or new line)
      if(i + chunkSize < words.Length && !chunk.EndsWith('.') && !chunk.EndsWith('?') && !chunk.EndsWith('!') && !chunk.EndsWith('\n')) {
        int lastBreak = Math.Max(chunk.LastIndexOf('.'), chunk.LastIndexOf('\n'));
        if(lastBreak != -1)
          chunk = chunk.Substring(0, lastBreak + 1); // Trim up to the last period or new line
      }
      chunks.Add(chunk.Trim());
    }
    return chunks;
  }

  public async Task<List<SearchResult>> SearchDocuments(
    float[] queryEmbedding,
    string queryText,
    int topResults = 5,
    bool includeOriginalText = true,
    bool includeOriginalDocumentText = true,
    string? categoryFilter = null,
    string? keywordsFilter = null,
    string? namedEntitiesFilter = null) {
    try {
      // Generate a cache key including filters
      string cacheKey = $"search:{queryText}:{categoryFilter ?? "none"}:{keywordsFilter ?? "none"}:{namedEntitiesFilter ?? "none"}";
      // Check if results are already cached
      var cachedResults = await _cacheService.GetAsync(cacheKey);
      if(!string.IsNullOrEmpty(cachedResults)) {
        return JsonSerializer.Deserialize<List<SearchResult>>(cachedResults) ?? new List<SearchResult>();
      }
      // Construct metadata filters for ChromaDB
      var metadataFilters = new Dictionary<string, string>();
      if(!string.IsNullOrEmpty(categoryFilter))
        metadataFilters["category"] = categoryFilter;
      if(!string.IsNullOrEmpty(keywordsFilter))
        metadataFilters["keywords"] = keywordsFilter;
      if(!string.IsNullOrEmpty(namedEntitiesFilter))
        metadataFilters["named_entities"] = namedEntitiesFilter;
      var queryEmbeddings = new List<ReadOnlyMemory<float>> { new ReadOnlyMemory<float>(queryEmbedding) };
      var queryResults = await _collectionClient.Query(
        queryEmbeddings,
        //where: ,
        include: ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances | (includeOriginalText ? ChromaQueryInclude.Documents : ChromaQueryInclude.None)
      );
      var resultsList = new List<SearchResult>();
      foreach(var result in queryResults) {
        foreach(var entry in result) {
          var searchResult = new SearchResult { Id = entry.Id, Distance = entry.Distance };
          if(entry.Metadata.TryGetValue("IsSourceDocument", out _)) {
            searchResult.DataType = RagDataType.SourceDocument;
          }
          else if(entry.Metadata.TryGetValue("ChunkIdx", out _)) {
            searchResult.DataType = RagDataType.Chunk;
          }
          else if(entry.Metadata.TryGetValue("IsSummary", out _)) {
            searchResult.DataType = RagDataType.ChunkSummary;
          }
          else if(entry.Metadata.TryGetValue("IsQuestion", out _)) {
            searchResult.DataType = RagDataType.ChunkQuesiton;
          }
          searchResult.OriginalChunkDocumentId = entry.Metadata.GetValueOrDefault("ChunkDocumentId", null) as string;
          if(includeOriginalText && entry.Document != null)
            searchResult.OriginalText = entry.Document;
          if(entry.Metadata != null && entry.Metadata.ContainsKey("OriginalDocumentId")) {
            searchResult.OriginalDocumentId = entry.Metadata["OriginalDocumentId"].ToString();
            //TODO: Cache
            var originalDocResult = await _collectionClient.Get([searchResult.OriginalDocumentId], include: ChromaGetInclude.Documents | ChromaGetInclude.Metadatas);
            var doc = originalDocResult.FirstOrDefault();
            searchResult.OriginalDocumentMetadata = doc?.Metadata;
            if(includeOriginalDocumentText && searchResult.OriginalDocumentId != null) {
              var originalDocText = await _cacheService.GetAsync($"doc:{searchResult.OriginalDocumentId}");
              if(string.IsNullOrEmpty(originalDocText)) {
                // If not found in Redis, retrieve from ChromaDB
                if(originalDocResult != null && originalDocResult.Count > 0) {
                  originalDocText = doc?.Document ?? "[[ERROR]]";
                  searchResult.OriginalDocumentMetadata = doc?.Metadata;
                  // Store in Redis for future requests
                  await _cacheService.SetAsync($"doc:{searchResult.OriginalDocumentId}", originalDocText);
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
      // Apply reranking and filtering (if needed)
      resultsList = resultsList.OrderByDescending(r => r.Distance) // TODO: Implement proper reranking
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

  public async Task<List<SearchResult>> SearchDocuments(string query,
    int topResults = 5,
    bool includeOriginalText = true,
    bool includeOriginalDocumentText = false,
    string? categoryFilter = null,
    string? keywordsFilter = null,
    string? namedEntitiesFilter = null)
  {
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
    return await SearchDocuments(queryEmbedding, 
      query, 
      topResults,
      includeOriginalText,
      includeOriginalDocumentText,
      categoryFilter: categoryFilter, 
      keywordsFilter: keywordsFilter, 
      namedEntitiesFilter: namedEntitiesFilter
    );
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

  public async Task<string?> GetDocumentById(string documentId) {
    try {
      // Check Redis cache first
      var cachedDocument = await _cacheService.GetAsync($"doc:{documentId}");
      if(!string.IsNullOrEmpty(cachedDocument))
        return cachedDocument;
      var results = await _collectionClient.Get([documentId], include: ChromaGetInclude.Documents);
      var document = results.FirstOrDefault()?.Document;
      if(document != null) {
        // Cache the document for future requests
        await _cacheService.SetAsync($"doc:{documentId}", document);
      }
      return document;
    }
    catch(Exception ex) {
      Console.WriteLine($"[VectorDbService] Error retrieving document by ID: {ex.Message}");
      return null;
    }
  }

}
