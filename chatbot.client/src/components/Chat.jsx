import React, { useState, useEffect, useRef } from 'react';
import axios from 'axios';
import Avatar from '@mui/material/Avatar';
import DescriptionIcon from '@mui/icons-material/Description';
import SettingsIcon from '@mui/icons-material/Settings';
import HelpIcon from '@mui/icons-material/Help';
import StarIcon from '@mui/icons-material/Star';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import SendRoundedIcon from '@mui/icons-material/SendRounded';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import ReactMarkdown from 'react-markdown';
import PersonIcon from '@mui/icons-material/Person';
import './Chat.css';

const BOT_AVATAR = '/cropped_circle_image.png';
const USER_AVATAR = null; // Or use a placeholder image or initials
const QUICK_ACTIONS = [
  { label: 'View Documentation', icon: <DescriptionIcon />, message: 'Show me the ERP documentation.' },
  { label: 'System Settings', icon: <SettingsIcon />, message: 'How do I change system settings?' },
  { label: 'Get Help', icon: <HelpIcon />, message: 'I need help with ERP.' },
  { label: 'Best Practices', icon: <StarIcon />, message: 'What are ERP best practices?' },
];
const SUGGESTIONS = [
  'How do I reset my password?',
  'Show me the leave policy.',
  'How do I create a new user?',
  'What is the approval workflow?',
  'How do I access reports?',
  'Show me best practices.',
  'How do I update my profile?',
];

