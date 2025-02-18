using System.Text.RegularExpressions;

public class TfidfEmbeddingService : ILocalEmbeddingService {
  private readonly Dictionary<string, int> _vocab;
  private readonly int _vectorSize;

  public TfidfEmbeddingService() {
    _vocab = new Dictionary<string, int>();
    _vectorSize = 1024; // You can adjust this value
  }

  public async Task<float[]> GenerateEmbeddingAsync(string text) {
    return await Task.Run(() => {
      var tokens = TokenizeText(text);
      var vector = new float[_vectorSize];
      foreach(var token in tokens) {
        if(!_vocab.ContainsKey(token))
          _vocab[token] = _vocab.Count % _vectorSize;
        vector[_vocab[token]] += 1;
      }
      return Normalize(vector);
    });
  }

  private List<string> TokenizeText(string text) {
    return Regex.Split(text.ToLower(), @"\W+")
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();
  }

  private float[] Normalize(float[] vector) {
    float sum = (float)Math.Sqrt(vector.Sum(v => v * v));
    return sum == 0 ? vector : vector.Select(v => v / sum).ToArray();
  }
}
