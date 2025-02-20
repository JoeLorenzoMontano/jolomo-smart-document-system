using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[ApiController]
[Route("api/search")]
public class SearchController(VectorDbService vectorDbService, ILocalEmbeddingService embeddingService, OllamaClient ollamaClient, RedisCacheService cacheService, SearchHelpers searchHelper) : ControllerBase {
  private readonly VectorDbService _vectorDbService = vectorDbService;
  private readonly ILocalEmbeddingService _embeddingService = embeddingService;
  private readonly OllamaClient _ollamaClient = ollamaClient;
  private readonly RedisCacheService _cacheService = cacheService;
  private readonly SearchHelpers _searchHelper = searchHelper;

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
  public async Task<ActionResult<RAGResponse>> SearchWithRAG([FromQuery] string query, [FromQuery] int topResults = 10) {
    if(string.IsNullOrWhiteSpace(query))
      return BadRequest(new { message = "Query cannot be empty." });
    try {
      string cacheKey = $"rag:{query}";
      var cachedResponse = await _cacheService.GetAsync(cacheKey);
      return cachedResponse is not null
        ? Ok(JsonSerializer.Deserialize<RAGResponse>(cachedResponse))
        : await _searchHelper.RagSearch(query, cacheKey, topResults);
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
      return Ok(await _searchHelper.GetDocumentData(query, topResults));
    }
    catch(Exception ex) {
      return StatusCode(500, new { message = "Internal server error.", error = ex.Message });
    }
  }
}
