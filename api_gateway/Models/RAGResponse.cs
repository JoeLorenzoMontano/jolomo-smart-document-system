public class RAGResponse {
  public string? Query { get; set; }
  public List<DocumentSearchResult>? RetrievedDocuments { get; set; }
  public string? LLMResponse { get; set; }
  public string? PromptContext { get; set; }
}
