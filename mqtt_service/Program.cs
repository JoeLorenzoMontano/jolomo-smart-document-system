class Program {
  static async Task Main(string[] args) {
    var broker = new MqttBrokerService();

    Console.CancelKeyPress += async (sender, e) =>
    {
      e.Cancel = true;
      await broker.StopAsync();
    };

    await broker.StartAsync();

    Console.WriteLine("[MQTT Broker] Running... Press Ctrl+C to stop.");
    await Task.Delay(-1);  // Keeps the application alive indefinitely
  }
}
