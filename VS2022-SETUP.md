# Visual Studio 2022 Setup Guide

This guide will help you set up the ChatBot project to run properly in Visual Studio 2022.

## Prerequisites

1. **Visual Studio 2022** with the following workloads:
   - ASP.NET and web development
   - .NET desktop development
   - Python development

2. **Python 3.11** installed on your system

3. **Node.js** (for the React client)

## Setup Steps

### 1. Configure Python Interpreter

1. Open the solution in Visual Studio 2022
2. Right-click on the `erp-nlp-service` project in Solution Explorer
3. Select "Properties"
4. Go to the "Python Environment" tab
5. Set the interpreter to: `venv_new\Scripts\python.exe`
6. Make sure the virtual environment is activated

### 2. Set Multiple Startup Projects

1. Right-click on the solution in Solution Explorer
2. Select "Set Startup Projects..."
3. Choose "Multiple startup projects"
4. Set the following projects to "Start":
   - `ChatBot.Server`
   - `erp-nlp-service`
   - `chatbot.client`

### 3. Alternative: Use Startup Scripts

If you prefer to start services manually, you can use the provided scripts:

#### Option A: Batch File
```bash
start-all-services.bat
```

#### Option B: PowerShell Script
```powershell
.\start-all-services.ps1
```

### 4. Verify Virtual Environment

Make sure your Python virtual environment has all required packages:

```bash
cd erp-nlp-service\erp-nlp-service
venv_new\Scripts\activate
pip list
```

Required packages should include:
- spacy
- spacy-transformers
- coreferee
- fastapi
- uvicorn
- chromadb

### 5. Install spaCy Model (if not already installed)

```bash
cd erp-nlp-service\erp-nlp-service
venv_new\Scripts\activate
python -m spacy download en_core_web_lg
```

## Running the Project

### Method 1: Visual Studio Start Button
1. Press F5 or click the "Start" button in Visual Studio
2. All three projects should start automatically
3. The browser will open to the React client

### Method 2: Manual Start
1. Start the Python NLP service first
2. Start the .NET server
3. Start the React client

## Service URLs

- **NLP Service**: http://localhost:8000
- **ChatBot Server**: http://localhost:5049
- **React Client**: http://localhost:5173

## Troubleshooting

### Python Interpreter Issues
- Make sure the virtual environment path is correct
- Verify that `venv_new\Scripts\python.exe` exists
- Check that all required packages are installed

### Port Conflicts
- If ports are already in use, stop other services
- Check Task Manager for running processes
- Use `netstat -ano` to find what's using the ports

### Virtual Environment Issues
- Recreate the virtual environment if needed:
  ```bash
  cd erp-nlp-service\erp-nlp-service
  python -m venv venv_new
  venv_new\Scripts\activate
  pip install -r requirements.txt
  python -m spacy download en_core_web_lg
  ```

## Development Workflow

1. Make changes to your code
2. Save all files
3. Press F5 to restart all services
4. Test your changes in the browser

## Notes

- The Python service must be running for the chatbot to work
- The .NET server acts as the main API gateway
- The React client provides the user interface
- All services communicate via HTTP APIs 