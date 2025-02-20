using Microsoft.Extensions.Logging;
using System.Text.Json;

public class RerankerService {
  private readonly OllamaClient _ollamaClient;
  private readonly RedisCacheService _cacheService;
  private readonly ILogger<RerankerService> _logger;

  public RerankerService(OllamaClient ollamaClient, RedisCacheService cacheService, ILogger<RerankerService> logger) {
    _ollamaClient = ollamaClient;
    _cacheService = cacheService;
    _logger = logger;
  }

  public async Task<List<DocumentSearchResult>> RerankDocuments(
      List<DocumentSearchResult> vectorDbResults,
      List<ElasticSearchDocument> keywordResults, // Updated to use TextChunk
      string query,
      int topResults = 10,
      bool enableLLMRerank = true) {

    var cacheKey = $"rerank:{query}:{topResults}";
    var cachedResults = await _cacheService.GetAsync(cacheKey);
    if(!string.IsNullOrEmpty(cachedResults)) {
      return JsonSerializer.Deserialize<List<DocumentSearchResult>>(cachedResults) ?? [];
    }

    // 🔹 Step 1: Convert TextChunks to DocumentSearchResult for uniform handling
    var keywordResultsConverted = keywordResults.Select(chunk => new DocumentSearchResult {
      Id = chunk.Id,
      OriginalText = chunk.Content,
      Score = 0.5 // Give an initial score to keyword-based matches
    }).ToList();

    // 🔹 Step 2: Merge vector DB and keyword-based results
    var combinedResults = MergeResults(vectorDbResults, keywordResultsConverted);

    // 🔹 Step 3: Score documents using hybrid weighting
    var scoredResults = combinedResults
        .Select(doc => {
          doc.Score = ComputeHybridScore(doc, query);
          return doc;
        })
        .OrderByDescending(doc => doc.Score)
        .Take(topResults)
        .ToList();

    // 🔹 Step 4: Optional LLM-based reranking
    if(enableLLMRerank) {
      scoredResults = await LLMRerank(scoredResults, query);
    }

    await _cacheService.SetAsync(cacheKey, JsonSerializer.Serialize(scoredResults));
    return scoredResults;
  }

  private List<DocumentSearchResult> MergeResults(List<DocumentSearchResult> vectorDbResults, List<DocumentSearchResult> keywordResults) {
    var merged = new Dictionary<string, DocumentSearchResult>();

    foreach(var result in vectorDbResults) {
      if(!merged.ContainsKey(result.Id)) {
        merged[result.Id] = result;
      }
    }

    foreach(var result in keywordResults) {
      if(merged.TryGetValue(result.Id, out var existing)) {
        // Merge scores if already in the list
        existing.Score = Math.Max(existing.Score, result.Score);
      }
      else {
        merged[result.Id] = result;
      }
    }

    return merged.Values.ToList();
  }

  private double ComputeHybridScore(DocumentSearchResult result, string query) {
    double vectorScore = 1.0 - result.Distance; // Convert distance to similarity
    double keywordBoost = 0.3 * ComputeKeywordMatchScore(result, query);
    double metadataScore = 0.2 * ComputeMetadataRelevance(result, query);

    return vectorScore + keywordBoost + metadataScore;
  }

  private double ComputeKeywordMatchScore(DocumentSearchResult result, string query) {
    return query.Split(' ').Count(word => result.OriginalText?.Contains(word, StringComparison.OrdinalIgnoreCase) ?? false);
  }

  private double ComputeMetadataRelevance(DocumentSearchResult result, string query) {
    if(result.Metadatas == null) return 0;
    return result.Metadatas.Values.Any(value => value.ToString()?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ? 1.0 : 0.0;
  }

  private async Task<List<DocumentSearchResult>> LLMRerank(List<DocumentSearchResult> results, string query) {
    var llmQuery = $"Re-rank the following search results based on relevance to the query: \"{query}\".\n\n";
    llmQuery += string.Join("\n\n", results.Select((r, i) => $"{i + 1}. {r.OriginalText}"));

    var rankedOrder = await _ollamaClient.GenerateRankingOrderAsync(llmQuery);

    return results
        .OrderBy(r => rankedOrder.IndexOf(r.Id))
        .ToList();
  }
}
