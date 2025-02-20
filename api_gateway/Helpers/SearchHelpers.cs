using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

public class SearchHelpers(VectorDbService vectorDbService, OllamaClient ollamaClient, 
  RedisCacheService cacheService, RerankerService rerankerService, ElasticSearchService elasticSearchService)
{
  private readonly VectorDbService _vectorDbService = vectorDbService;
  private readonly OllamaClient _ollamaClient = ollamaClient;
  private readonly RedisCacheService _cacheService = cacheService;
  private readonly RerankerService _rankerService = rerankerService;
  private readonly ElasticSearchService _elasticSearchService = elasticSearchService;

  public async Task<ActionResult<RAGResponse>> RagSearch(string query, string cacheKey, int topResults = 10, int maxRetries = 3) {
    var data = await GetDocumentData(query, topResults);
    if(data.Documents.Count == 0)
      return new NotFoundObjectResult(new { message = "No relevant documents found." });
    var context = await GetRagContext(data.Documents);
    //var context = _vectorDbService.ExtractRelevantContext(query, uniqueResults);
    string llmResponse = "";
    int attempts = 0;
    while(attempts < maxRetries) {
      llmResponse = await _ollamaClient.GenerateResponseAsync(context, query);
      if(await _ollamaClient.IsAnswerRelevant(query, llmResponse))
        break;
      attempts++;
      Console.WriteLine($"[RagSearch] Attempt {attempts}/{maxRetries} - Response not relevant, retrying...");
    }
    if(attempts == maxRetries) {
      return new BadRequestObjectResult(new {
        message = "Failed to generate a relevant response after multiple attempts."
      });
    }
    return await SetCacheAndReturnOK(cacheKey, new RAGResponse {
      Query = query,
      RetrievedDocuments = data.Documents,
      PromptContext = context,
      LLMResponse = llmResponse
    });
  }

  public async Task<DocumentDataResponse> GetDocumentData(string query, int topResults = 10) {
    var dictMetaData = await _ollamaClient.ExpandMetadata(query);
    var namedEntities = dictMetaData?.GetValueOrDefault("named_entities") as string ?? string.Empty;
    var category = dictMetaData?.GetValueOrDefault("category") as string ?? string.Empty;
    var keywords = dictMetaData?.GetValueOrDefault("keywords") as string ?? string.Empty;
    // Split query into keyword groups for better search
    var queries = await _ollamaClient.GenerateQueryVariations(query);
    var vectorDbResults = new List<DocumentSearchResult>();
    var keywordResults = new List<ElasticSearchDocument>();
    foreach(var q in queries) {
      vectorDbResults.AddRange(await _vectorDbService.SearchDocuments(q,
          categoryFilter: category,
          keywordsFilter: keywords,
          namedEntitiesFilter: namedEntities
      ));
      keywordResults.AddRange(await _elasticSearchService.SearchTextChunksAsync(q));
    }
    var distinctResults = await _rankerService.RerankDocuments(vectorDbResults, keywordResults, query, topResults, true);
        //.DistinctBy(x => x.OriginalDocumentId)
        //.OrderByDescending(x => x.Distance)
        //.Take(topResults)
        //.ToList();
    return new DocumentDataResponse() {
       DictMetaData = dictMetaData,
       Documents = distinctResults,
       Queries = queries
    };
  }

  public async Task<string> GetRagContext(List<DocumentSearchResult> documents) {
    var sb = new StringBuilder(); ;
    foreach(var result in documents) {
      switch(result.DataType) {
        case RagDataType.SourceDocument:
          if(result.OriginalText == null) {
            continue;
          }
          sb.AppendLine(await _ollamaClient.SummarizeTextAsync(result.OriginalText));
          break;
        case RagDataType.Chunk:
        case RagDataType.ChunkSummary:
          sb.AppendLine(result.OriginalText);
          break;
        case RagDataType.ChunkQuesiton:
          if(result.OriginalChunkDocumentId == null) {
            continue;
          }
          sb.AppendLine(await _vectorDbService.GetDocumentById(result.OriginalChunkDocumentId));
          break;
      }
      sb.AppendLine();
      sb.AppendLine();
      sb.AppendLine();
      sb.AppendLine();
    }
    return sb.ToString();
  }

  public async Task<ActionResult<RAGResponse>> SetCacheAndReturnOK(string cacheKey, RAGResponse response) {
    await _cacheService.SetAsync(cacheKey, JsonSerializer.Serialize(response));
    return new OkObjectResult(response);
  }
}

public class DocumentDataResponse {
  public List<DocumentSearchResult> Documents { get; set; } = new();
  public Dictionary<string, object>? DictMetaData { get; set; }
  public List<string>? Queries { get; set; }
}
