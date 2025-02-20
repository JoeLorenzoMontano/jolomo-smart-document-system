using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class ElasticSearchController : ControllerBase {
  private readonly ElasticSearchService _elasticSearchService;

  public ElasticSearchController(ElasticSearchService elasticSearchService) {
    _elasticSearchService = elasticSearchService;
  }

  /// <summary>
  /// Perform a keyword search on indexed text chunks with optional metadata filtering.
  /// </summary>
  [HttpGet("search")]
  public async Task<IActionResult> SearchTextChunks([FromQuery] string query, [FromQuery] Dictionary<string, object>? filters = null) {
    if(string.IsNullOrWhiteSpace(query))
      return BadRequest("Query cannot be empty.");

    var results = await _elasticSearchService.SearchTextChunksAsync(query, filters);
    return Ok(results);
  }

  /// <summary>
  /// Index a single text chunk into Elasticsearch with optional metadata.
  /// </summary>
  [HttpPost("index")]
  public async Task<IActionResult> IndexTextChunk([FromBody] TextChunkRequest request) {
    if(request == null || string.IsNullOrWhiteSpace(request.Text))
      return BadRequest("Invalid request.");

    var success = await _elasticSearchService.IndexTextChunkAsync(request.Text, request.Metadata);
    return success ? Ok("Text chunk indexed successfully.") : StatusCode(500, "Failed to index text chunk.");
  }

  /// <summary>
  /// Bulk index multiple text chunks with metadata.
  /// </summary>
  [HttpPost("bulk-index")]
  public async Task<IActionResult> BulkIndexTextChunks([FromBody] List<TextChunkRequest> requests) {
    if(requests == null || requests.Count == 0)
      return BadRequest("Invalid request, at least one text chunk is required.");

    var formattedChunks = requests.ConvertAll(r => (r.Text, r.Metadata));
    var success = await _elasticSearchService.BulkIndexTextChunksAsync(formattedChunks);
    return success ? Ok("Bulk text chunks indexed successfully.") : StatusCode(500, "Failed to index bulk text chunks.");
  }

  /// <summary>
  /// Delete a text chunk from Elasticsearch by ID.
  /// </summary>
  [HttpDelete("delete/{id}")]
  public async Task<IActionResult> DeleteTextChunk(string id) {
    if(string.IsNullOrWhiteSpace(id))
      return BadRequest("Invalid document ID.");

    var success = await _elasticSearchService.DeleteTextChunkAsync(id);
    return success ? Ok("Text chunk deleted.") : NotFound("Text chunk not found.");
  }

  /// <summary>
  /// Ensure the Elasticsearch index exists before indexing data.
  /// </summary>
  [HttpPost("ensure-index")]
  public async Task<IActionResult> EnsureIndexExists() {
    await _elasticSearchService.EnsureIndexExistsAsync();
    return Ok("Index verified or created.");
  }
}

/// <summary>
/// Request model for indexing text chunks with optional metadata.
/// </summary>
public class TextChunkRequest {
  public string Text { get; set; }
  public Dictionary<string, object>? Metadata { get; set; }
}