function Chat() {
    const [messages, setMessages] = useState([]);
    const [input, setInput] = useState('');
    const [isTyping, setIsTyping] = useState(false);
    const [sessionId, setSessionId] = useState(null);
    const [suggestions, setSuggestions] = useState([]);
    const chatBoxRef = useRef(null);
    const [copiedIdx, setCopiedIdx] = useState(null);
    const [showQuickActions, setShowQuickActions] = useState(true);

  useEffect(() => {
    async function fetchWelcome() {
      try {
        const res = await axios.post('https://localhost:7037/api/Chat/send', {
          userMessage: '__get_welcome__',
          sessionId: null,
        });
        if (res.data && res.data.data) {
          setMessages([{ from: 'bot', text: res.data.data, timestamp: new Date() }]);
          if (res.data.sessionId) setSessionId(res.data.sessionId);
        } else {
          setMessages([{ from: 'bot', text: "Hello! I'm your ERP Assistant. How can I help you today?", timestamp: new Date() }]);
        }
      } catch {
        setMessages([{ from: 'bot', text: "Hello! I'm your ERP Assistant. How can I help you today?", timestamp: new Date() }]);
      }
    }
    fetchWelcome();
  }, []);

    useEffect(() => {
        if (chatBoxRef.current) {
            chatBoxRef.current.scrollTop = chatBoxRef.current.scrollHeight;
        }
    }, [messages]);

    useEffect(() => {
      // Update suggestions as user types
      const inputValue = input.trim().toLowerCase();
      setSuggestions(
        inputValue.length === 0
          ? []
          : SUGGESTIONS.filter((s) => s.toLowerCase().includes(inputValue))
      );
    }, [input]);

    const formatTime = (date) => {
    return new Date(date).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    };

    const restartChat = () => {
        setMessages([]);
        setInput('');
        setIsTyping(false);
        setSessionId(null);
        setShowQuickActions(true);
        setTimeout(() => {
          window.location.reload();
        }, 200);
  };

  const sendMessage = async (msg = null) => {
    const text = msg || input;
    if (!text.trim()) return;
    if (showQuickActions) setShowQuickActions(false);
        setInput('');
        setIsTyping(true);
    setMessages((prev) => [...prev, { from: 'user', text, timestamp: new Date() }]);
    try {
      const response = await axios.post('https://localhost:7037/api/Chat/send', {
        userMessage: text,
        sessionId: sessionId,
      });
      if (response.data && response.data.data) {
        if (!sessionId && response.data.sessionId) setSessionId(response.data.sessionId);
        const botMsg = { from: 'bot', text: response.data.data, timestamp: new Date() };
        setMessages((prev) => [...prev, botMsg]);
      } else {
        setMessages((prev) => [...prev, { from: 'bot', text: 'No response from server.', timestamp: new Date(), error: true }]);
      }
        } catch (error) {
      setMessages((prev) => [...prev, { from: 'bot', text: `An error occurred: ${error.message}`, timestamp: new Date(), error: true }]);
        } finally {
            setIsTyping(false);
        }
    };

  const handleCopy = (text, idx) => {
    navigator.clipboard.writeText(text);
    setCopiedIdx(idx);
    setTimeout(() => setCopiedIdx(null), 1200);
    };

    return (
    <div className="chatbot-bg">
      <div className="chatbot-center">
        <div className="chatbot-window">
          <div className="chatbot-header">
            <Avatar src={BOT_AVATAR} alt="Bot" className="chatbot-bot-avatar" />
            <span className="chatbot-title">ERP Assistant</span>
            <button className="chatbot-restart-btn" onClick={restartChat} aria-label="Restart chat">
              <RestartAltIcon fontSize="medium" />
            </button>
          </div>
          {showQuickActions && (
            <div className="chatbot-quick-actions-section">
              <div className="chatbot-quick-actions-label">Quick Actions</div>
              <div className="chatbot-quick-actions-grid">
                {QUICK_ACTIONS.map((action) => (
                  <button key={action.label} className="chatbot-quick-action-btn" onClick={() => sendMessage(action.message)}>
                    <span className="chatbot-quick-action-icon">{action.icon}</span>
                    <span className="chatbot-quick-action-text">{action.label}</span>
                  </button>
                ))}
                </div>
                    </div>
                )}
          <div className="chatbot-divider" />
          <div className="chatbot-messages" ref={chatBoxRef}>
                {messages.map((msg, idx) => (
              <div key={idx} className={`chatbot-message ${msg.from}`}>
                {msg.from === 'bot' ? (
                  <Avatar src={BOT_AVATAR} className="message-avatar message-avatar-bot" />
                ) : (
                  <Avatar className="message-avatar user-avatar">
                    <PersonIcon style={{ color: '#fff' }} />
                  </Avatar>
                )}
                <div className={`chatbot-bubble message-content${msg.from === 'user' ? ' user' : ''}${msg.error ? ' error' : ''}`}
                  style={{ position: 'relative' }}>
                  {msg.from === 'bot'
                    ? <>
                        <ReactMarkdown>{msg.text}</ReactMarkdown>
                        <button
                          className="chatbot-copy-btn"
                          title="Copy to clipboard"
                          onClick={() => handleCopy(msg.text, idx)}
                          style={{ position: 'absolute', top: 8, right: 8, background: 'none', border: 'none', cursor: 'pointer', padding: 0 }}
                        >
                          <ContentCopyIcon fontSize="small" style={{ color: '#888' }} />
                        </button>
                        {copiedIdx === idx && <span className="chatbot-copied-feedback">Copied!</span>}
                      </>
                    : msg.text}
                        </div>
                <div className="chatbot-timestamp">{formatTime(msg.timestamp)}</div>
                    </div>
                ))}
                {isTyping && (
                    <div className="chatbot-typing">
                    <span>typing</span>
                        <span className="chatbot-typing-dots">
                      <span>.</span><span>.</span><span>.</span>
                        </span>
                    </div>
                )}
            </div>
            {suggestions.length > 0 && (
              <div className="chatbot-suggestion-bubbles">
                {suggestions.map((s, idx) => (
                  <button
                    key={idx}
                    className="chatbot-suggestion-bubble"
                    onClick={() => { setInput(''); sendMessage(s); }}
                    tabIndex={0}
                  >
                    {s}
                  </button>
                ))}
              </div>
            )}
          <div className="chatbot-input chatbot-input-bottom chatbot-input-fixed">
            <div className="chatbot-input-container">
              <textarea
                className="chatbot-input"
                placeholder="Ask a qustion..."
                    value={input}
                onChange={e => setInput(e.target.value)}
                onKeyDown={e => {
                  if (e.key === 'Enter' && !e.shiftKey && input.trim()) {
                    e.preventDefault();
                    sendMessage(input);
                  }
                }}
                    aria-label="Chat input"
                rows={1}
                style={{ resize: 'none' }}
              />
              <button onClick={() => input.trim() && sendMessage(input)} disabled={!input.trim()} aria-label="Send message" className={`chatbot-send-btn${!input.trim() ? ' disabled' : ''}`}>
                <SendRoundedIcon fontSize="medium" />
                </button>
            </div>
          </div>
        </div>
            </div>
        </div>
    );
}

export default Chat;
