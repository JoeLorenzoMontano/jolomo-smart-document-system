using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public class OnnxEmbeddingService : ILocalEmbeddingService {
  private readonly InferenceSession _session;
  private const int MaxSequenceLength = 512; // ✅ Ensure correct input size

  public OnnxEmbeddingService() {
    _session = new InferenceSession("models/miniLM.onnx"); // Load ONNX model
  }

  public async Task<float[]> GenerateEmbeddingAsync(string text) {
    return await Task.Run(() => {
      var tokens = TokenizeText(text);

      // Fix: Ensure exactly 512 tokens (pad/truncate as needed)
      var paddedTokens = new long[MaxSequenceLength];
      var attentionMask = new long[MaxSequenceLength];
      var tokenTypeIds = new long[MaxSequenceLength];

      int length = Math.Min(tokens.Count, MaxSequenceLength);
      for(int i = 0; i < length; i++) {
        paddedTokens[i] = tokens[i]; // Assign token values
        attentionMask[i] = 1;        // Enable attention for real tokens
        tokenTypeIds[i] = 0;         // Sentence type (if needed)
      }

      // Use the correct DenseTensor constructor
      var inputTensor = new DenseTensor<long>(paddedTokens, new[] { 1, MaxSequenceLength });
      var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, MaxSequenceLength });
      var tokenTypeIdsTensor = new DenseTensor<long>(tokenTypeIds, new[] { 1, MaxSequenceLength });

      var inputs = new NamedOnnxValue[] {
                NamedOnnxValue.CreateFromTensor("input_ids", inputTensor),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
            };

      using var results = _session.Run(inputs);
      return results.First().AsEnumerable<float>().ToArray(); // Return embedding
    });
  }

  private List<int> TokenizeText(string text) {
    text = Regex.Replace(text.ToLower(), @"\W+", " "); // Remove punctuation
    return Encoding.UTF8.GetBytes(text).Select(b => (int)b).ToList(); // Simple UTF-8 encoding
  }
}
