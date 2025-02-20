public class SearchResult {
  public string Id { get; set; }
  public float Distance { get; set; }
  public string? OriginalText { get; internal set; }
  public string? OriginalDocumentId { get; set; }
  public string? OriginalChunkDocumentId { get; set; }
  public string? OriginalDocumentText { get; internal set; }
  public Dictionary<string, object>? OriginalDocumentMetadata { get; set; }
  public RagDataType DataType { get; set; }
}

public enum RagDataType {
  SourceDocument,
  Chunk,
  ChunkSummary,
  ChunkQuesiton
}