# AI-Powered Document Processing API Gateway

## Overview
This repository contains an API Gateway designed for **file uploads, real-time messaging via MQTT, and future AI-powered enhancements**. The goal is to create a **modular and extensible document processing system** that will incorporate **LLMs, RAG (Retrieval-Augmented Generation), and intelligent automation** over time.

## Features
- **File Upload API** - Supports document uploads with **text extraction**.
- **MQTT Integration** - Publishes events on uploads and allows real-time messaging.
- **Embedded MQTT Broker** - A local broker for handling internal messaging.
- **Swagger UI** - API documentation with file upload support.
- **Extensible Design** - Future integration with LLMs and knowledge retrieval systems.

## Future Enhancements
This project will evolve to include:
- **LLM Integration** - Use **Ollama** for document summarization and Q&A.
- **RAG (Retrieval-Augmented Generation)** - Implement **vector databases** for enhanced AI retrieval.
- **Multi-Modal Data Handling** - Support for **text, images, PDFs, and structured data**.
- **Event-Driven Processing** - Real-time document analysis using MQTT triggers.
- **User Authentication & Authorization** - Secure API access with JWT/OAuth.
- **Cloud & On-Prem Deployment** - Containerized services with **Docker & Kubernetes**.

## Project Structure
```
📂 YourSolution
│── 📂 api_gateway
│   ├── UploadController.cs  # Handles file uploads & MQTT publishing
│   ├── Program.cs           # Configures API & MQTT client
│   ├── appsettings.json     # Stores MQTT & API settings
│── 📂 MqttService
│   ├── MqttBrokerService.cs # Runs embedded MQTT broker
│   ├── MqttClientService.cs # Connects to broker & handles messages
│   ├── Program.cs           # Starts the MQTT broker when launched
```

## Installation & Setup
### Prerequisites
- **.NET 8** or later
- **Mosquitto MQTT Broker** (Optional for external broker testing)

### Running the API Gateway & MQTT Broker
1. Clone the repository:
   ```sh
   git clone https://github.com/yourusername/your-repo.git
   cd your-repo
   ```
2. Run **MqttService** (Starts the embedded broker):
   ```sh
   cd MqttService
   dotnet run
   ```
3. Run **api_gateway** (Starts the API and MQTT client):
   ```sh
   cd ../api_gateway
   dotnet run
   ```
4. Open **Swagger UI**:
   ```
   http://localhost:5003/swagger
   ```

## Testing MQTT Messaging
To monitor MQTT messages, open a terminal and run:
```sh
mosquitto_sub -h localhost -t "uploads/new" -v
```
Then upload a file via the API to see the published message.

## Contribution & Roadmap
This is an **actively evolving project**, and contributions are welcome! Planned enhancements include:
- 📌 **LLM-powered document summarization**
- 📌 **Real-time entity extraction & knowledge graph building**
- 📌 **Secure cloud-based deployment options**

## License
MIT License - See [LICENSE](LICENSE) for details.

---
📌 **Follow this repository for updates as we integrate AI-powered enhancements! 🚀**

