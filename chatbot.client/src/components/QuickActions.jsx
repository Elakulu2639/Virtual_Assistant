import React from 'react';
import './QuickActions.css';

function QuickActions({ actions, onAction }) {
  return (
    <div className="chatbot-quick-actions-section" aria-label="Quick Actions">
      <div className="chatbot-quick-actions-label">Quick Actions</div>
      <div className="chatbot-quick-actions-grid">
        {actions.map((action, idx) => (
          <button
            key={idx}
            className="chatbot-quick-action-btn"
            onClick={() => onAction(action.message)}
            aria-label={action.label}
          >
            <span className="chatbot-quick-action-icon">{action.icon}</span>
            <span className="chatbot-quick-action-text">{action.label}</span>
          </button>
        ))}
      </div>
    </div>
  );
}

export default QuickActions; 