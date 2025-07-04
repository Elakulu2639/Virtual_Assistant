@echo off
echo ============================================================================
echo ChatBot NLP Service Setup Script
echo ============================================================================
echo.

REM Check if virtual environment exists
if not exist "venv_new" (
    echo Creating new virtual environment...
    python -m venv venv_new
)

REM Activate virtual environment
echo Activating virtual environment...
call venv_new\Scripts\activate.bat

REM Install requirements
echo Installing Python packages...
pip install -r requirements.txt

REM Download spaCy models
echo Downloading spaCy models...
python -m spacy download en_core_web_lg
python -m spacy download en_core_web_sm

REM Verify installation
echo.
echo ============================================================================
echo Verifying installation...
echo ============================================================================
python -c "import spacy; print('spaCy version:', spacy.__version__)"
python -c "import coreferee; print('coreferee version:', coreferee.__version__)"
python -c "import chromadb; print('ChromaDB version:', chromadb.__version__)"
python -c "import transformers; print('Transformers version:', transformers.__version__)"
python -c "import torch; print('PyTorch version:', torch.__version__)"
python -c "import fastapi; print('FastAPI version:', fastapi.__version__)"

echo.
echo ============================================================================
echo Testing spaCy models...
echo ============================================================================
python -c "import spacy; nlp = spacy.load('en_core_web_lg'); print('en_core_web_lg loaded successfully')"
python -c "import spacy; nlp = spacy.load('en_core_web_sm'); print('en_core_web_sm loaded successfully')"

echo.
echo ============================================================================
echo Setup completed!
echo ============================================================================
echo Next steps:
echo 1. Make sure your intent_model folder is in the correct location
echo 2. Make sure your CSV files are in the correct locations:
echo    - ../../ChatBot.Server/Data/erp_case_data_expanded.csv
echo    - ../../intent_training/erp_intents.csv
echo 3. Run the service with: python erp_nlp_service.py
echo 4. The service will be available at: http://localhost:8000
echo ============================================================================
pause 