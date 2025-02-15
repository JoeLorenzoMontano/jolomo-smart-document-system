public interface ILocalEmbeddingService {
  Task<float[]> GenerateEmbeddingAsync(string text);
}
