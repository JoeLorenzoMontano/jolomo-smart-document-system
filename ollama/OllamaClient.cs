using System.Text;
using System.Text.Json;

public class OllamaClient {
  private readonly HttpClient _httpClient;
  private readonly string _ollamaUrl = "http://localhost:11434/v1/chat/completions";

  public OllamaClient(HttpClient httpClient) {
    _httpClient = httpClient;
  }

  public async Task<string> GenerateResponseAsync(string context, string query, string model = "deepseek-r1:8b") {
    var requestBody = new {
      model,
      messages = new[]
        {
            new
            {
                role = "system",
                content = "You are an AI assistant specializing in providing accurate responses based **only on the provided context**.\n" +
                          "### Instructions:\n" +
                          "- If the answer is found in the context, provide a clear, structured response.\n" +
                          "- If the information is **not available in the context**, reply with:\n" +
                          "  *'I could not find relevant information in the provided context. Please provide additional details if needed.'*\n" +
                          "- Do **not** generate an answer using external knowledge.\n" +
                          "- Do **not** make up information beyond the given context.\n\n" +
                          "### Context:\n" +
                          $"\"\"\"\n{context}\n\"\"\""
            },
            new { role = "user", content = $"Query: {query}" }
        },
      stream = false
    };

    var json = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    var response = await _httpClient.PostAsync(_ollamaUrl, content);
    response.EnsureSuccessStatusCode();
    var responseContent = await response.Content.ReadAsStringAsync();
    var responseData = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
    return responseData?.choices?.FirstOrDefault()?.message?.content ?? "No response generated.";
  }

}


