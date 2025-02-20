using ChromaDB.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class ChromaWorkerService : BackgroundService {
  private readonly ILogger<ChromaWorkerService> _logger;
  private readonly string _collectionName;
  private readonly ChromaConfigurationOptions _configOptions;
  private readonly ChromaClient _chromaClient;
  private readonly ChromaCollectionClient _collectionClient;
  private readonly TimeSpan _interval = TimeSpan.FromMinutes(30);//TODO: Consider using MQTT based triggers

  public ChromaWorkerService(ILogger<ChromaWorkerService> logger, IConfiguration configuration) {
    _logger = logger;
    _collectionName = configuration?["ChromaDB:CollectionName"] ?? "documents";
    _configOptions = new ChromaConfigurationOptions(uri: configuration?["ChromaDB:Uri"] ?? "http://localhost:8000/api/v1/");
    _chromaClient = new ChromaClient(_configOptions, new HttpClient());
    var collection = _chromaClient.GetOrCreateCollection(_collectionName).Result;
    _collectionClient = new ChromaCollectionClient(collection, _configOptions, new HttpClient());
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    _logger.LogInformation("ChromaWorkerService started.");
    while(!stoppingToken.IsCancellationRequested) {
      try {
        _logger.LogInformation("Checking ChromaDB for documents...");
        var documents = await _collectionClient.Get(include: ChromaGetInclude.Documents | ChromaGetInclude.Metadatas);
        foreach(var doc in documents) {
          _logger.LogInformation($"Processing document: {doc.Id}");
          // TODO: Add logic to process each document
        }
      }
      catch(Exception ex) {
        _logger.LogError($"Error in ChromaWorkerService: {ex.Message}");
      }
      _logger.LogInformation($"Worker sleeping for {_interval.TotalMinutes} minutes...");
      await Task.Delay(_interval, stoppingToken);
    }
  }

  public override Task StopAsync(CancellationToken cancellationToken) {
    _logger.LogInformation("ChromaWorkerService is stopping.");
    return base.StopAsync(cancellationToken);
  }
}
