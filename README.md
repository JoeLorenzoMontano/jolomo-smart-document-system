# JoLoMo Smart Document System

## Overview
The **JoLoMo Smart Document System** is a fully integrated AI-powered document management system. It enables **document ingestion, semantic search, and retrieval** using vector embeddings stored in **ChromaDB**. This system consists of multiple components working together:

- **RAG**: Query and interact with data from uploaded documents via our retrieval and Ollama LLM integration.
- **API Gateway**: Handles document uploads, processes text, and interacts with ChromaDB.
- **Vector Database (ChromaDB)**: Stores document embeddings for fast and efficient search.
- **Local Embedding Service**: Generates embeddings without relying on third-party services.
- **Angular Frontend**: Provides an intuitive UI for file uploads and search queries.
- **MQTT Integration**: Enables real-time updates and event-driven document processing.

## Features
- **Document Upload & Chunking**: Supports breaking documents into configurable chunks.
- **Semantic Search**: Finds relevant documents based on meaning, not just keywords.
- **Local Embedding Generation**: No reliance on third-party AI services.
- **Full Document Storage**: Links document chunks back to the original full document.
- **Real-Time Processing with MQTT**: Broadcasts updates and events for advanced automation.
- **Configurable Search & Ranking**: Allows fine-tuned document retrieval.

## System Architecture
- **Frontend (Angular)** → Communicates with API Gateway
- **Backend (ASP.NET Core)** → Manages document processing & embeddings
- **Vector Database (ChromaDB)** → Stores embeddings and document metadata
- **MQTT Broker** → Facilitates real-time event updates

## Installation
### Prerequisites
- **.NET 8** (for API Gateway & backend services)
- **Node.js & Angular CLI** (for frontend)
- **Docker** (for ChromaDB & MQTT Broker)

### Setup
1. **Clone the repository**:
   ```sh
   git clone https://github.com/your-repo/jolomo-smart-document-system.git
   cd jolomo-smart-document-system
   ```
2. **Start the backend services**:
   ```sh
   cd api_gateway
   dotnet run
   ```
3. **Start ChromaDB using Docker**:
   ```sh
   docker-compose up -d
   ```
4. **Start the Angular frontend**:
   ```sh
   cd angular_frontend
   npm install
   ng serve --open
   ```

## API Integration
### **Upload Document**
- **Endpoint:** `POST /api/upload`
- **Functionality:** Stores document text and embeddings.

### **Search Documents**
- **Endpoint:** `GET /api/upload/search`
- **Functionality:** Retrieves semantically relevant documents.

## Roadmap
### **Planned Enhancements**
- **RAG (Retrieval-Augmented Generation) Implementation**: Enhance search with AI-powered summarization.
- **Advanced Query Expansion**: Improve search ranking with NLP-based techniques.
- **Multi-Modal Embeddings**: Support text, images, and structured data.
- **Enhanced Role-Based Access Control (RBAC)**: Secure document access per user role.
- **Optimized Document Indexing**: Improve performance for large-scale document sets.
- **Integration with Other AI Models**: Allow plug-and-play AI enhancements.

## Contributing
Feel free to open an issue or submit a pull request if you would like to contribute!

## Contact
For inquiries, please reach out to `your-email@example.com`.

