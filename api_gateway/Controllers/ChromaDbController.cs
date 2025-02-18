using ChromaDB.Client;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

[ApiController]
[Route("api/chromadb")]
public class ChromaDbController : ControllerBase {
  private readonly ChromaClient _chromaClient;
  private readonly ChromaCollectionClient _collectionClient;
  private readonly string _collectionName = "documents";

  public ChromaDbController() {
    var configOptions = new ChromaConfigurationOptions(uri: "http://localhost:8000/api/v1/");
    _chromaClient = new ChromaClient(configOptions, new HttpClient());

    // Ensure the collection exists or create it
    var collection = _chromaClient.GetOrCreateCollection(_collectionName).Result;
    _collectionClient = new ChromaCollectionClient(collection, configOptions, new HttpClient());
  }

  /// <summary>
  /// Retrieve all documents from ChromaDB.
  /// </summary>
  [HttpGet("all")]
  public async Task<IActionResult> GetAllDocuments() {
    var allDocs = await _collectionClient.Get(include: ChromaGetInclude.Documents | ChromaGetInclude.Metadatas);
    return Ok(allDocs);
  }

  /// <summary>
  /// Delete a collection (clear ChromaDB).
  /// </summary>
  [HttpDelete("clear")]
  public async Task<IActionResult> ClearCollection() {
    await _chromaClient.DeleteCollection(_collectionName);
    return Ok(new { message = "ChromaDB collection cleared." });
  }



  public class SearchResult {
    public string Id { get; set; }
    public string Text { get; set; }
    public float Distance { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
  }
}
