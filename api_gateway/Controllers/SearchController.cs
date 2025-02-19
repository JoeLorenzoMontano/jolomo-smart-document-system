using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("api/search")]
public class SearchController(VectorDbService vectorDbService, ILocalEmbeddingService embeddingService, OllamaClient ollamaClient, RedisCacheService cacheService) : ControllerBase {
  private readonly VectorDbService _vectorDbService = vectorDbService;
  private readonly ILocalEmbeddingService _embeddingService = embeddingService;
  private readonly OllamaClient _ollamaClient = ollamaClient;
  private readonly RedisCacheService _cacheService = cacheService;

  /// <summary>
  /// Standard vector database search with caching.
  /// </summary>
  [HttpGet("documents")]
  public async Task<IActionResult> SearchDocuments([FromQuery] string query) {
    if(string.IsNullOrWhiteSpace(query))
      return BadRequest(new { message = "Query cannot be empty." });
    try {
      string cacheKey = $"search:{query}";
      var cachedResults = await _cacheService.GetAsync(cacheKey);
      if(!string.IsNullOrEmpty(cachedResults)) {
        return Ok(JsonSerializer.Deserialize<List<SearchResult>>(cachedResults));
      }
      var results = await _vectorDbService.SearchDocuments(query);
      if(results.Count == 0)
        return NotFound(new { message = "No relevant documents found." });
      // Cache search results
      await _cacheService.SetAsync(cacheKey, JsonSerializer.Serialize(results));
      return Ok(results);
    }
    catch(Exception ex) {
      return StatusCode(500, new { message = "Internal server error.", error = ex.Message });
    }
  }

  [HttpGet("variations")]
  public async Task<IActionResult> QueryInputVariations([FromQuery] string query) {
    return Ok(await _ollamaClient.GenerateQueryVariations(query));
  }

  [HttpGet("rag")]
  public async Task<IActionResult> SearchWithRAG([FromQuery] string query, [FromQuery] int topResults = 5) {
    if(string.IsNullOrWhiteSpace(query))
      return BadRequest(new { message = "Query cannot be empty." });
    try {
      string cacheKey = $"rag:{query}";
      var cachedResponse = await _cacheService.GetAsync(cacheKey);
      if(!string.IsNullOrEmpty(cachedResponse)) {
        return Ok(JsonSerializer.Deserialize<RAGResponse>(cachedResponse));
      }
      var dictMetaData = await _ollamaClient.ExpandMetadata(query);
      object named_entities = "";
      object category = "";
      object keywords = "";
      dictMetaData?.TryGetValue("named_entities", out named_entities);
      dictMetaData?.TryGetValue("category", out category);
      dictMetaData?.TryGetValue("keywords", out keywords);
      // Split query into keyword groups for better search
      var queries = await _ollamaClient.GenerateQueryVariations(query);
      var combinedResults = new List<SearchResult>();
      foreach(var q in queries) {
        var results = await _vectorDbService.SearchDocuments(q, 
          categoryFilter: named_entities?.ToString(), 
          keywordsFilter: keywords?.ToString(), 
          namedEntitiesFilter: named_entities?.ToString()
        );
        combinedResults.AddRange(results);
      }
      var uniqueResults = combinedResults
          .Where(x => x.OriginalDocumentText!=null)
          .DistinctBy(x => x.OriginalDocumentId)
          .OrderByDescending(x => x.Distance)
          //.OrderByDescending(x => ...)//TODO: Handle reranking differently
          .Take(topResults)
          .ToList();
      if(uniqueResults.Count == 0)
        return NotFound(new { message = "No relevant documents found." });
      var sb = new StringBuilder(); ;
      foreach(var result in uniqueResults) {
        if(result.OriginalDocumentText == null)
          continue;
        sb.AppendLine(await _ollamaClient.SummarizeTextAsync(result.OriginalDocumentText));
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine();
      }
      //var context = string.Join("\n\r\r\r", uniqueResults.Select(x => x.OriginalDocumentText));
      //var context = _vectorDbService.ExtractRelevantContext(query, uniqueResults);
      var llmResponse = await _ollamaClient.GenerateResponseAsync(sb.ToString(), query);
      var response = new RAGResponse {
        Query = query,
        RetrievedDocuments = uniqueResults,
        LLMResponse = llmResponse
      };
      // Cache the final RAG response
      await _cacheService.SetAsync(cacheKey, JsonSerializer.Serialize(response));
      return Ok(response);
    }
    catch(Exception ex) {
      return StatusCode(500, new { message = "Internal server error.", error = ex.Message });
    }
  }

  [HttpGet("rag-docs")]
  public async Task<IActionResult> GetRagDoc([FromQuery] string query, [FromQuery] int topResults = 5) {
    if(string.IsNullOrWhiteSpace(query))
      return BadRequest(new { message = "Query cannot be empty." });
    try {
      var dictMetaData = await _ollamaClient.ExpandMetadata(query);
      object named_entities = "";
      object category = "";
      object keywords = "";
      dictMetaData?.TryGetValue("named_entities", out named_entities);
      dictMetaData?.TryGetValue("category", out category);
      dictMetaData?.TryGetValue("keywords", out keywords);
      // Split query into keyword groups for better search
      var queries = await _ollamaClient.GenerateQueryVariations(query);
      var combinedResults = new List<SearchResult>();
      foreach(var q in queries) {
        var results = await _vectorDbService.SearchDocuments(q,
          categoryFilter: named_entities?.ToString(),
          keywordsFilter: keywords?.ToString(),
          namedEntitiesFilter: named_entities?.ToString()
        );
        combinedResults.AddRange(results);
      }
      var uniqueResults = combinedResults
          .Where(x => x.OriginalDocumentText != null)
          .DistinctBy(x => x.OriginalDocumentId)
          .OrderByDescending(x => x.Distance)
          //.OrderByDescending(x => ...)//TODO: Handle reranking differently
          .Take(topResults)
          .ToList();
      var result = new { 
        queries,
        dictMetaData,
        data = uniqueResults 
      };
      return Ok(result);
    }
    catch(Exception ex) {
      return StatusCode(500, new { message = "Internal server error.", error = ex.Message });
    }
  }
}
