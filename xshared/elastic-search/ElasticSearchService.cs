using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class ElasticSearchService {
  private readonly IElasticClient _elasticClient;
  private const string IndexName = "text_chunks";

  public ElasticSearchService(IElasticClient elasticClient) {
    _elasticClient = elasticClient;
  }

  /// <summary>
  /// Ensure the Elasticsearch index exists, with custom mapping for metadata.
  /// </summary>
  public async Task EnsureIndexExistsAsync() {
    var existsResponse = await _elasticClient.Indices.ExistsAsync(IndexName);
    if(!existsResponse.Exists) {
      var createIndexResponse = await _elasticClient.Indices.CreateAsync(IndexName, c => c
          .Map<ElasticSearchDocument>(m => m
              .AutoMap()
              .Properties(ps => ps
                  .Text(t => t.Name(tc => tc.Content))
                  .Object<Dictionary<string, object>>(o => o.Name(tc => tc.Metadata))
              )
          )
      );
    }
  }

  /// <summary>
  /// Index a single text chunk into Elasticsearch, with optional metadata.
  /// </summary>
  public async Task<bool> IndexTextChunkAsync(string text, Dictionary<string, object>? metadata = null) {
    var chunk = new ElasticSearchDocument {
      Id = Guid.NewGuid().ToString(),
      Content = text,
      Metadata = metadata ?? new Dictionary<string, object>()
    };
    var response = await _elasticClient.IndexAsync(chunk, i => i.Index(IndexName));
    return response.IsValid;
  }

  /// <summary>
  /// Bulk index multiple text chunks with metadata into Elasticsearch.
  /// </summary>
  public async Task<bool> BulkIndexTextChunksAsync(List<(string text, Dictionary<string, object>? metadata)> chunks) {
    var bulkRequest = new BulkDescriptor();
    foreach(var chunk in chunks) {
      bulkRequest.Index<ElasticSearchDocument>(op => op
          .Index(IndexName)
          .Document(new ElasticSearchDocument {
            Id = Guid.NewGuid().ToString(),
            Content = chunk.text,
            Metadata = chunk.metadata ?? new Dictionary<string, object>()
          })
      );
    }
    var response = await _elasticClient.BulkAsync(bulkRequest);
    return response.IsValid;
  }

  /// <summary>
  /// Search text chunks by query with optional metadata filtering.
  /// </summary>
  public async Task<List<ElasticSearchDocument>> SearchTextChunksAsync(string query, Dictionary<string, object>? filters = null, int topResults = 10) {
    var searchDescriptor = new SearchDescriptor<ElasticSearchDocument>()
        .Index(IndexName)
        .Query(q => q
            .Bool(b => b
                .Must(mu => mu.Match(m => m.Field(tc => tc.Content).Query(query).Fuzziness(Fuzziness.Auto)))
                .Filter(filters?.Select(f => (QueryContainer)new TermQuery { Field = $"metadata.{f.Key}", Value = f.Value }).ToArray())
            )
        )
        .Size(topResults);

    var response = await _elasticClient.SearchAsync<ElasticSearchDocument>(searchDescriptor);
    return response.Documents.ToList();
  }

  /// <summary>
  /// Delete a document from Elasticsearch by ID.
  /// </summary>
  public async Task<bool> DeleteTextChunkAsync(string documentId) {
    var response = await _elasticClient.DeleteAsync<ElasticSearchDocument>(documentId, d => d.Index(IndexName));
    return response.IsValid;
  }

  /// <summary>
  /// Check if the Elasticsearch index exists.
  /// </summary>
  public async Task<bool> IndexExistsAsync() {
    var existsResponse = await _elasticClient.Indices.ExistsAsync(IndexName);
    return existsResponse.Exists;
  }
}
