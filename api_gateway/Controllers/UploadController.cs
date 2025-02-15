using Microsoft.AspNetCore.Mvc;

[Route("api/upload")]
[ApiController]
public class UploadController : ControllerBase {
  private readonly string _uploadsFolder = "uploads/";
  private readonly MqttClientService _mqttClientService;
  private readonly VectorDbService _vectorDbService;

  public UploadController(MqttClientService mqttClientService, VectorDbService vectorDbService) {
    _mqttClientService = mqttClientService;
    _vectorDbService = vectorDbService;
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
      bool success = await _vectorDbService.AddDocument(Guid.NewGuid().ToString(), extractedText);
      if(!success)
        return StatusCode(500, "Failed to store document in vector database.");
      var response = new {
        file.FileName,
        FileSize = file.Length,
        FileType = Path.GetExtension(file.FileName),
        ExtractedText = extractedText
      };
      await _mqttClientService.PublishAsync("uploads/new", $"File uploaded: {file.FileName}");
      //await _mqttClientService.PublishAsync("uploads/status", $"Processing {file.FileName}");
      return Ok(response);
    }
    catch(Exception ex) {
      return StatusCode(500, $"Internal server error: {ex.Message}");
    }
  }

}