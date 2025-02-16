using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.IO;

public class VectorDbServiceTests {
  private readonly Mock<ILocalEmbeddingService> _embeddingServiceMock;
  private readonly IConfiguration _configuration;
  private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
  private readonly VectorDbService _vectorDbService;

  public VectorDbServiceTests() {
    _embeddingServiceMock = new Mock<ILocalEmbeddingService>();
    _httpClientFactoryMock = new Mock<IHttpClientFactory>();
    var httpClient = new HttpClient();
    _httpClientFactoryMock.Setup(factory => factory.CreateClient(It.IsAny<string>())).Returns(httpClient);
    _vectorDbService = new VectorDbService(_embeddingServiceMock.Object, _configuration, _httpClientFactoryMock.Object);
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