using System.Text;
using System.Text.Json;

public class OllamaClient {
  private readonly HttpClient _httpClient;
  private readonly string _ollamaUrl = "http://localhost:11434/v1/chat/completions";

  public OllamaClient(HttpClient httpClient) {
    _httpClient = httpClient;
  }

  public async Task<string> GenerateResponseAsync(string context, string query, string model = "llama3:latest") {
    var requestBody = new {
      model = model,
      messages = new[] {
        new { role = "system", content = $"You are an AI assistant. " + 
        "Use the provided context to answer the user’s question. " +
        "Do not use any other context and if you dont know the answer based on the provided context then state 'based on my knowledge bank I am not sure'. " + 
        $"[[CONTEXT]]: {context}" },
        new { role = "user", content = $"Query: {query}" }
      },
      stream = false
    };
    var json = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    var response = await _httpClient.PostAsync(_ollamaUrl, content);
    response.EnsureSuccessStatusCode();
    var responseContent = await response.Content.ReadAsStringAsync();
    var responseData = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
    return responseData?.choices?.FirstOrDefault()?.message?.content ?? "No response generated.";
  }
}


