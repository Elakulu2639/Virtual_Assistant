import React from 'react';
import SendRoundedIcon from '@mui/icons-material/SendRounded';
import './ChatInput.css';

function ChatInput({ input, setInput, onSend, disabled }) {
  return (
    <div className="chatbot-input chatbot-input-bottom chatbot-input-fixed">
      <div className="chatbot-input-container">
        <textarea
          className="chatbot-input"
          placeholder="Ask a question..."
          value={input}
          onChange={e => setInput(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Enter' && !e.shiftKey && input.trim()) {
              e.preventDefault();
              onSend(input);
            }
          }}
          aria-label="Type your message"
          rows={1}
          style={{ resize: 'none' }}
        />
        <button
          onClick={() => input.trim() && onSend(input)}
          disabled={disabled}
          aria-label="Send message"
          className={`chatbot-send-btn${disabled ? ' disabled' : ''}`}
        >
          <SendRoundedIcon fontSize="medium" />
        </button>
      </div>
    </div>
  );
}

export default ChatInput; 