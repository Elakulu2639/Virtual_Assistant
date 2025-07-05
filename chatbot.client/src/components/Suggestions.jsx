import React from 'react';
import './Suggestions.css';

function Suggestions({ suggestions, onSuggestion }) {
  if (!suggestions.length) return null;
  return (
    <div className="chatbot-suggestion-bubbles">
      {suggestions.map((s, idx) => (
        <button
          key={idx}
          className="chatbot-suggestion-bubble"
          onClick={() => onSuggestion(s)}
          tabIndex={0}
        >
          {s}
        </button>
      ))}
    </div>
  );
}

export default Suggestions; 