using ChromaDB.Client;
using System.Reflection.Metadata;

public class VectorDbService {
  private readonly ChromaClient _chromaClient;
  private readonly ChromaConfigurationOptions _configOptions;
  private readonly HttpClient _httpClient;
  private readonly string _collectionName = "documents";
  private readonly ChromaCollectionClient _collectionClient;
  private readonly ILocalEmbeddingService _embeddingService;
  private static EmbeddingConfig _embeddingConfig = new();

  public VectorDbService(ILocalEmbeddingService embeddingService) {
    _configOptions = new ChromaConfigurationOptions(uri: "http://localhost:8000/api/v1/");
    _httpClient = new HttpClient();
    _chromaClient = new ChromaClient(_configOptions, _httpClient);
    _embeddingService = embeddingService;
    var collection = _chromaClient.GetOrCreateCollection(_collectionName).Result;
    _collectionClient = new ChromaCollectionClient(collection, _configOptions, _httpClient);
  }

  public async Task<bool> AddDocument(string text, EmbeddingConfig? config=null) {
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
      foreach(var chunk in chunks) {
        embedding = await _embeddingService.GenerateEmbeddingAsync(chunk);
        embeddings.Add(new ReadOnlyMemory<float>(embedding));
        var documentId = Guid.NewGuid().ToString();
        ids.Add(documentId);
        documents.Add(chunk);
        metadatas.Add(new Dictionary<string, object> { { "OriginalDocumentId", sourceDocumentId } });
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
    var chunks = new List<string>();
    switch(config.ChunkingMethod) {
      case ChunkMethod.Paragraph:
        var paragraphs = text.Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries);
        chunks.AddRange(paragraphs);
        break;
      case ChunkMethod.Newline:
        var lines = text.Split(["\n"], StringSplitOptions.RemoveEmptyEntries);
        chunks.AddRange(lines);
        break;
      case ChunkMethod.Word:
        var words = text.Split(' ');
        for(int i = 0; i < words.Length; i += config.ChunkSize - config.Overlap) {
          var chunk = string.Join(" ", words.Skip(i).Take(config.ChunkSize));
          chunks.Add(chunk);
        }
        break;
    }
    return chunks;
  }

  public async Task<List<SearchResult>> SearchDocuments(float[] queryEmbedding, string queryText, int topResults = 5, bool includeOriginalText = true, bool includeOriginalDocumentText = false) {
    try {
      var queryEmbeddings = new List<ReadOnlyMemory<float>> { new ReadOnlyMemory<float>(queryEmbedding) };
      var queryResults = await _collectionClient.Query(queryEmbeddings, include: ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances | (includeOriginalText ? ChromaQueryInclude.Documents : 0));
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
              var originalDocResult = await _collectionClient.Get([searchResult.OriginalDocumentId], include: ChromaGetInclude.Documents);
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
          .ThenByDescending(r => r.OriginalText != null ? CalculateTextSimilarity(r.OriginalText, queryText) : 0)
          .Take(topResults)
          .ToList();

      return resultsList;
    }
    catch(Exception ex) {
      Console.WriteLine($"[VectorDbService] Error searching documents: {ex.Message}");
      return [new SearchResult { Id = "Error", Distance = -1 }];
    }
  }

  private int CalculateTextSimilarity(string text, string query) {
    var textWords = text.Split(' ');
    var queryWords = query.Split(' ');
    return textWords.Intersect(queryWords, StringComparer.OrdinalIgnoreCase).Count();
  }


  public async Task<List<SearchResult>> SearchDocuments(string query) {
    return await SearchDocuments(await _embeddingService.GenerateEmbeddingAsync(query), query);
  }

  public async Task<bool> ClearChromaDB() {
    try {
      await _chromaClient.DeleteCollection(_collectionClient.Collection.Name);
      Console.WriteLine("[VectorDbService] Cleared all documents from ChromaDB.");
      return true;
    }
    catch(Exception ex) {
      Console.WriteLine($"[VectorDbService] Error clearing ChromaDB: {ex.Message}");
      return false;
    }
  }

}
