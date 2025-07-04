from fastapi import FastAPI, Request
import spacy
from transformers import pipeline, AutoTokenizer, AutoModelForSequenceClassification
import pandas as pd
import numpy as np
import torch
import json
from pydantic import BaseModel
from typing import List, Optional, Dict, Any, Union
import chromadb
from chromadb.config import Settings
from datetime import datetime
import uuid
import coreferee
from dataclasses import dataclass
from enum import Enum
import os

app = FastAPI()

# Configuration class for different data sources and thresholds
@dataclass
class DataSourceConfig:
    name: str
    csv_path: Optional[str] = None
    similarity_threshold: float = 0.7
    max_results: int = 5
    enabled: bool = True

@dataclass
class IntentConfig:
    model_path: str
    lookup_csv_path: Optional[str] = None
    confidence_threshold: float = 0.5
    enabled: bool = True

@dataclass
class SemanticConfig:
    similarity_threshold: float = 0.6
    max_history_results: int = 5
    use_coreference: bool = True
    enabled: bool = True

class AnalysisStrategy(Enum):
    EXACT_MATCH = "exact_match"
    SEMANTIC_SEARCH = "semantic_search"
    INTENT_CLASSIFICATION = "intent_classification"
    CONTEXT_AWARE = "context_aware"
    HYBRID = "hybrid"

# Global configuration
class Config:
    def __init__(self):
        self.data_sources: List[DataSourceConfig] = []
        self.intent_config: Optional[IntentConfig] = None
        self.semantic_config = SemanticConfig()
        self.default_strategy = AnalysisStrategy.HYBRID
        
    def add_data_source(self, config: DataSourceConfig):
        self.data_sources.append(config)
        
    def set_intent_config(self, config: IntentConfig):
        self.intent_config = config
        
    def set_semantic_config(self, config: SemanticConfig):
        self.semantic_config = config

# Initialize global configuration
config = Config()

# Initialize ChromaDB for semantic memory
chroma_client = chromadb.Client(Settings(
    persist_directory="./chroma_db"  # Persistent storage for chat history
))
chat_collection = chroma_client.get_or_create_collection("chat_history")

# Use spaCy's large model for all NLP tasks (embeddings, similarity, coreference)
try:
    nlp = spacy.load("en_core_web_lg")
    print(f"[spaCy] Loaded model: {nlp.meta['name']} (version {nlp.meta['version']})")
    if config.semantic_config.use_coreference:
        try:
            nlp.add_pipe('coreferee')
            print("[spaCy] coreferee pipeline added successfully to en_core_web_lg.")
        except Exception as e:
            print(f"[spaCy] Error adding coreferee pipeline: {e}")
except Exception as e:
    print(f"[spaCy] Error loading model or setting up coreferee: {e}")
    raise

# Load fine-tuned intent classifier
INTENT_MODEL_PATH = "intent_model"
intent_tokenizer = AutoTokenizer.from_pretrained(INTENT_MODEL_PATH)
intent_model = AutoModelForSequenceClassification.from_pretrained(INTENT_MODEL_PATH)
with open(f"{INTENT_MODEL_PATH}/id2intent.json", "r") as f:
    id2intent = json.load(f)

# Load CSV and compute spaCy docs for semantic search (use nlp with vectors)
CSV_PATH = '../../ChatBot.Server/Data/erp_case_data_expanded.csv'
df = pd.read_csv(CSV_PATH)
questions = df['Question'].tolist()
answers = df['Answer'].tolist()
question_docs = [nlp(q) for q in questions]

# Load intent CSV for hybrid lookup
INTENT_LOOKUP_CSV = '../../intent_training/erp_intents.csv'
intent_df = pd.read_csv(INTENT_LOOKUP_CSV)
intent_lookup = {str(q).strip().lower(): i for q, i in zip(intent_df['text'], intent_df['intent'])}

