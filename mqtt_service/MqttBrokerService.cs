using MQTTnet.Server;

public class MqttBrokerService {
  private readonly MqttServer _mqttServer;

  public MqttBrokerService() {
    var factory = new MqttServerFactory();
    var options = new MqttServerOptionsBuilder()
        .WithDefaultEndpoint()
        .WithDefaultEndpointPort(1883)  // Local broker listens on port 1883
        .Build();
    _mqttServer = factory.CreateMqttServer(options);
  }

  public async Task StartAsync() {
    _mqttServer.ApplicationMessageEnqueuedOrDroppedAsync += async e =>
    {
      Console.WriteLine($"[Broker] Message received on topic: {e.ApplicationMessage.Topic}");
      //Console.WriteLine($"[Broker] Payload: {Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment)}");
      await Task.CompletedTask;
    };

    await _mqttServer.StartAsync();
    Console.WriteLine("[MQTT Broker] Started on port 1883.");
  }

  public async Task StopAsync() {
    await _mqttServer.StopAsync();
    Console.WriteLine("[MQTT Broker] Stopped.");
  }
}
