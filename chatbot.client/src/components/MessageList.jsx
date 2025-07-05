import React, { useEffect, useRef, forwardRef } from 'react';
import { FixedSizeList as List } from 'react-window';
import MessageBubble from './MessageBubble';
import './MessageList.css';

const ITEM_HEIGHT = 72; // Approximate height, can be adjusted for your UI

const Row = forwardRef(({ data, index, style }, ref) => {
  const { messages, onCopy, copiedIdx, isTyping, botAvatar } = data;
  const msg = messages[index];
  const prevMsg = messages[index - 1];
  const nextMsg = messages[index + 1];
  const isGrouped = prevMsg && prevMsg.from === msg.from;
  const showTimestamp = !nextMsg || nextMsg.from !== msg.from;
  // Only attach ref to the last item (for auto-scroll)
  const bubbleRef = index === messages.length - 1 && !isTyping ? ref : null;
  return (
    <div style={style}>
      <MessageBubble
        msg={msg}
        idx={index}
        onCopy={onCopy}
        copiedIdx={copiedIdx}
        botAvatar={botAvatar}
        isGrouped={isGrouped}
        showTimestamp={showTimestamp}
        ref={bubbleRef}
      />
    </div>
  );
});

function MessageList({ messages, onCopy, copiedIdx, isTyping, botAvatar }) {
  const lastMessageRef = useRef(null);
  const listRef = useRef();

  // Auto-scroll to bottom when messages or typing changes
  useEffect(() => {
    if (listRef.current) {
      listRef.current.scrollToItem(messages.length - 1, 'end');
    }
  }, [messages, isTyping]);

  // Data for react-window
  const itemData = { messages, onCopy, copiedIdx, isTyping, botAvatar };
  // Add typing indicator as a virtual message if present
  const displayMessages = isTyping
    ? [...messages, { from: 'bot', text: '', timestamp: new Date(), isTyping: true }]
    : messages;

  return (
    <List
      height={400}
      width={'100%'}
      itemCount={displayMessages.length}
      itemSize={ITEM_HEIGHT}
      itemData={{ ...itemData, messages: displayMessages }}
      ref={listRef}
      style={{ background: '#fff' }}
    >
      {(props) => <Row {...props} ref={lastMessageRef} />}
    </List>
  );
}

export default MessageList; 