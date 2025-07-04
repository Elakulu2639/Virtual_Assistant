import json
import sys
from typing import List, Dict

INPUT_FILE = "erp_ner_training_data_entities.json"  # Input: entities as strings, not spans
OUTPUT_FILE = "erp_ner_training_data.json"  # Output: entities as [start, end, label]

def find_all_spans(text: str, entity: str) -> List[int]:
    """Find all start indices of entity in text."""
    starts = []
    start = text.find(entity)
    while start != -1:
        starts.append(start)
        start = text.find(entity, start + 1)
    return starts

def main():
    with open(INPUT_FILE, "r", encoding="utf-8") as f:
        data = json.load(f)
    corrected = []
    for entry in data:
        text = entry["text"]
        entities = entry["entities"]  # List of [entity_string, label]
        spans = []
        for entity_string, label in entities:
            found = False
            for start in find_all_spans(text, entity_string):
                end = start + len(entity_string)
                spans.append([start, end, label])
                found = True
            if not found:
                print(f"WARNING: '{entity_string}' not found in: '{text}'")
        corrected.append({"text": text, "entities": spans})
    with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
        json.dump(corrected, f, indent=2)
    print(f"Wrote corrected data to {OUTPUT_FILE}")

if __name__ == "__main__":
    main() 