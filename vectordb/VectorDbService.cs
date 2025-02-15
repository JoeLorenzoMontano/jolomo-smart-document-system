using ChromaDB.Client;

public class VectorDbService {
  private readonly ChromaClient _chromaClient;
  private readonly ChromaConfigurationOptions _configOptions;
  private readonly HttpClient _httpClient;
  private readonly string _collectionName = "documents";
  private readonly ChromaCollectionClient _collectionClient;
  private readonly ILocalEmbeddingService _embeddingService;

  public VectorDbService(ILocalEmbeddingService embeddingService) {
    _configOptions = new ChromaConfigurationOptions(uri: "http://localhost:8000/api/v1/");
    _httpClient = new HttpClient();
    _chromaClient = new ChromaClient(_configOptions, _httpClient);
    _embeddingService = embeddingService;
    var collection = _chromaClient.GetOrCreateCollection(_collectionName).Result;
    _collectionClient = new ChromaCollectionClient(collection, _configOptions, _httpClient);
  }

  public async Task<bool> AddDocument(string documentId, string text) {
    try {
      float[] embedding = await _embeddingService.GenerateEmbeddingAsync(text);
      var embeddingMemory = new List<ReadOnlyMemory<float>> { new ReadOnlyMemory<float>(embedding) };
      await _collectionClient.Add(new List<string> { documentId }, embeddings: embeddingMemory);
      Console.WriteLine($"[VectorDbService] Added document {documentId} with embedding to ChromaDB.");
      return true;
    }
    catch(Exception ex) {
      Console.WriteLine($"[VectorDbService] Error adding document: {ex.Message}");
      return false;
    }
  }

  public async Task<List<(string Id, float Distance)>> SearchDocuments(float[] queryEmbedding) {
    try {
      var queryEmbeddings = new List<ReadOnlyMemory<float>> { new ReadOnlyMemory<float>(queryEmbedding) };
      var queryResults = await _collectionClient.Query(queryEmbeddings, include: ChromaQueryInclude.Metadatas | ChromaQueryInclude.Distances);
      var resultsList = new List<(string Id, float Distance)>();
      foreach(var result in queryResults) {
        foreach(var entry in result) {
          resultsList.Add((entry.Id, entry.Distance));
        }
      }

      return resultsList;
    }
    catch(Exception ex) {
      Console.WriteLine($"[VectorDbService] Error searching documents: {ex.Message}");
      return new List<(string, float)> { ("Error", -1) };
    }
  }
}
