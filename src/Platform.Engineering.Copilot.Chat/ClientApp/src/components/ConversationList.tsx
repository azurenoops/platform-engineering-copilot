import React, { useState, useEffect } from 'react';
import { Search, Plus, MessageCircle, Trash2 } from 'lucide-react';
import { Conversation } from '../types/chat';
import { chatApi } from '../services/chatApi';

interface ConversationListProps {
  conversations: Conversation[];
  selectedConversationId: string | null;
  onSelectConversation: (id: string) => void;
  onNewConversation: () => void;
  onDeleteConversation: (id: string) => void;
  loading: boolean;
}

export const ConversationList: React.FC<ConversationListProps> = ({
  conversations,
  selectedConversationId,
  onSelectConversation,
  onNewConversation,
  onDeleteConversation,
  loading
}) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [filteredConversations, setFilteredConversations] = useState<Conversation[]>([]);

  useEffect(() => {
    if (!searchTerm.trim()) {
      setFilteredConversations(conversations);
    } else {
      const filtered = conversations.filter(conv =>
        conv.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
        conv.description?.toLowerCase().includes(searchTerm.toLowerCase())
      );
      setFilteredConversations(filtered);
    }
  }, [conversations, searchTerm]);

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const now = new Date();
    const diffTime = Math.abs(now.getTime() - date.getTime());
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));

    if (diffDays === 1) {
      return 'Today';
    } else if (diffDays === 2) {
      return 'Yesterday';
    } else if (diffDays <= 7) {
      return `${diffDays - 1} days ago`;
    } else {
      return date.toLocaleDateString();
    }
  };

  if (loading) {
    return (
      <div className="flex flex-col h-full p-4">
        <div className="flex items-center justify-center flex-1 text-gray-500">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-500"></div>
          <span className="ml-3">Loading conversations...</span>
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full">
      <div className="flex items-center justify-between p-4 pb-3">
        <h2 className="text-lg font-semibold text-gray-800">Conversations</h2>
        <button 
          className="p-2 rounded-lg bg-blue-50 hover:bg-blue-100 transition-colors duration-200 text-blue-600"
          onClick={onNewConversation}
          title="Start new conversation"
        >
          <Plus size={16} />
        </button>
      </div>

      <div className="px-4 pb-4">
        <input
          type="text"
          className="w-full px-3 py-2 bg-white border border-gray-300 rounded-lg placeholder-gray-400 text-gray-800 focus:outline-none focus:ring-2 focus:ring-blue-400 focus:border-transparent"
          placeholder="Search conversations..."
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
        />
      </div>

      <div className="flex-1 overflow-y-auto px-2">
        {filteredConversations.length === 0 && !loading ? (
          <div className="flex flex-col items-center justify-center py-8 text-center">
            <MessageCircle size={32} className="opacity-50 mb-4 text-gray-400" />
            <p className="text-sm text-gray-500">
              {searchTerm ? 'No conversations found' : 'No conversations yet'}
            </p>
          </div>
        ) : (
          filteredConversations.map((conversation) => (
            <div
              key={conversation.id}
              className={`p-3 mx-2 mb-2 rounded-lg transition-all duration-200 hover:bg-gray-50 group ${
                selectedConversationId === conversation.id 
                  ? 'bg-blue-50 border border-blue-200 shadow-md' 
                  : 'bg-white border border-gray-200'
              }`}
            >
              <div 
                className="cursor-pointer"
                onClick={() => onSelectConversation(conversation.id)}
              >
                <div className="flex justify-between items-start mb-1">
                  <div className="font-medium text-gray-800 truncate flex-1">
                    {conversation.title || 'New Conversation'}
                  </div>
                  <button
                    onClick={(e) => {
                      e.stopPropagation();
                      if (window.confirm('Are you sure you want to delete this conversation?')) {
                        onDeleteConversation(conversation.id);
                      }
                    }}
                    className="opacity-0 group-hover:opacity-100 transition-opacity duration-200 p-1 hover:bg-red-100 rounded text-red-600 hover:text-red-800 ml-2"
                    title="Delete conversation"
                  >
                    <Trash2 size={14} />
                  </button>
                </div>
                <div className="flex justify-between text-xs text-gray-500 mb-1">
                  <span>{conversation.messageCount || 0} messages</span>
                  <span>{formatDate(conversation.updatedAt)}</span>
                </div>
                {conversation.description && (
                  <div className="text-xs text-gray-600 mt-1 truncate">
                    {conversation.description}
                  </div>
                )}
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
};