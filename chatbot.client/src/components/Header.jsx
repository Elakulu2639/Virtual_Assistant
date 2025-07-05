import React from 'react';
import './Header.css';
import Avatar from '@mui/material/Avatar';
import RestartAltIcon from '@mui/icons-material/RestartAlt';

function Header({ onRestart, botAvatar, title }) {
  return (
    <div className="chatbot-header">
      <Avatar src={botAvatar} alt="Bot" className="chatbot-bot-avatar" />
      <span className="chatbot-title">{title}</span>
      <button className="chatbot-restart-btn" onClick={onRestart} aria-label="Restart chat">
        <RestartAltIcon fontSize="medium" />
      </button>
    </div>
  );
}

export default Header; 