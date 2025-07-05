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
import Header from './Header';
import QuickActions from './QuickActions';
import MessageList from './MessageList';
import Suggestions from './Suggestions';
import ChatInput from './ChatInput';
import useChat from '../hooks/useChat';

const BOT_AVATAR = '/cropped_circle_image.png';
const USER_AVATAR = null; // Or use a placeholder image or initials
const QUICK_ACTIONS = [
  { label: 'View Documentation', icon: <DescriptionIcon />, message: 'Show me the ERP documentation.' },
  { label: 'System Settings', icon: <SettingsIcon />, message: 'How do I change system settings?' },
  { label: 'Get Help', icon: <HelpIcon />, message: 'I need help with ERP.' },
  { label: 'Best Practices', icon: <StarIcon />, message: 'What are ERP best practices?' },
];

function Chat() {
  const {
    messages,
    input,
    setInput,
    isTyping,
    suggestions,
    copiedIdx,
    chatBoxRef,
    sendMessage,
    handleCopy,
    restartChat,
    showQuickActions,
    error,
    dismissError,
  } = useChat();

  const BOT_AVATAR = '/cropped_circle_image.png';

  return (
    <div className="chatbot-bg">
      <div className="chatbot-center">
        <div className="chatbot-window">
          {error && (
            <div className="chatbot-error-banner" role="alert">
              <span>{error}</span>
              <button onClick={dismissError} className="chatbot-error-dismiss" aria-label="Dismiss error">×</button>
            </div>
          )}
          <Header onRestart={restartChat} botAvatar={BOT_AVATAR} title="ERP Assistant" />
          {showQuickActions && (
            <QuickActions actions={QUICK_ACTIONS} onAction={sendMessage} />
          )}
          <div className="chatbot-divider" />
          <div className="chatbot-messages" ref={chatBoxRef} role="log" aria-live="polite" aria-relevant="additions">
            <MessageList messages={messages} onCopy={handleCopy} copiedIdx={copiedIdx} isTyping={isTyping} botAvatar={BOT_AVATAR} />
          </div>
          <Suggestions suggestions={suggestions} onSuggestion={(s) => { setInput(''); sendMessage(s); }} />
          <ChatInput input={input} setInput={setInput} onSend={sendMessage} disabled={!input.trim()} />
        </div>
      </div>
    </div>
  );
}

export default Chat;
