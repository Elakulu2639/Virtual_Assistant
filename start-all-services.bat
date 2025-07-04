@echo off
echo Starting ChatBot Services...
echo.

echo Starting Python NLP Service...
cd /d "%~dp0erp-nlp-service\erp-nlp-service"
start "NLP Service" cmd /k "venv_new\Scripts\activate && python erp_nlp_service.py"

echo Starting .NET Server...
cd /d "%~dp0ChatBot.Server"
start "ChatBot Server" cmd /k "dotnet run"

echo Starting React Client...
cd /d "%~dp0chatbot.client"
start "ChatBot Client" cmd /k "npm run dev"

echo.
echo All services are starting...
echo - NLP Service: http://localhost:8000
echo - ChatBot Server: http://localhost:5049
echo - React Client: http://localhost:5173
echo.
echo Press any key to close this window...
pause > nul 