# Configuration for different domains (can be extended)
DOMAIN_CONFIGS = {
    'erp': {
        'csv_path': '../../ChatBot.Server/Data/erp_case_data_expanded.csv',
        'intent_lookup_csv': '../../intent_training/erp_intents.csv',
        'similarity_threshold': 0.75,
        'intent_confidence_threshold': 0.5
    },
    'customer_service': {
        'csv_path': None,  # Can be added later
        'intent_lookup_csv': None,
        'similarity_threshold': 0.7,
        'intent_confidence_threshold': 0.6
    },
    'technical_support': {
        'csv_path': None,  # Can be added later
        'intent_lookup_csv': None,
        'similarity_threshold': 0.8,
        'intent_confidence_threshold': 0.7
    }
}

# Current domain (can be changed via API)
current_domain = 'erp'

def get_domain_config():
    return DOMAIN_CONFIGS.get(current_domain, DOMAIN_CONFIGS['erp'])

# ChromaDB functions for semantic memory (store as before, but use spaCy for similarity)
def add_message_to_chroma(session_id: str, message: str, role: str, timestamp: Optional[str] = None):
    if timestamp is None:
        timestamp = datetime.utcnow().isoformat()
    message_id = str(uuid.uuid4())
    # Use spaCy vector for embedding
    embedding = nlp(message).vector
    print(f"[Embedding DEBUG] Message: '{message}'\n[Embedding DEBUG] Vector (first 5): {embedding[:5]} | Norm: {np.linalg.norm(embedding):.4f}")
    if embedding is None or np.linalg.norm(embedding) == 0 or len(embedding) == 0:
        print(f"[Embedding WARNING] Empty or zero embedding for message: '{message}' (skipping ChromaDB add)")
        return None
    chat_collection.add(
        documents=[message],
        embeddings=[embedding.tolist()],
        metadatas=[{
            "session_id": session_id,
            "role": role,
            "timestamp": timestamp,
            "message_id": message_id
        }],
        ids=[message_id]
    )
    print(f"[ChromaDB] Stored {role} message for session {session_id}")
    return message_id

def get_relevant_history(query: str, session_id: Optional[str] = None, top_k: int = 5):
    query_doc = nlp(query)
    filters = {}
    if session_id:
        filters["session_id"] = session_id
    # Get all messages for the session
    results = chat_collection.get(where=filters if filters else None)
    messages = []
    if results["documents"]:
        for i, doc in enumerate(results["documents"]):
            messages.append({
                "message": doc,
                "role": results["metadatas"][i]["role"],
                "timestamp": results["metadatas"][i]["timestamp"]
            })
    # Compute similarity using spaCy
    for msg in messages:
        msg_doc = nlp(msg["message"])
        msg["similarity"] = query_doc.similarity(msg_doc)
    # Sort by similarity and return top_k
    messages.sort(key=lambda x: x["similarity"], reverse=True)
    return messages[:top_k]

def get_session_history(session_id: str, limit: int = 10):
    try:
        results = chat_collection.get(
            where={"session_id": session_id},
            limit=limit
        )
        messages = []
        if results["documents"]:
            for i, doc in enumerate(results["documents"]):
                messages.append({
                    "message": doc,
                    "role": results["metadatas"][i]["role"],
                    "timestamp": results["metadatas"][i]["timestamp"]
                })
        messages.sort(key=lambda x: x["timestamp"])
        return messages
    except Exception as e:
        print(f"[ChromaDB] Error getting session history: {e}")
        return []

def search_with_context(query: str, context_messages: List[str] = None):
    """Enhanced semantic search that considers conversation context"""
    if not questions or not question_docs:
        return None, 0.0
    
    query_doc = nlp(query)
    best_score = 0.0
    best_idx = -1
    
    # If we have context, create a combined query
    if context_messages:
        context_text = " ".join(context_messages)
        context_doc = nlp(context_text)
        # Combine query with context for better matching
        combined_query = f"{query} {context_text}"
        combined_doc = nlp(combined_query)
    else:
        combined_doc = query_doc
    
    # Search through all questions
    for i, q_doc in enumerate(question_docs):
        # Calculate similarity with both original query and combined query
        direct_similarity = query_doc.similarity(q_doc)
        context_similarity = combined_doc.similarity(q_doc)
        
        # Use the higher similarity score
        similarity = max(direct_similarity, context_similarity)
        
        if similarity > best_score:
            best_score = similarity
            best_idx = i
    
    return best_idx, best_score

def lookup_intent_exact(text):
    return intent_lookup.get(str(text).strip().lower())

