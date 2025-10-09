import React, { useState, useRef, useEffect } from 'react';
import './ChatPanel.css';
import adminApi from '../services/adminApi';

interface Message {
  id: string;
  text: string;
  sender: 'user' | 'assistant';
  timestamp: Date;
  suggestions?: string[];
}

interface ChatPanelProps {
  isOpen: boolean;
  onClose: () => void;
}

const ChatPanel: React.FC<ChatPanelProps> = ({ isOpen, onClose }) => {
  const [messages, setMessages] = useState<Message[]>([
    {
      id: '1',
      text: 'Hello! I can help you with platform management, template creation, environment deployment, and Azure resource queries. What would you like to know?',
      sender: 'assistant',
      timestamp: new Date(),
      suggestions: [
        'Show me my active environments',
        'Create a new AKS template',
        'Estimate costs for deployment',
        'List available templates'
      ]
    }
  ]);
  const [inputText, setInputText] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const handleSend = async () => {
    if (!inputText.trim() || isLoading) return;

    const userMessage: Message = {
      id: Date.now().toString(),
      text: inputText,
      sender: 'user',
      timestamp: new Date()
    };

    setMessages(prev => [...prev, userMessage]);
    const messageText = inputText; // Save before clearing
    setInputText('');
    setIsLoading(true);

    try {
      // Call Platform API chat endpoint
      const response = await adminApi.sendChatMessage(messageText);
      
      // Check if the response was successful
      if (!response.Success) {
        throw new Error(response.Error || 'Chat service returned an error');
      }
      
      const assistantMessage: Message = {
        id: (Date.now() + 1).toString(),
        text: response.Response || 'No response received',
        sender: 'assistant',
        timestamp: new Date(),
        suggestions: response.SuggestedActions || []
      };
      
      setMessages(prev => [...prev, assistantMessage]);
      setIsLoading(false);
    } catch (error: any) {
      console.error('Chat error:', error);
      
      let errorText = 'Sorry, I encountered an error processing your message.';
      
      // Provide more specific error messages
      if (error.code === 'ERR_NETWORK' || error.message?.includes('Network Error')) {
        errorText = 'Unable to connect to the chat service. Please ensure the Platform API is running on port 7001.';
      } else if (error.response?.status === 404) {
        errorText = 'Chat endpoint not found. The Platform API may not be configured correctly.';
      } else if (error.response?.status === 500) {
        errorText = 'The chat service encountered an internal error. Please try again or check the logs.';
      }
      
      const errorMessage: Message = {
        id: (Date.now() + 1).toString(),
        text: errorText,
        sender: 'assistant',
        timestamp: new Date()
      };
      setMessages(prev => [...prev, errorMessage]);
      setIsLoading(false);
    }
  };

  const handleSuggestionClick = (suggestion: string) => {
    setInputText(suggestion);
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  if (!isOpen) return null;

  return (
    <div className="chat-panel">
      <div className="chat-header">
        <div className="chat-title">
          <span className="chat-icon">💬</span>
          <h3>Platform Assistant</h3>
        </div>
        <button className="close-button" onClick={onClose}>✕</button>
      </div>

      <div className="chat-messages">
        {messages.map((message) => (
          <div key={message.id} className={`message ${message.sender}`}>
            <div className="message-content">
              <div className="message-text">{message.text}</div>
              <div className="message-time">
                {message.timestamp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
              </div>
            </div>
            {message.suggestions && message.suggestions.length > 0 && (
              <div className="message-suggestions">
                {message.suggestions.map((suggestion, idx) => (
                  <button
                    key={idx}
                    className="suggestion-chip"
                    onClick={() => handleSuggestionClick(suggestion)}
                  >
                    {suggestion}
                  </button>
                ))}
              </div>
            )}
          </div>
        ))}
        {isLoading && (
          <div className="message assistant">
            <div className="message-content">
              <div className="typing-indicator">
                <span></span>
                <span></span>
                <span></span>
              </div>
            </div>
          </div>
        )}
        <div ref={messagesEndRef} />
      </div>

      <div className="chat-input-container">
        <textarea
          className="chat-input"
          placeholder="Ask me anything about your platform..."
          value={inputText}
          onChange={(e) => setInputText(e.target.value)}
          onKeyPress={handleKeyPress}
          rows={2}
          disabled={isLoading}
        />
        <button 
          className="send-button" 
          onClick={handleSend}
          disabled={!inputText.trim() || isLoading}
        >
          {isLoading ? '⏳' : '➤'}
        </button>
      </div>
    </div>
  );
};

export default ChatPanel;
