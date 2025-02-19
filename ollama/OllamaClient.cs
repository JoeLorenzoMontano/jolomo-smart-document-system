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

  public async Task<string> SummarizeTextAsync(string text, string model = "mistral:7b", int? max_tokens = null) {
    if(string.IsNullOrWhiteSpace(text))
      return "Error: No input text provided.";
    var requestBody = new {
      model,
      messages = new[]
        {
            new
            {
                role = "system",
                content = "You are an AI assistant that specializes in summarization.\n" +
                          "### 🔹 **Summarization Rules (STRICTLY FOLLOW):**\n" +
                          "1. **You MUST preserve all key details, including major and minor facts.**\n" +
                          "2. **You MUST include all relevant quotes, figures, and statistics.**\n" +
                          "3. **You MUST ensure that the meaning of the original text is not altered.**\n" +
                          "4. **DO NOT add opinions, interpretations, or external information.**\n" +
                          "5. **DO NOT remove any crucial context that affects understanding.**\n\n" +
                          "### 🔹 **Text to Summarize:**\n" +
                          $"\"\"\"\n{text}\n\"\"\""
            },
            new { role = "user", content = "Provide a detailed yet concise summary following all the above rules." }
        },
      max_tokens
    };
    var json = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    var response = await _httpClient.PostAsync($"{_ollamaUrl}/chat/completions", content);
    response.EnsureSuccessStatusCode();
    var responseContent = await response.Content.ReadAsStringAsync();
    var responseData = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
    return RemoveThinkRegions(responseData?.choices?.FirstOrDefault()?.message?.content ?? "No summary generated.");
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

  public async Task<Dictionary<string, object>> ExpandMetadata(string text, string model = "mistral:7b", int? max_tokens = null) {
    if(string.IsNullOrWhiteSpace(text))
      return new Dictionary<string, object>();
    var requestBody = new {
      model,
      messages = new[]
        {
            new
            {
                role = "system",
                content = "You are an AI assistant that expands user-provided text with additional related vocabulary and terminology.\n" +
                          "Your output will be used for matching documents in a database.\n" +
                          "Strictly follow the JSON schema format and do not include explanations or extra formatting.\n" +
                          "Property 'named_entities' should only include; individuals, businesses, entity or specific names.\n" +
                          "Property 'category' should only include the general catigorical tags that are associated to the input. " +
                          "Property 'keywords' should only include keywords directly used in the input that are critical. "
            },
            new
            {
                role = "user",
                content = $"Expand on the following user input with related terminology and vocabulary:\n\n\"\"\"\n{text}\n\"\"\""
            }
        },
      max_tokens,
      response_format = new {
        type = "json_schema",
        seed = 08142024,
        json_schema = new {
          name = "expanded_metadata",
          schema = new {
            type = "object",
            properties = new Dictionary<string, object>
                    {
                        { "original_text", new { type = "string" } },
                        { "expanded_text", new { type = "string" } },
                        { "related_terms", new { type = "string" } },
                        { "named_entities", new { type = "string" } },
                        { "category", new { type = "string" } },
                        { "keywords", new { type = "string" } },
                        { "word_count", new { type = "integer" } },
                        { "character_length", new { type = "integer" } }
                    },
            required = new[] { "original_text", "expanded_text", "related_terms", "named_entities", "category", "keywords", "word_count", "character_length" },
            additionalProperties = false
          },
          strict = true
        }
      }
    };
    var json = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    var response = await _httpClient.PostAsync($"{_ollamaUrl}/chat/completions", content);
    response.EnsureSuccessStatusCode();
    var responseContent = await response.Content.ReadAsStringAsync();
    var responseData = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
    string responseString = responseData?.choices?.FirstOrDefault()?.message?.content ?? "";
    var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(responseString);
    return NormalizeMetadata(metadata);
  }

  public async Task<Dictionary<string, object>> ExtractMetadata(string text, string model = "mistral:7b", int? max_tokens = null) {
    if(string.IsNullOrWhiteSpace(text))
      return [];
    var requestBody = new {
      model,
      messages = new[]
        {
                new
                {
                    role = "system",
                    content = "You are an AI assistant that extracts structured metadata from text. " +
                              "You must follow the defined JSON schema strictly without adding explanations or extra formatting."
                },
                new
                {
                    role = "user",
                    content = $"Extract metadata from this document:\n\n\"\"\"\n{text}\n\"\"\""
                }
            },
      max_tokens,
      response_format = new {
        type = "json_schema",
        seed = 08142024,
        json_schema = new {
          name = "document_metadata",
          schema = new {
            type = "object",
            properties = new Dictionary<string, object>
              {
                  { "named_entities", new { type = "string" } },
                  { "category", new { type = "string" } },
                  { "keywords", new { type = "string" } },
                  { "word_count", new { type = "integer" } },
                  { "character_length", new { type = "integer" } }
              },
            required = new[] { "named_entities", "category", "keywords", "word_count", "character_length" },
            additionalProperties = false
          },
          strict = true
        }
      }
    };
    var json = JsonSerializer.Serialize(requestBody);
    var content = new StringContent(json, Encoding.UTF8, "application/json");
    var response = await _httpClient.PostAsync($"{_ollamaUrl}/chat/completions", content);
    response.EnsureSuccessStatusCode();
    var responseContent = await response.Content.ReadAsStringAsync();
    var responseData = JsonSerializer.Deserialize<OllamaResponse>(responseContent);
    string responseString = responseData?.choices?.FirstOrDefault()?.message?.content ?? "";
    var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(responseString);
    return NormalizeMetadata(metadata);
  }

  private Dictionary<string, object> NormalizeMetadata(Dictionary<string, object> metadata) {
    var fieldsToConvert = new HashSet<string> { "named_entities", "keywords" };
    foreach(var field in fieldsToConvert) {
      if(metadata.ContainsKey(field) && metadata[field] is JsonElement element && element.ValueKind == JsonValueKind.Array) {
        var concatenatedValues = string.Join("|", element.EnumerateArray().Select(e => e.GetString()).Where(s => !string.IsNullOrEmpty(s)));
        metadata[field] = concatenatedValues;
      }
    }
    return metadata;
  }




  public static string RemoveThinkRegions(string input) {
    return Regex.Replace(input, @"<think>.*?</think>", "", RegexOptions.Singleline).Trim();
  }
}
