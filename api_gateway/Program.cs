using api_gateway.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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

// Register Vector Database Service
builder.Services.AddSingleton<VectorDbService>(); // Inject Vector DB service

var app = builder.Build();

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
