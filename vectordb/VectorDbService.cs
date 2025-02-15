using System.Text;
using System.Text.Json;

public class VectorDbService {
  private readonly HttpClient _httpClient;
  private readonly string _chromaDbUrl = "http://localhost:8000/api/v1"; // ChromaDB URL

  public VectorDbService() {
    _httpClient = new HttpClient();
  }

  public async Task<bool> AddDocument(string documentId, string text) {
    var requestBody = new {
      collection_name = "documents",
      documents = new[]
        {
                new
                {
                    id = documentId,
                    content = text
                }
            }
    };

    var json = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    var response = await _httpClient.PostAsync($"{_chromaDbUrl}/collections", content);
    return response.IsSuccessStatusCode;
  }

  public async Task<string> SearchDocuments(string query) {
    var requestBody = new {
      collection_name = "documents",
      query = query,
      n_results = 5
    };

    var json = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    var response = await _httpClient.PostAsync($"{_chromaDbUrl}/search", content);
    return await response.Content.ReadAsStringAsync();
  }
}
