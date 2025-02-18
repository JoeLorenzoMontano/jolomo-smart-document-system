public class RAGResponse {
  public string Query { get; set; }
  public List<SearchResult> RetrievedDocuments { get; set; }
  public string LLMResponse { get; set; }
}