def resolve_coref(user_message, last_bot_message, last_user_message=None):
    # Use only the last bot message as context, with clear speaker tags
    if last_bot_message:
        context = f"Bot: {last_bot_message}\nUser: {user_message}"
    else:
        context = user_message
    doc = nlp(context)
    
    # Check if coreferee is properly loaded
    if not hasattr(doc._, 'coref_resolved'):
        print("[Coreferee WARNING] coref_resolved extension not found. Trying alternative approach.")
        # Fallback: simple context-aware resolution
        if last_bot_message and any(word in user_message.lower() for word in ['it', 'this', 'that', 'they', 'them', 'those', 'what about']):
            # If the user message contains pronouns or follow-up phrases, try to expand them with context
            if 'leave' in last_bot_message.lower() or 'policy' in last_bot_message.lower():
                if 'manager' in user_message.lower():
                    resolved = user_message.replace('what about', 'what is the leave policy for managers')
                elif 'employee' in user_message.lower():
                    resolved = user_message.replace('what about', 'what is the leave policy for employees')
                else:
                    resolved = user_message.replace('what about', 'what is the leave policy for')
            elif 'technical' in last_bot_message.lower() or 'support' in last_bot_message.lower():
                if 'manager' in user_message.lower():
                    resolved = user_message.replace('what about', 'what is the technical support for managers')
                else:
                    resolved = user_message.replace('what about', 'what is the technical support for')
            else:
                resolved = user_message
        else:
            resolved = user_message
    else:
        try:
            # Only return the rewritten user message part
            if "User:" in doc._.coref_resolved:
                resolved = doc._.coref_resolved.split("User:")[-1].strip()
            else:
                resolved = doc._.coref_resolved
        except Exception as e:
            print(f"[Coreferee WARNING] Error resolving coref: {e}")
            resolved = user_message
    
    return resolved

def classify_intent_local(text):
    inputs = intent_tokenizer(text, return_tensors="pt", truncation=True, padding=True, max_length=64)
    with torch.no_grad():
        logits = intent_model(**inputs).logits
        pred = torch.argmax(logits, dim=1).item()
    print(f"[Intent Debug] Input: {text}")
    print(f"[Intent Debug] Logits: {logits.tolist()}")
    print(f"[Intent Debug] Predicted class index: {pred}")
    print(f"[Intent Debug] Predicted intent: {id2intent[str(pred)]}")
    return id2intent[str(pred)]

class AnalyzeRequest(BaseModel):
    text: str
    session_id: Optional[str] = None
    prev_bot_response: Optional[str] = ""
    last_user_message: Optional[str] = None
    history: Optional[List[str]] = []  # Keep for backward compatibility

class ClassifyIntentRequest(BaseModel):
    text: str

class ExtractEntitiesRequest(BaseModel):
    text: str

class StoreMessageRequest(BaseModel):
    session_id: str
    message: str
    role: str  # "user" or "bot"
    timestamp: Optional[str] = None

class ConfigRequest(BaseModel):
    data_sources: Optional[List[Dict[str, Any]]] = None
    intent_config: Optional[Dict[str, Any]] = None
    semantic_config: Optional[Dict[str, Any]] = None
    default_strategy: Optional[str] = None

class DomainChangeRequest(BaseModel):
    domain: str

# Data source management
class DataSourceManager:
    def __init__(self):
        self.sources: Dict[str, Dict[str, Any]] = {}
        
    def add_source(self, source_config: DataSourceConfig):
        if not source_config.enabled or not source_config.csv_path:
            return
            
        try:
            df = pd.read_csv(source_config.csv_path)
            questions = df['Question'].tolist() if 'Question' in df.columns else []
            answers = df['Answer'].tolist() if 'Answer' in df.columns else []
            question_docs = [nlp(q) for q in questions] if questions else []
            
            self.sources[source_config.name] = {
                'config': source_config,
                'questions': questions,
                'answers': answers,
                'question_docs': question_docs,
                'df': df
            }
            print(f"[DataSource] Loaded {source_config.name}: {len(questions)} questions")
        except Exception as e:
            print(f"[DataSource] Error loading {source_config.name}: {e}")
    
    def search_all_sources(self, query: str) -> List[Dict[str, Any]]:
        results = []
        query_doc = nlp(query)
        
        for source_name, source_data in self.sources.items():
            if not source_data['config'].enabled:
                continue
                
            similarities = [query_doc.similarity(q_doc) for q_doc in source_data['question_docs']]
            if not similarities:
                continue
                
            best_idx = int(np.argmax(similarities))
            best_score = float(similarities[best_idx])
            
            if best_score >= source_data['config'].similarity_threshold:
                results.append({
                    'source': source_name,
                    'score': best_score,
                    'question': source_data['questions'][best_idx],
                    'answer': source_data['answers'][best_idx],
                    'index': best_idx
                })
        
        # Sort by score descending
        results.sort(key=lambda x: x['score'], reverse=True)
        return results[:max(len(results), 1)]

