#!/usr/bin/env python3
"""
Setup script for ChatBot NLP Service
This script will install all required packages and download necessary spaCy models.
"""

import subprocess
import sys
import os

def run_command(command, description):
    """Run a command and handle errors"""
    print(f"\n{'='*60}")
    print(f"Running: {description}")
    print(f"Command: {command}")
    print('='*60)
    
    try:
        result = subprocess.run(command, shell=True, check=True, capture_output=True, text=True)
        print("✅ Success!")
        if result.stdout:
            print("Output:", result.stdout)
        return True
    except subprocess.CalledProcessError as e:
        print(f"❌ Error: {e}")
        if e.stdout:
            print("stdout:", e.stdout)
        if e.stderr:
            print("stderr:", e.stderr)
        return False

def main():
    print("🚀 Setting up ChatBot NLP Service Environment")
    print("="*60)
    
    # Check if we're in a virtual environment
    if not hasattr(sys, 'real_prefix') and not (hasattr(sys, 'base_prefix') and sys.base_prefix != sys.prefix):
        print("⚠️  Warning: You're not in a virtual environment!")
        print("   It's recommended to create and activate a virtual environment first.")
        response = input("   Continue anyway? (y/N): ")
        if response.lower() != 'y':
            print("Setup cancelled.")
            return
    
    # Install requirements
    if not run_command("pip install -r requirements.txt", "Installing Python packages from requirements.txt"):
        print("❌ Failed to install requirements. Please check the error above.")
        return
    
    # Download spaCy models
    models_to_download = [
        ("en_core_web_lg", "Large English model (used for embeddings and similarity)"),
        ("en_core_web_sm", "Small English model (backup)")
    ]
    
    for model, description in models_to_download:
        if not run_command(f"python -m spacy download {model}", f"Downloading {model} - {description}"):
            print(f"❌ Failed to download {model}. Please check the error above.")
            return
    
    # Verify installation
    print("\n" + "="*60)
    print("🔍 Verifying installation...")
    print("="*60)
    
    verification_commands = [
        ("python -c \"import spacy; print('spaCy version:', spacy.__version__)\"", "Checking spaCy installation"),
        ("python -c \"import coreferee; print('coreferee version:', coreferee.__version__)\"", "Checking coreferee installation"),
        ("python -c \"import chromadb; print('ChromaDB version:', chromadb.__version__)\"", "Checking ChromaDB installation"),
        ("python -c \"import transformers; print('Transformers version:', transformers.__version__)\"", "Checking Transformers installation"),
        ("python -c \"import torch; print('PyTorch version:', torch.__version__)\"", "Checking PyTorch installation"),
        ("python -c \"import fastapi; print('FastAPI version:', fastapi.__version__)\"", "Checking FastAPI installation"),
    ]
    
    all_success = True
    for command, description in verification_commands:
        if not run_command(command, description):
            all_success = False
    
    # Test spaCy models
    print("\n" + "="*60)
    print("🧪 Testing spaCy models...")
    print("="*60)
    
    test_commands = [
        ("python -c \"import spacy; nlp = spacy.load('en_core_web_lg'); print('✅ en_core_web_lg loaded successfully')\"", "Testing en_core_web_lg model"),
        ("python -c \"import spacy; nlp = spacy.load('en_core_web_sm'); print('✅ en_core_web_sm loaded successfully')\"", "Testing en_core_web_sm model"),
    ]
    
    for command, description in test_commands:
        if not run_command(command, description):
            all_success = False
    
    # Final summary
    print("\n" + "="*60)
    if all_success:
        print("🎉 Setup completed successfully!")
        print("="*60)
        print("Next steps:")
        print("1. Make sure your intent_model folder is in the correct location")
        print("2. Make sure your CSV files are in the correct locations:")
        print("   - ../../ChatBot.Server/Data/erp_case_data_expanded.csv")
        print("   - ../../intent_training/erp_intents.csv")
        print("3. Run the service with: python erp_nlp_service.py")
        print("4. The service will be available at: http://localhost:8000")
    else:
        print("❌ Setup completed with errors!")
        print("Please check the error messages above and fix them.")
    print("="*60)

if __name__ == "__main__":
    main() 