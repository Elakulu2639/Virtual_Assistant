import spacy
from spacy.tokens import DocBin
import json

# Load your base model (use 'en_core_web_trf' or 'en_core_web_sm' as needed)
nlp = spacy.blank("en")

# Load the annotated data
with open("erp_ner_training_data.json", "r", encoding="utf-8") as f:
    training_data = json.load(f)

doc_bin = DocBin()
for entry in training_data:
    text = entry["text"]
    entities = entry["entities"]
    doc = nlp.make_doc(text)
    ents = []
    for start, end, label in entities:
        span = doc.char_span(start, end, label=label)
        if span is not None:
            ents.append(span)
        else:
            print(f"Skipping entity: {label} in '{text[start:end]}' (span could not be created)")
    doc.ents = ents
    doc_bin.add(doc)

doc_bin.to_disk("erp_ner_training_data.spacy")
print("Saved spaCy training data to erp_ner_training_data.spacy") 