# Intent classification management
class IntentManager:
    def __init__(self):
        self.tokenizer = None
        self.model = None
        self.id2intent = {}
        self.lookup_dict = {}
        self.enabled = False
        
    def setup(self, intent_config: IntentConfig):
        if not intent_config.enabled:
            return
            
        try:
            # Load fine-tuned model
            self.tokenizer = AutoTokenizer.from_pretrained(intent_config.model_path)
            self.model = AutoModelForSequenceClassification.from_pretrained(intent_config.model_path)
            
            with open(f"{intent_config.model_path}/id2intent.json", "r") as f:
                self.id2intent = json.load(f)
            
            # Load lookup CSV if provided
            if intent_config.lookup_csv_path:
                intent_df = pd.read_csv(intent_config.lookup_csv_path)
                self.lookup_dict = {str(q).strip().lower(): i for q, i in zip(intent_df['text'], intent_df['intent'])}
            
            self.enabled = True
            print(f"[Intent] Loaded model with {len(self.id2intent)} intents")
        except Exception as e:
            print(f"[Intent] Error loading intent model: {e}")
    
    def classify(self, text: str) -> Dict[str, Any]:
        if not self.enabled:
            return {'intent': 'unknown', 'confidence': 0.0, 'method': 'disabled'}
        
        # Try exact lookup first
        exact_intent = self.lookup_dict.get(str(text).strip().lower())
        if exact_intent:
            return {'intent': exact_intent, 'confidence': 1.0, 'method': 'exact_lookup'}
        
        # Use model classification
        try:
            inputs = self.tokenizer(text, return_tensors="pt", truncation=True, padding=True, max_length=64)
            with torch.no_grad():
                logits = self.model(**inputs).logits
                probabilities = torch.softmax(logits, dim=1)
                pred = torch.argmax(logits, dim=1).item()
                confidence = float(probabilities[0][pred])
            
            intent = self.id2intent.get(str(pred), 'unknown')
            return {
                'intent': intent,
                'confidence': confidence,
                'method': 'model_classification',
                'all_probabilities': probabilities[0].tolist()
            }
        except Exception as e:
            print(f"[Intent] Error in classification: {e}")
            return {'intent': 'unknown', 'confidence': 0.0, 'method': 'error'}

# Initialize managers
data_manager = DataSourceManager()
intent_manager = IntentManager()

# Generic analysis function
def analyze_text(text: str, session_id: Optional[str] = None, 
                prev_bot_response: str = "", last_user_message: str = None,
                strategy: AnalysisStrategy = None) -> Dict[str, Any]:
    
    if strategy is None:
        strategy = config.default_strategy
    
    # Store user message
    if session_id:
        add_message_to_chroma(session_id, text, "user")
    
    # Coreference resolution
    original_text = text
    resolved_text = resolve_coref(text, prev_bot_response, last_user_message)
    rewritten = resolved_text if resolved_text != text else None
    text = resolved_text
    
    result = {
        'original_text': original_text,
        'resolved_text': text,
        'rewritten': rewritten,
        'session_id': session_id,
        'strategy_used': strategy.value,
        'timestamp': datetime.utcnow().isoformat()
    }
    
    # Execute analysis based on strategy
    if strategy == AnalysisStrategy.EXACT_MATCH:
        result.update(analyze_exact_match(text))
    elif strategy == AnalysisStrategy.SEMANTIC_SEARCH:
        result.update(analyze_semantic_search(text))
    elif strategy == AnalysisStrategy.INTENT_CLASSIFICATION:
        result.update(analyze_intent_classification(text))
    elif strategy == AnalysisStrategy.CONTEXT_AWARE:
        result.update(analyze_context_aware(text, session_id))
    elif strategy == AnalysisStrategy.HYBRID:
        result.update(analyze_hybrid(text, session_id))
    
    return result

