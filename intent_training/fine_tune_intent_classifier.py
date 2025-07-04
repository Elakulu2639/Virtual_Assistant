import pandas as pd
from datasets import Dataset, ClassLabel
from transformers import AutoTokenizer, AutoModelForSequenceClassification, TrainingArguments, Trainer
import numpy as np
import torch

# 1. Load your CSV
df = pd.read_csv("erp_intents.csv")
df = df.dropna(subset=["text", "intent"])
df = df.reset_index(drop=True)

# 2. Encode intent labels
unique_intents = sorted(df["intent"].unique())
intent2id = {intent: i for i, intent in enumerate(unique_intents)}
id2intent = {i: intent for intent, i in intent2id.items()}
df["label"] = df["intent"].map(intent2id)

# 3. Convert to HuggingFace Dataset
dataset = Dataset.from_pandas(df[["text", "label"]])

# 4. Tokenize
tokenizer = AutoTokenizer.from_pretrained("distilbert-base-uncased")
def tokenize(batch):
    return tokenizer(batch["text"], truncation=True, padding="max_length", max_length=64)
dataset = dataset.map(tokenize, batched=True)

# 5. Train/Test Split (optional, here we use all for training)
train_dataset = dataset

# 6. Model
model = AutoModelForSequenceClassification.from_pretrained(
    "distilbert-base-uncased", num_labels=len(unique_intents)
)

# 7. Training Arguments
training_args = TrainingArguments(
    output_dir="./intent_model",
    num_train_epochs=4,
    per_device_train_batch_size=8,
    logging_dir="./logs",
    learning_rate=2e-5,
    weight_decay=0.01,
    save_total_limit=1,  # Only keep the last checkpoint
)

# 8. Trainer
trainer = Trainer(
    model=model,
    args=training_args,
    train_dataset=train_dataset,
    tokenizer=tokenizer,
)

# 9. Train
trainer.train()

# 10. Save model and tokenizer
model.save_pretrained("./intent_model")
tokenizer.save_pretrained("./intent_model")

# 11. Save label mapping
import json
with open("./intent_model/id2intent.json", "w") as f:
    json.dump(id2intent, f)
with open("./intent_model/intent2id.json", "w") as f:
    json.dump(intent2id, f)

print("Training complete. Model and tokenizer saved in ./intent_model/")