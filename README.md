# ChatBot Project

## Overview
A modern, full-stack chatbot application featuring a React frontend, .NET backend, and a Python NLP microservice. The project is designed for extensibility, robust NLP, and a beautiful, user-friendly chat interface.

## Features
- Conversational UI with markdown support, auto-suggestions, avatars, and custom scrollbars
- Multi-line, auto-expanding chat input
- Backend API with .NET 9 (C#)
- Python NLP microservice for intent classification and entity extraction
- Modular, extensible architecture
- Secure handling of secrets and configuration

## Architecture
```
[React Frontend] <-> [ASP.NET Core Backend API] <-> [Python NLP Microservice]
```
- **Frontend:** `chatbot.client/` (React, JSX, CSS)
- **Backend:** `ChatBot.Server/` (C#, ASP.NET Core)
- **NLP Service:** `erp-nlp-service/erp-nlp-service/` (Python, FastAPI or Flask)

## Folder Structure
```
ChatBot/
├── chatbot.client/         # React frontend
├── ChatBot.Server/         # .NET backend
├── erp-nlp-service/        # Python NLP microservice
├── intent_training/        # NER/intent model training scripts
├── start-all-services.bat  # Script to start all services
└── .gitignore
```

## Setup Instructions

### Prerequisites
- Node.js (for frontend)
- .NET 9 SDK (for backend)
- Python 3.8+ (for NLP microservice)

### 1. Clone the Repository
```sh
git clone <your-repo-url>
cd ChatBot
```

### 2. Install Frontend Dependencies
```sh
cd chatbot.client
npm install
```

### 3. Setup Backend
- Ensure `.NET 9` is installed.
- Copy `ChatBot.Server/appsettings.json.example` to `appsettings.json` and fill in your secrets (API keys, connection strings, etc.).

### 4. Setup Python NLP Microservice
```sh
cd erp-nlp-service/erp-nlp-service
python -m venv venv
venv/Scripts/activate  # or source venv/bin/activate on Linux/Mac
pip install -r requirements.txt  # (create this if missing)
```
- Configure any required environment variables or config files for the NLP service.

### 5. Running the Project

#### Start All Services (Windows)
```sh
./start-all-services.bat
```
#### Or Manually:
- **Backend:**
  ```sh
  cd ChatBot.Server
  dotnet run
  ```
- **Frontend:**
  ```sh
  cd chatbot.client
  npm start
  ```
- **NLP Service:**
  ```sh
  cd erp-nlp-service/erp-nlp-service
  venv/Scripts/activate  # or source venv/bin/activate
  python erp_nlp_service.py
  ```

## Environment Variables & Secrets
- **Backend:**
  - `appsettings.json` and `appsettings.Development.json` (not tracked by git)
  - Contains API keys, connection strings, etc.
- **NLP Service:**
  - Use `.env` or config files for secrets
- **Frontend:**
  - Place any secrets in environment variables, not in source code

## Security Note
- **Never commit secrets, API keys, or connection strings.**
- All sensitive configs (e.g., `appsettings.json`, `.env`) are gitignored by default.
- Review `.gitignore` before pushing to GitHub.

## Contribution Guidelines
- Fork the repo and create feature branches
- Use clear commit messages
- Ensure no secrets are committed
- Run tests and lint before PRs

## License
Specify your license here (MIT, Apache, etc.)

## Contact
For questions or support, open an issue or contact the maintainer. 