def analyze_exact_match(text: str) -> Dict[str, Any]:
    # Check all data sources for exact matches
    for source_name, source_data in data_manager.sources.items():
        if not source_data['config'].enabled:
            continue
            
        exact_match = source_data['lookup_dict'].get(text.lower().strip())
        if exact_match:
            return {
                'source': 'exact_match',
                'data_source': source_name,
                'match': exact_match,
                'confidence': 1.0
            }
    
    return {'source': 'exact_match', 'confidence': 0.0}

def analyze_semantic_search(text: str) -> Dict[str, Any]:
    results = data_manager.search_all_sources(text)
    
    if results:
        best_result = results[0]
        return {
            'source': 'semantic_search',
            'data_source': best_result['source'],
            'answer': best_result['answer'],
            'question': best_result['question'],
            'similarity': best_result['score'],
            'all_results': results
        }
    
    return {'source': 'semantic_search', 'similarity': 0.0}

def analyze_intent_classification(text: str) -> Dict[str, Any]:
    intent_result = intent_manager.classify(text)
    return {
        'source': 'intent_classification',
        'intent': intent_result['intent'],
        'confidence': intent_result['confidence'],
        'method': intent_result['method']
    }

def analyze_context_aware(text: str, session_id: Optional[str] = None) -> Dict[str, Any]:
    relevant_history = get_relevant_history(text, session_id)
    context_messages = [msg["message"] for msg in relevant_history if msg["role"] == "bot"]
    context_used = " | ".join(context_messages[-2:]) if context_messages else None
    
    # Extract entities
    doc = nlp(text)
    entities = {ent.label_: ent.text for ent in doc.ents}
    
    return {
        'source': 'context_aware',
        'entities': entities,
        'context_used': context_used,
        'relevant_history': relevant_history
    }

def analyze_hybrid(text: str, session_id: Optional[str] = None) -> Dict[str, Any]:
    # Try exact match first
    exact_result = analyze_exact_match(text)
    if exact_result['confidence'] > 0.9:
        return exact_result
    
    # Try semantic search
    semantic_result = analyze_semantic_search(text)
    if semantic_result['similarity'] > 0.8:
        return semantic_result
    
    # Try intent classification
    intent_result = analyze_intent_classification(text)
    if intent_result['confidence'] > 0.7:
        return intent_result
    
    # Fall back to context-aware analysis
    context_result = analyze_context_aware(text, session_id)
    return context_result

