public class OpenAiEmbeddingResponse {
  public string _object { get; set; }
  public Datum[] data { get; set; }
  public string model { get; set; }
  public Usage usage { get; set; }

  public class Usage {
    public int prompt_tokens { get; set; }
    public int total_tokens { get; set; }
  }

  public class Datum {
    public string _object { get; set; }
    public int index { get; set; }
    public float[] embedding { get; set; }
  }
}
