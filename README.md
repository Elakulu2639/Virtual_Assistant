# Intelligent Assistant Chatbot for ERP

## Overview
The Intelligent Assistant Chatbot for ERP is a next-generation conversational AI solution designed specifically for enterprise resource planning (ERP) environments. It empowers users to interact with complex business data and processes through natural language, streamlining workflows, improving productivity, and enhancing user experience across ERP modules.

Built with a modern full-stack architecture, the system combines:
- An intuitive React-based frontend for seamless user interaction
- A powerful .NET 9 backend API for secure business logic and integration
- An advanced Python NLP microservice for deep intent understanding, entity extraction, and semantic memory

This platform is engineered for extensibility, robust security, and rapid adaptation to diverse ERP domains, making it ideal for organizations seeking intelligent automation and conversational access to their business systems.

---

## Features
- **Conversational UI**: Markdown support, avatars, auto-suggestions, custom scrollbars, and multi-line input
- **Rich NLP**: Intent classification, entity extraction, semantic search, coreference resolution, and hybrid strategies
- **Session Memory**: Semantic chat history with ChromaDB
- **Modular Architecture**: Easily extend or swap out components
- **Secure Config**: Secrets and environment variables are never committed
- **Developer Friendly**: Hot reload, linting, and clear separation of concerns

---

## Architecture

```
[React Frontend]
    |
    |  (REST API, WebSockets)
    v
[.NET 9 Backend API]
    |
    |  (HTTP, JSON)
    v
[Python NLP Microservice]
```

- **Frontend**: `chatbot.client/` (React, Vite, MUI, modern JS)
- **Backend**: `ChatBot.Server/` (C#, ASP.NET Core 9, Entity Framework, Health Checks)
- **NLP Service**: `erp-nlp-service/erp-nlp-service/` (Python 3.8+, FastAPI, spaCy, Transformers, ChromaDB)
- **Model Training**: `intent_training/` (NER/intent scripts, data)

---

## Tech Stack

### Frontend
- React 19, Vite, MUI, Emotion, Axios, React Markdown, Framer Motion, ESLint

### Backend
- .NET 9, ASP.NET Core, Entity Framework Core, Health Checks, Swashbuckle (Swagger), SQL Server/PostgreSQL

### NLP Microservice
- Python 3.8+, FastAPI, spaCy, Transformers, ChromaDB, Uvicorn, Pandas, Scikit-learn

---

## Folder Structure
```
ChatBot/
├── chatbot.client/         # React frontend (src/, public/, etc.)
├── ChatBot.Server/         # .NET backend (Controllers/, Services/, Data/)
├── erp-nlp-service/        # Python NLP microservice (models, API, venv)
├── intent_training/        # NER/intent model training scripts and data
├── start-all-services.bat  # (Optional) Script to start all services (Windows)
└── ...
```

---

## Setup & Installation

### Prerequisites
- **Frontend**: Node.js (v18+ recommended)
- **Backend**: .NET 9 SDK
- **NLP Service**: Python 3.8+ (recommend venv), see `requirements.txt`

### 1. Clone the Repository
```sh
git clone <your-repo-url>
cd ChatBot
```

### 2. Frontend Setup
```sh
cd chatbot.client
npm install
```

### 3. Backend Setup
- Ensure .NET 9 is installed
- Copy `ChatBot.Server/appsettings.json.example` to `appsettings.json` and fill in secrets (API keys, DB, etc.)
- (Optional) Run migrations if using a database

### 4. NLP Microservice Setup
```sh
cd erp-nlp-service/erp-nlp-service
python -m venv venv
# Windows:
venv/Scripts/activate
# Linux/Mac:
# source venv/bin/activate
pip install -r requirements.txt
# Download spaCy models:
python -m spacy download en_core_web_lg
python -m spacy download en_core_web_sm
```
- Configure any required environment variables or config files

---

## Running the Project

### Start All Services (Windows)
```sh
./start-all-services.bat
```

### Or Run Individually
- **Backend:**
  ```sh
  cd ChatBot.Server
  dotnet run
  ```
- **Frontend:**
  ```sh
  cd chatbot.client
  npm run dev
  ```
- **NLP Service:**
  ```sh
  cd erp-nlp-service/erp-nlp-service
  venv/Scripts/activate  # or source venv/bin/activate
  python erp_nlp_service.py
  # Or with uvicorn for production:
  uvicorn erp_nlp_service:app --host 0.0.0.0 --port 8000
  ```