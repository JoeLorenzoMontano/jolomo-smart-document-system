using Nest;
using System.Xml.Linq;

public class DocumentSearchResult {
  public float Distance { get; set; }
  public string? OriginalDocumentId { get; set; }
  public string? OriginalChunkDocumentId { get; set; }
  public Dictionary<string, object>? OriginalDocumentMetadata { get; set; }
  public RagDataType DataType { get; set; }
  public double Score { get; set; }

  [Text(Name = "id")]
  public string Id { get; set; }

  [Text(Name = "original_text")]
  public string OriginalText { get; set; }

  [Text(Name = "original_document_text")]
  public string OriginalDocumentText { get; set; }

  [Object(Name = "metadata")]
  public Dictionary<string, object> Metadatas { get; set; }

}

public enum RagDataType {
  SourceDocument,
  Chunk,
  ChunkSummary,
  ChunkQuesiton
}