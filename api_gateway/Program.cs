﻿using api_gateway.Services;
using ChromaDB.Client;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.OpenApi.Models;
using Nest;

var builder = WebApplication.CreateBuilder(args);

// Allow CORS for requests from the Angular frontend
builder.Services.AddCors(options => {
  options.AddPolicy("AllowAngular",
      policy => policy
          .WithOrigins("http://localhost:49904")
          .AllowAnyMethod()
          .AllowAnyHeader()
          .AllowCredentials());
});

// Add services to the container.
builder.Services.AddControllers();

// Configure Swagger to allow file uploads
builder.Services.AddSwaggerGen(c => {
  c.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });
  c.OperationFilter<FileUploadOperation>(); // Enables file upload support
});

// Register MQTT Services (Only as a client)
builder.Services.AddSingleton(provider => {
  var config = provider.GetRequiredService<IConfiguration>();
  return new MqttClientService(
      config["MqttSettings:BrokerHost"],
      int.Parse(config["MqttSettings:BrokerPort"]),
      config["MqttSettings:ClientId"]
  );
});

// Register ElasticSearch Client
builder.Services.AddSingleton<IElasticClient>(sp => {
  var settings = new ConnectionSettings(new Uri("http://localhost:9200"))
      .DefaultIndex("documents"); // Default index
  return new ElasticClient(settings);
});

// Register ElasticSearchService as a singleton
builder.Services.AddSingleton<ElasticSearchService>();

// Register VectorDbService and inject ILocalEmbeddingService
builder.Services.AddSingleton<RedisCacheService>();
// Register Vector Database Service
builder.Services.AddSingleton<VectorDbService>();
// Register ILocalEmbeddingService 
builder.Services.AddSingleton<ILocalEmbeddingService, OnnxEmbeddingService>();
// Register hosted ChromaWorkerService for VectorDB logic that runs on interval, might add MQTT trigger down the road.
builder.Services.AddHostedService<ChromaWorkerService>();
// Register helper classes
builder.Services.AddScoped<SearchHelpers>();
// Register 
builder.Services.AddScoped<RerankerService>();

// Add Configs
builder.Services.AddOptions();
builder.Services.Configure<ChromaConfigurationOptions>(builder.Configuration.GetSection("ChromaDB"));

// Register OllamaClient
builder.Services.AddHttpClient<OllamaClient>();

var app = builder.Build();

// Use CORS before mapping controllers
app.UseCors("AllowAngular");

// Start MQTT Client (Subscribe to a test topic)
var mqttClientService = app.Services.GetRequiredService<MqttClientService>();
await mqttClientService.ConnectAsync();
await mqttClientService.SubscribeAsync("uploads/new");

// Configure the HTTP request pipeline.
if(app.Environment.IsDevelopment()) {
  app.UseDeveloperExceptionPage();
  app.UseSwagger();
  app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
