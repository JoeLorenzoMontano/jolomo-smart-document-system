using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Threading.Tasks;
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
      // Split query into keyword groups for better search
      var queries = await _ollamaClient.GenerateQueryVariations(query);
      var combinedResults = new List<SearchResult>();
      foreach(var q in queries) {
        var results = await _vectorDbService.SearchDocuments(q);
        combinedResults.AddRange(results);
      }
      var uniqueResults = combinedResults
          .DistinctBy(x => x.OriginalDocumentId)
          .OrderByDescending(x => x.Distance)
          //.OrderByDescending(x => ...)//TODO: Handle reranking differently
          .Take(topResults)
          .ToList();
      if(uniqueResults.Count == 0)
        return NotFound(new { message = "No relevant documents found." });
      var context = string.Join("\n\n", uniqueResults.Select(r => r.OriginalDocumentText));
      var llmResponse = await _ollamaClient.GenerateResponseAsync(context, query);
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
}
