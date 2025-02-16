public class SearchResult {
  public string Id { get; set; }
  public float Distance { get; set; }
  public string? OriginalText { get; internal set; }
  public string? OriginalDocumentId { get; set; }
  public string? OriginalDocumentText { get; internal set; }
}