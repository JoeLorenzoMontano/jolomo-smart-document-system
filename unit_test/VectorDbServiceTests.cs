using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.IO;
using Moq.Protected;
using System.Net;

public class VectorDbServiceTests {
  private readonly Mock<ILocalEmbeddingService> _embeddingServiceMock;
  private readonly IConfiguration _configuration;
  private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
  private readonly VectorDbService _vectorDbService;
  private readonly Mock<RedisCacheService> _redisCacheServiceMock;
  private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
  private readonly HttpClient _httpClient;

  public VectorDbServiceTests() {
    _embeddingServiceMock = new Mock<ILocalEmbeddingService>();
    _httpClientFactoryMock = new Mock<IHttpClientFactory>();
    _redisCacheServiceMock = new Mock<RedisCacheService>();
    _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
    _httpMessageHandlerMock.Protected()
        .Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        )
        .ReturnsAsync(() => {
          return new HttpResponseMessage {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{\"result\":\"success\"}")
          };
        });
    _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
    _httpClientFactoryMock.Setup(factory => factory.CreateClient(It.IsAny<string>())).Returns(_httpClient);
    _vectorDbService = new VectorDbService(_embeddingServiceMock.Object, _configuration, _httpClientFactoryMock.Object, _redisCacheServiceMock.Object);
  }

  [Fact]
  public async Task AddDocument_ShouldReturnTrue_WhenDocumentIsAddedSuccessfully() {
    var text = "Sample document text";
    _embeddingServiceMock.Setup(service => service.GenerateEmbeddingAsync(It.IsAny<string>()))
                         .ReturnsAsync(new float[1024]);//Expected size of embedding is 1024
    var result = await _vectorDbService.AddDocument(text);
    Assert.True(result);
  }

  [Fact]
  public void ChunkText_ShouldReturnCorrectChunks_ForParagraphChunking() {
    var text = "Paragraph 1.\n\nParagraph 2.";
    var config = new EmbeddingConfig { ChunkingMethod = ChunkMethod.Paragraph };
    var chunks = _vectorDbService.ChunkText(text, config);
    Assert.Equal(2, chunks.Count);
    Assert.Contains("Paragraph 1.", chunks);
    Assert.Contains("Paragraph 2.", chunks);
  }
}