import React from 'react';
import Avatar from '@mui/material/Avatar';
import ReactMarkdown from 'react-markdown';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import PersonIcon from '@mui/icons-material/Person';
import './MessageBubble.css';

const BOT_AVATAR = '/cropped_circle_image.png';

const MessageBubble = React.forwardRef(function MessageBubble({ msg, idx, onCopy, copiedIdx, isTyping, botAvatar, showTimestamp }, ref) {
  // Hide empty bot bubbles unless it's a typing indicator
  if (msg.from === 'bot' && !msg.text && !isTyping) {
    return null;
  }
  return (
    <div
      ref={ref}
      key={idx}
      className={`chatbot-message ${msg.from}`}
    >
      {(msg.from === 'bot' ? (
        <Avatar src={botAvatar || BOT_AVATAR} className="message-avatar message-avatar-bot" />
      ) : (
        <Avatar className="message-avatar user-avatar">
          <PersonIcon style={{ color: '#fff' }} />
        </Avatar>
      ))}
      <div
        className={`chatbot-bubble message-content${msg.from === 'user' ? ' user' : ''}${msg.error ? ' error' : ''}`}
        style={{ position: 'relative' }}
      >
        {isTyping ? (
          <div className="chatbot-typing">
            <span>typing</span>
            <span className="chatbot-typing-dots">
              <span>.</span><span>.</span><span>.</span>
            </span>
          </div>
        ) : msg.from === 'bot' ? (
          <>
            <ReactMarkdown>{msg.text}</ReactMarkdown>
            <button
              className="chatbot-copy-btn"
              title="Copy to clipboard"
              onClick={() => onCopy(msg.text, idx)}
              style={{ position: 'absolute', top: 8, right: 8, background: 'none', border: 'none', cursor: 'pointer', padding: 0 }}
            >
              <ContentCopyIcon fontSize="small" style={{ color: '#888' }} />
            </button>
            {copiedIdx === idx && <span className="chatbot-copied-feedback">Copied!</span>}
          </>
        ) : msg.text}
      </div>
      {showTimestamp && (
        <div className="chatbot-timestamp">{new Date(msg.timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</div>
      )}
    </div>
  );
});

export default MessageBubble; 