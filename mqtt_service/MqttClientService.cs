using MQTTnet;
using System.Text;

public class MqttClientService {
  private readonly IMqttClient _mqttClient;
  private readonly MqttClientOptions _options;

  public MqttClientService(string brokerHost, int brokerPort, string clientId) {
    var factory = new MqttClientFactory();
    _mqttClient = factory.CreateMqttClient();

    _options = new MqttClientOptionsBuilder()
        .WithTcpServer(brokerHost, brokerPort)
        .WithClientId(clientId)
        .Build();
  }

  public async Task ConnectAsync() {
    if(!_mqttClient.IsConnected) {
      await _mqttClient.ConnectAsync(_options, CancellationToken.None);
      Console.WriteLine("MQTT Connected.");
    }
  }

  public async Task PublishAsync(string topic, string message) {
    if(!_mqttClient.IsConnected)
      await ConnectAsync();

    var mqttMessage = new MqttApplicationMessageBuilder()
        .WithTopic(topic)
        .WithPayload(Encoding.UTF8.GetBytes(message))
        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
        .WithRetainFlag(false)
        .Build();

    await _mqttClient.PublishAsync(mqttMessage, CancellationToken.None);
    Console.WriteLine($"Published to MQTT: {topic} -> {message}");
  }

  public async Task SubscribeAsync(string topic) {
    if(!_mqttClient.IsConnected)
      await ConnectAsync();

    await _mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(topic).Build());

    _mqttClient.ApplicationMessageReceivedAsync += async e =>
    {
      Console.WriteLine($"[MQTT Client] Received message on topic {e.ApplicationMessage.Topic}:");
      //Console.WriteLine($"[MQTT Client] {Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment)}");
      await Task.CompletedTask;
    };

    Console.WriteLine($"[MQTT Client] Subscribed to {topic}");
  }
}
