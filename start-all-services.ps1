# Start ChatBot Services
Write-Host "Starting ChatBot Services..." -ForegroundColor Green
Write-Host ""

# Get the script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Start Python NLP Service
Write-Host "Starting Python NLP Service..." -ForegroundColor Yellow
$nlpPath = Join-Path $scriptDir "erp-nlp-service\erp-nlp-service"
$venvPath = Join-Path $nlpPath "venv_new\Scripts\Activate.ps1"
$pythonScript = Join-Path $nlpPath "erp_nlp_service.py"

if (Test-Path $venvPath) {
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "& '$venvPath'; python '$pythonScript'" -WindowStyle Normal
} else {
    Write-Host "Error: Virtual environment not found at $venvPath" -ForegroundColor Red
}

# Start .NET Server
Write-Host "Starting .NET Server..." -ForegroundColor Yellow
$serverPath = Join-Path $scriptDir "ChatBot.Server"
if (Test-Path $serverPath) {
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$serverPath'; dotnet run" -WindowStyle Normal
} else {
    Write-Host "Error: Server project not found at $serverPath" -ForegroundColor Red
}

# Start React Client
Write-Host "Starting React Client..." -ForegroundColor Yellow
$clientPath = Join-Path $scriptDir "chatbot.client"
if (Test-Path $clientPath) {
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$clientPath'; npm run dev" -WindowStyle Normal
} else {
    Write-Host "Error: Client project not found at $clientPath" -ForegroundColor Red
}

Write-Host ""
Write-Host "All services are starting..." -ForegroundColor Green
Write-Host "- NLP Service: http://localhost:8000" -ForegroundColor Cyan
Write-Host "- ChatBot Server: http://localhost:5049" -ForegroundColor Cyan
Write-Host "- React Client: http://localhost:5173" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press any key to close this window..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 