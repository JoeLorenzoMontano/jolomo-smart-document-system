/// <summary>
/// Model representing a stored text chunk in Elasticsearch.
/// </summary>
public class ElasticSearchDocument {
  public string Id { get; set; }
  public string Content { get; set; }
  public Dictionary<string, object>? Metadata { get; set; } = new();
}