@app.post("/analyze")
async def analyze(request: AnalyzeRequest):
    text = request.text
    session_id = request.session_id
    prev_bot_response = request.prev_bot_response or ""
    last_user_message = request.last_user_message
    history = request.history or []
    context_used = None

    # Get domain-specific configuration
    domain_config = get_domain_config()

    # Store the user message in ChromaDB for future semantic retrieval
    if session_id:
        add_message_to_chroma(session_id, text, "user")

    # Coreference resolution for multi-turn context
    print(f"[Coreferee DEBUG] User message: '{text}'")
    print(f"[Coreferee DEBUG] Last bot message: '{prev_bot_response}'")
    print(f"[Coreferee DEBUG] Last user message: '{last_user_message}'")
    resolved_text = resolve_coref(text, prev_bot_response, last_user_message)
    print(f"[Coreferee DEBUG] Resolved (rewritten) message: '{resolved_text}'")
    rewritten = None
    if resolved_text != text:
        print(f"[Coreferee] Rewrote '{text}' to '{resolved_text}' using context.")
        rewritten = resolved_text
        text = resolved_text

    # 0. Hybrid: Exact intent lookup first (domain-specific)
    exact_intent = lookup_intent_exact(text)
    if exact_intent:
        return {
            "source": "csv_lookup",
            "intent": exact_intent,
            "matched_question": text,
            "rewritten": rewritten,
            "domain": current_domain
        }

    # 1. Try semantic search in CSV using spaCy similarity (domain-specific threshold)
    if questions and question_docs:
        # Get recent conversation context for better search
        context_messages = []
        if session_id:
            recent_history = get_session_history(session_id, limit=3)
            context_messages = [msg["message"] for msg in recent_history if msg["role"] == "bot"]
        
        # Use enhanced context-aware search
        best_idx, best_score = search_with_context(text, context_messages)
        if best_idx >= 0:
            print(f"[Semantic Search] User Query: {text}")
            print(f"[Semantic Search] Best Match: {questions[best_idx]}")
            print(f"[Semantic Search] Similarity Score: {best_score}")
            print(f"[Semantic Search] Domain Threshold: {domain_config['similarity_threshold']}")
            print(f"[Semantic Search] Context used: {len(context_messages)} messages")
            if best_score > domain_config['similarity_threshold']:
                return {
                    "source": "csv",
                    "answer": answers[best_idx],
                    "similarity": best_score,
                    "matched_question": questions[best_idx],
                    "rewritten": rewritten,
                    "domain": current_domain
                }

    # 2. Get semantically relevant chat history using spaCy similarity
    relevant_history = []
    if session_id:
        relevant_history = get_relevant_history(text, session_id, top_k=5)
        print(f"[Context] Retrieved {len(relevant_history)} relevant messages from semantic memory")
        if relevant_history:
            context_messages = [msg["message"] for msg in relevant_history if msg["role"] == "bot"]
            if context_messages:
                context_used = " | ".join(context_messages[-2:])

    # 3. Context-aware intent/entity extraction (domain-specific confidence)
    doc = nlp(text)
    entities = {ent.label_: ent.text for ent in doc.ents}
    intent_result = classify_intent_local(text)
    
    # Check if intent confidence meets domain threshold
    intent_confidence = 0.9  # Default high confidence for exact matches
    if intent_result != "unknown":
        intent_confidence = 0.8  # High confidence for model classification
    
    return {
        "source": "llm",
        "intent": intent_result,
        "entities": entities,
        "context_used": context_used,
        "relevant_history": relevant_history,
        "rewritten": rewritten,
        "domain": current_domain,
        "confidence": intent_confidence,
        "domain_threshold": domain_config['intent_confidence_threshold']
    }

@app.post("/store_message")
async def store_message(request: StoreMessageRequest):
    """Store a message in ChromaDB for semantic memory."""
    try:
        message_id = add_message_to_chroma(
            request.session_id, 
            request.message, 
            request.role, 
            request.timestamp
        )
        return {"status": "success", "message_id": message_id}
    except Exception as e:
        return {"status": "error", "message": str(e)}

@app.get("/get_relevant_history")
async def get_relevant_history_endpoint(query: str, session_id: Optional[str] = None, top_k: int = 5):
    """Get semantically relevant chat history for a query."""
    try:
        relevant_history = get_relevant_history(query, session_id, top_k)
        return {"relevant_history": relevant_history}
    except Exception as e:
        return {"status": "error", "message": str(e)}

@app.get("/get_session_history")
async def get_session_history_endpoint(session_id: str, limit: int = 10):
    """Get recent messages from a specific session."""
    try:
        history = get_session_history(session_id, limit)
        return {"session_history": history}
    except Exception as e:
        return {"status": "error", "message": str(e)}

@app.post("/classify_intent")
async def classify_intent(request: ClassifyIntentRequest):
    text = request.text
    # Hybrid: Exact intent lookup first
    exact_intent = lookup_intent_exact(text)
    if exact_intent:
        return {"intent": exact_intent, "source": "csv_lookup"}
    intent = classify_intent_local(text)
    return {"intent": intent, "source": "model"}

@app.post("/extract_entities")
async def extract_entities(request: ExtractEntitiesRequest):
    text = request.text
    doc = nlp(text)
    entities = {ent.label_: ent.text for ent in doc.ents}
    return {"entities": entities}

@app.post("/resolve_coref")
async def resolve_coref_endpoint(request: AnalyzeRequest):
    last_user = None
    if request.session_id:
        session_history = get_session_history(request.session_id, limit=2)
        if session_history:
            for msg in reversed(session_history):
                if msg["role"] == "user":
                    last_user = msg["message"]
                    break
    last_bot = request.prev_bot_response
    resolved = resolve_coref(request.text, last_bot, last_user)
    return {"resolved": resolved}

