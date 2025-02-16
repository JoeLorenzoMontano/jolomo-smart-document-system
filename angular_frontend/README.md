# Angular Frontend

## Overview
This Angular project serves as the frontend for interacting with the API Gateway, enabling users to upload documents and perform semantic searches. It communicates with a backend that processes documents and stores embeddings in ChromaDB.

## Features
- **File Upload:** Allows users to upload documents to the API Gateway.
- **Search Functionality:** Retrieves documents based on semantic similarity using vector embeddings.
- **Configurable Results:** Supports options for filtering and retrieving original document text.

## Installation
### Prerequisites
- Node.js (v16+ recommended)
- Angular CLI (`npm install -g @angular/cli`)

### Setup
1. Clone this repository:
   ```sh
   git clone https://github.com/your-repo/angular-frontend.git
   cd angular-frontend
   ```
2. Install dependencies:
   ```sh
   npm install
   ```
3. Start the development server:
   ```sh
   ng serve --open
   ```
   This will open the app in `http://localhost:4200/`.

## API Integration
This frontend interacts with the **API Gateway** (`http://localhost:5003/api/upload`). Ensure the API Gateway is running before testing.

### **Upload Document**
- **Endpoint:** `POST /api/upload`
- **Function in Angular Service:** `uploadFile(file: File)`

### **Search Documents**
- **Endpoint:** `GET /api/upload/search`
- **Function in Angular Service:** `searchDocuments(query: string, topResults?: number, includeOriginalText?: boolean)`

## Roadmap
### **Planned Enhancements**
- **UI Enhancements:** Improve styling and user experience.
- **Loading Indicators:** Display progress during uploads and searches.
- **Pagination & Sorting:** Enhance search results usability.
- **Error Handling:** Provide informative feedback to users.

## Contributing
Feel free to open an issue or submit a pull request if you would like to contribute!

## Contact
For inquiries, please reach out to `your-email@example.com`.

