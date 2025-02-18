using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

public class OllamaClient {
  private readonly HttpClient _httpClient;
  private readonly string _ollamaUrl = "http://localhost:11434/v1";

  public OllamaClient(HttpClient httpClient) {
    _httpClient = httpClient;
    _httpClient.Timeout = Timeout.InfiniteTimeSpan;
  }

  public async Task<string> GenerateResponseAsync(string context, string query, string model = "deepseek-r1:8b", int? max_tokens = null) {
    var requestBody = new {
      model,
      messages = new[]
        {
            new
            {
                role = "system",
                content = "You are an AI assistant that must follow strict response rules.\n" +
                  "### 🔹 **Rules (MUST FOLLOW):**\n" +
                  "1 **You MUST ONLY use information from the provided context.**\n" +
                  "2 **If the answer is NOT in the context, respond with:**\n" +
                  "   ❌ 'I could not find relevant information in the provided context. Please provide additional details if needed.'\n" +
                  "3 **You MUST NOT generate an answer using external knowledge.**\n" +
                  "4 **You MUST NOT make up any information.**\n\n" +
                  "### 🔹 **Context (ONLY use the information provided below to answer the query):**\n" +
                  $"\"\"\"\n{context}\n\"\"\"\n\n"
            },
            new { role = "user", content = $"Query: {query}" }
        },
      stream = false,
      max_tokens
    };
    var json = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    var response = await _httpClient.PostAsync($"{_ollamaUrl}/chat/completions", content);
    response.EnsureSuccessStatusCode();
    var responseContent = await response.Content.ReadAsStringAsync();
    var responseData = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
    if(responseData?.choices.Any() ?? false) {
      responseData.choices[0].message.content = RemoveThinkRegions(responseData.choices[0].message.content);//TODO: Make optional
    }
    return responseData?.choices?.FirstOrDefault()?.message?.content ?? "No response generated.";
  }

  public async Task<float[]> GenerateEmbeddingAsync(string input) {
    var requestBody = new {
      model = "text-embedding-ada-002",
      input = new[] { input }
    };
    var requestJson = JsonSerializer.Serialize(requestBody);
    var requestContent = new StringContent(requestJson, Encoding.UTF8, "application/json");
    var response = await _httpClient.PostAsync($"{_ollamaUrl}/embeddings", requestContent);
    response.EnsureSuccessStatusCode();
    var responseContent = await response.Content.ReadAsStringAsync();
    var responseData = JsonSerializer.Deserialize<OpenAiEmbeddingResponse>(responseContent);
    return responseData?.data?.FirstOrDefault()?.embedding ?? [];
  }

  public async Task<List<string>> GenerateQueryVariations(string query, string model = "chnaaam/santa-keyword-extractor", int? max_tokens = null) {
    var requestBody = new {
      model,
      messages = new[]
        {
            new
            {
                role = "system",
                    content = "You are a query optimization assistant. Your task is to extract the most relevant keywords and keyword phrases from the provided query and add additional relevant words to enhance search recall.\n" +
                      "### Instructions:\n" +
                      "- Extract the most **important words and key phrases** from the user's query.\n" +
                      "- Identify **related and contextually relevant words** that might help broaden the search results.\n" +
                      "- Output them as a **comma-separated list**.\n" +
                      "- Do **not** generate an explanation, just return the keywords.\n"
            },
            new { role = "user", content = $"Extract keywords from this query: \"{query}\"" }
        },
      stream = false,
      max_tokens
    };
    var json = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    var response = await _httpClient.PostAsync($"{_ollamaUrl}/chat/completions", content);
    response.EnsureSuccessStatusCode();
    var responseContent = await response.Content.ReadAsStringAsync();
    var responseData = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
    string keywordString = responseData?.choices?.FirstOrDefault()?.message?.content ?? "";
    keywordString = RemoveThinkRegions(keywordString);//TODO: Make optional
    var keywords = keywordString.Split(',').Select(k => k.Trim()).Where(k => !string.IsNullOrEmpty(k)).ToList();
    // Add original query to ensure it's searched as well
    var queries = new List<string> { query };
    // Add individual keywords
    queries.AddRange(keywords);
    // Generate bigram keyword combinations (for phrase-based search)
    for(int i = 0; i < keywords.Count - 1; i++) {
      queries.Add($"{keywords[i]} {keywords[i + 1]}");
    }
    return queries.Distinct().ToList();
  }

  public async Task<string> ExtractMetadata(string text, string model = "chnaaam/santa-keyword-extractor", int? max_tokens = null) {
    if(string.IsNullOrWhiteSpace(text))
      return "";
    var requestBody = new {
      model,
      messages = new[]
        {
                new
                {
                    role = "system",
                    content = "You are an AI metadata extraction assistant. Your task is to analyze the given text and extract relevant metadata in a structured format. " +
                              "\n\nRules:\n" +
                              "1. Identify key entities (e.g., people, places, topics, keywords).\n" +
                              "2. Extract metadata fields such as word count, character count, and topics covered.\n" +
                              "3. Format the output as a **JSON object** with clearly defined keys.\n" +
                              "4. Only return structured data, do not include explanations or additional text.\n"
                },
                new
                {
                    role = "user",
                    content = $"Extract metadata from this document:\n\n\"\"\"\n{text}\n\"\"\""
                }
            },
      max_tokens
    };
    var json = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    var response = await _httpClient.PostAsync($"{_ollamaUrl}/chat/completions", content);
    response.EnsureSuccessStatusCode();
    var responseContent = await response.Content.ReadAsStringAsync();
    var responseData = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
    string responseString = responseData?.choices?.FirstOrDefault()?.message?.content ?? "";
    return RemoveThinkRegions(responseString);//TODO: Make optional
    //var responseData = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent);
    //return responseData ?? new Dictionary<string, object>();
  }




  public static string RemoveThinkRegions(string input) {
    return Regex.Replace(input, @"<think>.*?</think>", "", RegexOptions.Singleline).Trim();
  }
}