@app.post("/configure")
async def configure(request: ConfigRequest):
    global config
    
    # Configure data sources
    if request.data_sources:
        config.data_sources.clear()
        for source_config in request.data_sources:
            config.add_data_source(DataSourceConfig(**source_config))
        data_manager.sources.clear()
        for source_config in config.data_sources:
            data_manager.add_source(source_config)
    
    # Configure intent classification
    if request.intent_config:
        intent_config = IntentConfig(**request.intent_config)
        config.set_intent_config(intent_config)
        intent_manager.setup(intent_config)
    
    # Configure semantic search
    if request.semantic_config:
        semantic_config = SemanticConfig(**request.semantic_config)
        config.set_semantic_config(semantic_config)
    
    # Set default strategy
    if request.default_strategy:
        config.default_strategy = AnalysisStrategy(request.default_strategy)
    
    return {"status": "configured", "config": {
        "data_sources": len(config.data_sources),
        "intent_enabled": intent_manager.enabled,
        "semantic_config": config.semantic_config,
        "default_strategy": config.default_strategy.value
    }}

@app.get("/health")
async def health():
    return {
        "status": "healthy",
        "spacy_model": nlp.meta['name'] if nlp else None,
        "data_sources": len(data_manager.sources),
        "intent_enabled": intent_manager.enabled,
        "chroma_connected": True
    }

@app.post("/change_domain")
async def change_domain(request: DomainChangeRequest):
    global current_domain, df, questions, answers, question_docs, intent_lookup
    
    if request.domain not in DOMAIN_CONFIGS:
        return {"error": f"Domain '{request.domain}' not found. Available domains: {list(DOMAIN_CONFIGS.keys())}"}
    
    current_domain = request.domain
    config = get_domain_config()
    
    # Reload data based on new domain
    if config['csv_path'] and os.path.exists(config['csv_path']):
        df = pd.read_csv(config['csv_path'])
        questions = df['Question'].tolist()
        answers = df['Answer'].tolist()
        question_docs = [nlp(q) for q in questions]
    else:
        questions = []
        answers = []
        question_docs = []
    
    # Reload intent lookup
    if config['intent_lookup_csv'] and os.path.exists(config['intent_lookup_csv']):
        intent_df = pd.read_csv(config['intent_lookup_csv'])
        intent_lookup = {str(q).strip().lower(): i for q, i in zip(intent_df['text'], intent_df['intent'])}
    else:
        intent_lookup = {}
    
    return {
        "status": "domain_changed",
        "domain": current_domain,
        "config": config,
        "questions_loaded": len(questions),
        "intents_loaded": len(intent_lookup)
    }

@app.get("/get_domains")
async def get_domains():
    return {
        "current_domain": current_domain,
        "available_domains": list(DOMAIN_CONFIGS.keys()),
        "domain_configs": DOMAIN_CONFIGS
    }

# Initialize with default ERP configuration
def initialize_default_config():
    # Add ERP data source
    erp_config = DataSourceConfig(
        name="erp_knowledge",
        csv_path="../../ChatBot.Server/Data/erp_case_data_expanded.csv",
        similarity_threshold=0.75,
        max_results=5,
        enabled=True
    )
    config.add_data_source(erp_config)
    
    # Add ERP intent configuration
    intent_config = IntentConfig(
        model_path="intent_model",
        lookup_csv_path="../../intent_training/erp_intents.csv",
        confidence_threshold=0.5,
        enabled=True
    )
    config.set_intent_config(intent_config)
    
    # Set semantic configuration
    semantic_config = SemanticConfig(
        similarity_threshold=0.6,
        max_history_results=5,
        use_coreference=True,
        enabled=True
    )
    config.set_semantic_config(semantic_config)
    
    # Initialize managers
    data_manager.add_source(erp_config)
    intent_manager.setup(intent_config)
    
    print("[Config] Default ERP configuration loaded")

# Initialize on startup
initialize_default_config()

if __name__ == "__main__":
    import uvicorn
    uvicorn.run("erp_nlp_service:app", host="127.0.0.1", port=8000, reload=True)