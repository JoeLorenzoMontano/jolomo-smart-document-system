public class EmbeddingConfig {
  public int ChunkSize { get; set; } = 512;
  public int Overlap { get; set; } = 50;
  public ChunkMethod ChunkingMethod = ChunkMethod.Newline;
}

public enum ChunkMethod {
  Word,
  Paragraph,
  Newline
}
