using Microsoft.AspNetCore.Mvc;

[Route("api/upload")]
[ApiController]
public class UploadController : ControllerBase {
  private readonly string _uploadsFolder = "uploads/";
  private readonly MqttClientService _mqttClientService;
  private readonly VectorDbService _vectorDbService;
  private readonly ILocalEmbeddingService _embeddingService;
  private readonly OllamaClient _ollamaClient;

  public UploadController(MqttClientService mqttClientService, VectorDbService vectorDbService, ILocalEmbeddingService embeddingService, OllamaClient OllamaClient) {
    _mqttClientService = mqttClientService;
    _vectorDbService = vectorDbService;
    _embeddingService = embeddingService;
    _ollamaClient = OllamaClient;
    if(!Directory.Exists(_uploadsFolder))
      Directory.CreateDirectory(_uploadsFolder);
  }

  [HttpPost]
  [Consumes("multipart/form-data")]
  public async Task<IActionResult> UploadFile(IFormFile file) {
    if(file == null || file.Length == 0)
      return BadRequest("No file uploaded.");

    try {
      string filePath = Path.Combine("uploads", file.FileName);
      using(var stream = new FileStream(filePath, FileMode.Create)) {
        await file.CopyToAsync(stream);
      }
      var processor = new FileProcessor(filePath);
      string extractedText = processor.ExtractText();
      bool success = await _vectorDbService.AddDocument(extractedText);
      if(!success)
        return StatusCode(500, "Failed to store document in vector database.");
      var response = new {
        file.FileName,
        FileSize = file.Length,
        FileType = Path.GetExtension(file.FileName),
        ExtractedText = extractedText,
      };
      await _mqttClientService.PublishAsync("uploads/new", $"File uploaded: {file.FileName}");
      return Ok(response);
    }
    catch(Exception ex) {
      return StatusCode(500, $"Internal server error: {ex.Message}");
    }
  }

  [HttpGet("search")]
  public async Task<IActionResult> SearchDocuments([FromQuery] string query) {
    if(string.IsNullOrEmpty(query))
      return BadRequest("Query cannot be empty.");
    try {
      return Ok(await _vectorDbService.SearchDocuments(query));
    }
    catch(Exception ex) {
      return StatusCode(500, $"Internal server error: {ex.Message}");
    }
  }

  [HttpDelete("[action]")]
  public async Task<IActionResult> ClearChromaDB() {
    try {
      return Ok(await _vectorDbService.ClearChromaDB());
    }
    catch(Exception ex) {
      return StatusCode(500, $"Internal server error: {ex.Message}");
    }
  }

  [HttpGet("search-rag")]
  public async Task<string> SearchWithRAG([FromQuery] string query, [FromQuery] int topResults = 5) {
    var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
    var results = await _vectorDbService.SearchDocuments(queryEmbedding, query, topResults, includeOriginalText: true, includeOriginalDocumentText: true);
    if(results.Count == 0)
      return "No relevant documents found.";
    var context = string.Join("\n\n", results.Select(r => r.OriginalDocumentText));
    return await _ollamaClient.GenerateResponseAsync(context, query);
  }

}
