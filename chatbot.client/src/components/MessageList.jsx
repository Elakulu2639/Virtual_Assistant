import React, { useEffect, useRef } from 'react';
import MessageBubble from './MessageBubble';
import './MessageList.css';

function MessageList({ messages, onCopy, copiedIdx, isTyping, botAvatar }) {
  const lastMessageRef = useRef(null);

  // Auto-scroll to bottom when messages or typing changes
  useEffect(() => {
    if (lastMessageRef.current) {
      lastMessageRef.current.scrollIntoView({ behavior: 'smooth' });
    }
  }, [messages, isTyping]);

  // Add typing indicator as a virtual message if present
  const displayMessages = isTyping
    ? [...messages, { from: 'bot', text: '', timestamp: new Date(), isTyping: true }]
    : messages;

  return (
    <div className="chatbot-messages">
      {displayMessages.map((msg, idx) => {
        const nextMsg = displayMessages[idx + 1];
        const showTimestamp = !nextMsg || nextMsg.from !== msg.from;
        const isLast = idx === displayMessages.length - 1;
        return (
          <MessageBubble
            key={idx}
            msg={msg}
            idx={idx}
            onCopy={onCopy}
            copiedIdx={copiedIdx}
            botAvatar={botAvatar}
            showTimestamp={showTimestamp}
            isTyping={!!msg.isTyping}
            ref={isLast ? lastMessageRef : null}
          />
        );
      })}
    </div>
  );
}

export default MessageList; 