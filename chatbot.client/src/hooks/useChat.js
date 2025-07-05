import { useState, useEffect, useRef } from 'react';
import { sendMessage as apiSendMessage, fetchWelcome } from '../api/chat';

const SUGGESTIONS = [
  'How do I reset my password?',
  'Show me the leave policy.',
  'How do I create a new user?',
  'What is the approval workflow?',
  'How do I access reports?',
  'Show me best practices.',
  'How do I update my profile?',
];

export default function useChat() {
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState('');
  const [isTyping, setIsTyping] = useState(false);
  const [sessionId, setSessionId] = useState(null);
  const [suggestions, setSuggestions] = useState([]);
  const [copiedIdx, setCopiedIdx] = useState(null);
  const [showQuickActions, setShowQuickActions] = useState(true);
  const [error, setError] = useState(null);
  const chatBoxRef = useRef(null);

  useEffect(() => {
    async function getWelcome() {
      const res = await fetchWelcome();
      if (res && res.data) {
        setMessages([{ from: 'bot', text: res.data, timestamp: new Date() }]);
        if (res.sessionId) setSessionId(res.sessionId);
      } else {
        setMessages([{ from: 'bot', text: "Hello! I'm your ERP Assistant. How can I help you today?", timestamp: new Date() }]);
      }
      setShowQuickActions(true);
    }
    getWelcome();
  }, []);

  useEffect(() => {
    if (chatBoxRef.current) {
      chatBoxRef.current.scrollTop = chatBoxRef.current.scrollHeight;
    }
  }, [messages, isTyping]);

  useEffect(() => {
    const inputValue = input.trim().toLowerCase();
    setSuggestions(
      inputValue.length === 0
        ? []
        : SUGGESTIONS.filter((s) => s.toLowerCase().includes(inputValue))
    );
  }, [input]);

  const sendMessage = async (msg = null) => {
    const text = msg || input;
    if (!text.trim()) return;
    setInput('');
    setIsTyping(true);
    setShowQuickActions(false);
    setMessages((prev) => [...prev, { from: 'user', text, timestamp: new Date() }]);
    try {
      const response = await apiSendMessage(text, sessionId);
      if (response && response.data) {
        if (!sessionId && response.sessionId) setSessionId(response.sessionId);
        const botMsg = { from: 'bot', text: response.data, timestamp: new Date() };
        setMessages((prev) => [...prev, botMsg]);
      } else {
        setMessages((prev) => [...prev, { from: 'bot', text: response.error || 'No response from server.', timestamp: new Date(), error: true }]);
        setError(response.error || 'No response from server.');
      }
    } catch (error) {
      setMessages((prev) => [...prev, { from: 'bot', text: `An error occurred: ${error.message}`, timestamp: new Date(), error: true }]);
      setError(`An error occurred: ${error.message}`);
    } finally {
      setIsTyping(false);
    }
  };

  const handleCopy = (text, idx) => {
    navigator.clipboard.writeText(text);
    setCopiedIdx(idx);
    setTimeout(() => setCopiedIdx(null), 1200);
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

  const dismissError = () => setError(null);

  return {
    messages,
    input,
    setInput,
    isTyping,
    sessionId,
    suggestions,
    copiedIdx,
    chatBoxRef,
    sendMessage,
    handleCopy,
    restartChat,
    setMessages,
    showQuickActions,
    setShowQuickActions,
    error,
    dismissError,
  };
} 