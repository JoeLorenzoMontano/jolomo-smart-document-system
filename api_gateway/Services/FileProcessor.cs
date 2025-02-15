using DocumentFormat.OpenXml.Packaging;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Tesseract;
using System.IO;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Path = System.IO.Path;

public class FileProcessor {
  private readonly string _filePath;
  private readonly string _fileType;

  public FileProcessor(string filePath) {
    _filePath = filePath;
    _fileType = Path.GetExtension(filePath).ToLower();
  }

  public string ExtractText() {
    return _fileType switch {
      ".pdf" => ExtractTextFromPdf(),
      ".docx" => ExtractTextFromDocx(),
      ".txt" => ExtractTextFromTxt(),
      ".jpg" or ".png" => ExtractTextFromImage(),
      ".html" or ".htm" => ExtractTextFromHtml(),
      _ => throw new NotSupportedException($"Unsupported file type: {_fileType}"),
    };
  }

  private string ExtractTextFromPdf() {
    using var reader = new PdfReader(_filePath);
    string text = "";

    for(int i = 1; i <= reader.NumberOfPages; i++) {
      text += PdfTextExtractor.GetTextFromPage(reader, i) + "\n";
    }
    return text;
  }

  private string ExtractTextFromDocx() {
    using var doc = WordprocessingDocument.Open(_filePath, false);
    var body = doc.MainDocumentPart.Document.Body;
    return body.InnerText;
  }

  private string ExtractTextFromTxt() {
    return File.ReadAllText(_filePath);
  }

  private string ExtractTextFromImage() {
    using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
    using var img = Pix.LoadFromFile(_filePath);
    using var page = engine.Process(img);
    return page.GetText();
  }

  private string ExtractTextFromHtml() {
    var doc = new HtmlDocument();
    doc.Load(_filePath);
    return Regex.Replace(doc.DocumentNode.InnerText, "\\s+", " ").Trim();
  }
}
