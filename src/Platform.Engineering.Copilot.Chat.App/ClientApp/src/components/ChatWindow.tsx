import React, { useState, useRef, useEffect } from 'react';
import { Send, Paperclip, Bot, User, Zap, CheckCircle, Clock, TrendingUp, Lightbulb } from 'lucide-react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeHighlight from 'rehype-highlight';
import { ChatMessage, Conversation, MessageRole, ProactiveSuggestion } from '../types/chat';
import { useChat } from '../contexts/ChatContext';

interface ChatWindowProps {
  conversation: Conversation | null;
  messages: ChatMessage[];
  onSendMessage: (content: string, attachments?: File[]) => void;
  loading: boolean;
  isTyping: boolean;
}

export const ChatWindow: React.FC<ChatWindowProps> = ({
  conversation,
  messages,
  onSendMessage,
  loading,
  isTyping
}) => {
  const [inputValue, setInputValue] = useState('');
  const [attachments, setAttachments] = useState<File[]>([]);
  const [expandedToolResults, setExpandedToolResults] = useState<Set<string>>(new Set());
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const { state } = useChat();
  const connectionStatus = state.isConnected ? 'Connected' : 'Disconnected';

  // Auto-scroll to bottom when new messages arrive
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, isTyping]);

  // Auto-resize textarea
  useEffect(() => {
    if (textareaRef.current) {
      textareaRef.current.style.height = 'auto';
      textareaRef.current.style.height = `${textareaRef.current.scrollHeight}px`;
    }
  }, [inputValue]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (inputValue.trim() || attachments.length > 0) {
      onSendMessage(inputValue.trim(), attachments);
      setInputValue('');
      setAttachments([]);
      if (textareaRef.current) {
        textareaRef.current.style.height = 'auto';
      }
    }
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSubmit(e as any);
    }
  };

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      const newFiles = Array.from(e.target.files);
      setAttachments(prev => [...prev, ...newFiles]);
    }
  };

  const removeAttachment = (index: number) => {
    setAttachments(prev => prev.filter((_, i) => i !== index));
  };

  const formatTime = (dateString: string) => {
    return new Date(dateString).toLocaleTimeString([], { 
      hour: '2-digit', 
      minute: '2-digit' 
    });
  };

  const handleSuggestionClick = (suggestion: ProactiveSuggestion) => {
    setInputValue(suggestion.suggestedPrompt);
    textareaRef.current?.focus();
  };

  const toggleToolResult = (messageId: string) => {
    setExpandedToolResults(prev => {
      const newSet = new Set(prev);
      if (newSet.has(messageId)) {
        newSet.delete(messageId);
      } else {
        newSet.add(messageId);
      }
      return newSet;
    });
  };

  if (!conversation) {
    return (
      <div className="flex flex-col h-full bg-white">
        <div className="flex flex-col items-center justify-center flex-1 text-center p-8">
          <div className="text-6xl mb-6">💬</div>
          <h2 className="text-2xl font-semibold text-gray-800 mb-4">Welcome to Platform Engineering Copilot</h2>
          <p className="text-gray-600 max-w-md">
            Select a conversation from the sidebar or start a new one to begin chatting.
          </p>
        </div>
      </div>
    );
  }

  const canSend = inputValue.trim() || attachments.length > 0;

  return (
    <div className="flex flex-col h-full bg-white">
      <div className="px-4 py-2 text-xs border-b border-gray-200 bg-gray-50">
        {connectionStatus === 'Connected' ? (
          <span className="text-green-600">✅ Real-time features active (SignalR connected)</span>
        ) : (
          <span className="text-orange-600">⚠️ Real-time features unavailable (SignalR disconnected) - Check console for details</span>
        )}
      </div>

      <div className="px-6 py-4 border-b border-gray-200 bg-gray-50">
        <h2 className="text-xl font-semibold text-gray-800">{conversation.title || 'New Conversation'}</h2>
      </div>

      <div className="flex-1 overflow-y-auto p-6 space-y-4 bg-gray-50 custom-scrollbar">
        {messages.map((message) => (
          <div key={message.id} className={`flex ${message.role === MessageRole.User ? 'justify-end' : 'justify-start'}`}>
            <div className={`max-w-[80%] ${message.role === MessageRole.User ? 'bg-blue-600 text-white' : 'bg-white text-gray-800'} rounded-lg p-4 shadow-sm border ${message.role === MessageRole.User ? 'border-blue-600' : 'border-gray-200'}`}>
              <div className="flex items-center gap-2 mb-2">
                {message.role === MessageRole.User ? (
                  <User size={16} className={message.role === MessageRole.User ? "text-blue-100" : "text-gray-600"} />
                ) : (
                  <Bot size={16} className="text-blue-600" />
                )}
                <span className={`text-sm font-medium ${message.role === MessageRole.User ? 'text-blue-100' : 'text-gray-700'}`}>
                  {message.role === MessageRole.User ? 'You' : 'Assistant'}
                </span>
              </div>
              
              <div className={`text-sm leading-relaxed ${message.role === MessageRole.User ? 'text-white' : 'text-gray-800'} markdown-content`}>
                <ReactMarkdown
                  remarkPlugins={[remarkGfm]}
                  rehypePlugins={[rehypeHighlight]}
                  components={{
                    h1: ({children}) => <h1 className="text-xl font-bold mb-3 mt-4">{children}</h1>,
                    h2: ({children}) => <h2 className="text-lg font-bold mb-2 mt-3">{children}</h2>,
                    h3: ({children}) => <h3 className="text-md font-semibold mb-2 mt-2">{children}</h3>,
                    h4: ({children}) => <h4 className="text-sm font-semibold mb-1 mt-2">{children}</h4>,
                    p: ({children}) => <p className="mb-2">{children}</p>,
                    ul: ({children}) => <ul className="list-disc list-inside mb-2 ml-4">{children}</ul>,
                    ol: ({children}) => <ol className="list-decimal list-inside mb-2 ml-4">{children}</ol>,
                    li: ({children}) => <li className="mb-1">{children}</li>,
                    code: ({className, children, ...props}) => {
                      const isInline = !className;
                      return isInline ? (
                        <code className="bg-gray-100 px-1 py-0.5 rounded text-xs font-mono text-gray-800" {...props}>
                          {children}
                        </code>
                      ) : (
                        <code className={`${className} !text-gray-100`} {...props}>
                          {children}
                        </code>
                      );
                    },
                    pre: ({children}) => (
                      <pre className="bg-gray-900 p-3 rounded-md overflow-x-auto mb-3 text-xs !text-gray-100 [&_*]:!text-gray-100 [&_*]:!text-opacity-100 [&_code]:!text-gray-100 [&_table]:!text-gray-100 [&_th]:!text-gray-100 [&_td]:!text-gray-100" style={{color: '#f3f4f6'}}>
                        {children}
                      </pre>
                    ),
                    blockquote: ({children}) => (
                      <blockquote className="border-l-4 border-blue-500 pl-4 italic mb-2">
                        {children}
                      </blockquote>
                    ),
                    table: ({children}) => (
                      <div className="overflow-x-auto mb-2">
                        <table className="min-w-full border border-gray-300 bg-white [&_th]:text-gray-900 [&_td]:text-gray-900">
                          {children}
                        </table>
                      </div>
                    ),
                    th: ({children}) => (
                      <th className="border border-gray-300 px-2 py-1 bg-gray-100 font-semibold text-left">
                        {children}
                      </th>
                    ),
                    td: ({children}) => (
                      <td className="border border-gray-300 px-2 py-1">
                        {children}
                      </td>
                    ),
                  }}
                >
                  {message.content}
                </ReactMarkdown>
              </div>
              
              {message.attachments && message.attachments.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-2">
                  {message.attachments.map((attachment, index) => (
                    <div key={index} className={`px-2 py-1 rounded text-xs flex items-center gap-1 ${message.role === MessageRole.User ? 'bg-blue-500 text-blue-100' : 'bg-gray-100 text-gray-700'}`}>
                      📎 {attachment.fileName}
                    </div>
                  ))}
                </div>
              )}

              {/* INTELLIGENT CHAT METADATA */}
              {message.role === MessageRole.Assistant && message.metadata && (
                <div className="mt-3 space-y-2">
                  {/* Intent Classification Badge */}
                  {message.metadata.intentType && (
                    <div className="flex items-center gap-2 text-xs">
                      <div className="flex items-center gap-1 px-2 py-1 bg-blue-50 text-blue-700 rounded border border-blue-200">
                        <Zap size={12} />
                        <span className="font-medium">Intent:</span>
                        <span>{message.metadata.intentType}</span>
                        {message.metadata.confidence && (
                          <span className="text-blue-600">({Math.round(message.metadata.confidence * 100)}%)</span>
                        )}
                      </div>
                      {message.metadata.processingTimeMs && (
                        <div className="flex items-center gap-1 px-2 py-1 bg-gray-50 text-gray-600 rounded border border-gray-200">
                          <Clock size={12} />
                          <span>{message.metadata.processingTimeMs}ms</span>
                        </div>
                      )}
                    </div>
                  )}

                  {/* Tool Execution Result */}
                  {message.metadata.toolExecuted && message.metadata.toolName && (
                    <div className="border border-green-200 bg-green-50 rounded-lg overflow-hidden">
                      <button
                        onClick={() => toggleToolResult(message.id)}
                        className="w-full px-3 py-2 flex items-center justify-between text-sm text-green-800 hover:bg-green-100 transition-colors"
                      >
                        <div className="flex items-center gap-2">
                          <CheckCircle size={14} className="text-green-600" />
                          <span className="font-medium">Tool Executed:</span>
                          <span className="text-green-700">{message.metadata.toolName}</span>
                        </div>
                        <span className="text-xs text-green-600">
                          {expandedToolResults.has(message.id) ? '▼' : '▶'}
                        </span>
                      </button>
                      {expandedToolResults.has(message.id) && message.metadata.toolResult && (
                        <div className="px-3 py-2 bg-white border-t border-green-200">
                          <pre className="text-xs text-gray-700 overflow-x-auto">
                            {JSON.stringify(message.metadata.toolResult, null, 2)}
                          </pre>
                        </div>
                      )}
                    </div>
                  )}

                  {/* Tool Chain Progress */}
                  {message.metadata.toolChain && (
                    <div className="border border-purple-200 bg-purple-50 rounded-lg p-3">
                      <div className="flex items-center gap-2 mb-2 text-sm text-purple-800">
                        <TrendingUp size={14} className="text-purple-600" />
                        <span className="font-medium">Multi-Step Workflow</span>
                        <span className="text-xs text-purple-600">
                          ({message.metadata.toolChain.completedSteps}/{message.metadata.toolChain.totalSteps} steps)
                        </span>
                      </div>
                      <div className="w-full bg-purple-200 rounded-full h-2 mb-2">
                        <div 
                          className="bg-purple-600 h-2 rounded-full transition-all"
                          style={{ 
                            width: `${(message.metadata.toolChain.completedSteps / message.metadata.toolChain.totalSteps) * 100}%` 
                          }}
                        />
                      </div>
                      <div className="flex items-center justify-between text-xs text-purple-700">
                        <span className="font-medium">{message.metadata.toolChain.status}</span>
                        <span>Success Rate: {Math.round(message.metadata.toolChain.successRate * 100)}%</span>
                      </div>
                    </div>
                  )}

                  {/* Proactive Suggestions */}
                  {message.metadata.suggestions && message.metadata.suggestions.length > 0 && (
                    <div className="space-y-2 pt-2">
                      <div className="flex items-center gap-2 text-xs text-gray-600">
                        <Lightbulb size={12} />
                        <span className="font-medium">Suggested Next Steps:</span>
                      </div>
                      <div className="grid gap-2">
                        {message.metadata.suggestions.map((suggestion: ProactiveSuggestion, idx: number) => (
                          <button
                            key={idx}
                            onClick={() => handleSuggestionClick(suggestion)}
                            className="text-left p-3 bg-amber-50 hover:bg-amber-100 border border-amber-200 rounded-lg transition-colors group"
                          >
                            <div className="flex items-start gap-2">
                              {suggestion.icon && (
                                <span className="text-lg flex-shrink-0">{suggestion.icon}</span>
                              )}
                              <div className="flex-1 min-w-0">
                                <div className="flex items-center gap-2 mb-1">
                                  <h4 className="text-sm font-semibold text-amber-900">{suggestion.title}</h4>
                                  <span className={`text-xs px-1.5 py-0.5 rounded ${
                                    suggestion.priority === 'High' ? 'bg-red-100 text-red-700' :
                                    suggestion.priority === 'Medium' ? 'bg-yellow-100 text-yellow-700' :
                                    'bg-green-100 text-green-700'
                                  }`}>
                                    {suggestion.priority}
                                  </span>
                                  {suggestion.confidence && (
                                    <span className="text-xs text-amber-600">
                                      {Math.round(suggestion.confidence * 100)}%
                                    </span>
                                  )}
                                </div>
                                <p className="text-xs text-amber-800 mb-2">{suggestion.description}</p>
                                <div className="text-xs text-amber-700 italic group-hover:text-amber-900">
                                  Click to try: "{suggestion.suggestedPrompt}"
                                </div>
                              </div>
                            </div>
                          </button>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              )}
              
              <div className={`text-xs mt-2 ${message.role === MessageRole.User ? 'text-blue-200' : 'text-gray-500'}`}>
                {formatTime(message.timestamp)}
              </div>
            </div>
          </div>
        ))}

        {isTyping && (
          <div className="flex justify-start">
            <div className="bg-white rounded-lg p-4 shadow-sm border border-gray-200 flex items-center gap-2">
              <Bot size={16} className="text-blue-600" />
              <div className="flex gap-1">
                <div className="w-2 h-2 bg-gray-400 rounded-full animate-bounce"></div>
                <div className="w-2 h-2 bg-gray-400 rounded-full animate-bounce" style={{ animationDelay: '0.1s' }}></div>
                <div className="w-2 h-2 bg-gray-400 rounded-full animate-bounce" style={{ animationDelay: '0.2s' }}></div>
              </div>
            </div>
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      <div className="p-4 bg-white border-t border-gray-200">
        {attachments.length > 0 && (
          <div className="mb-3 flex flex-wrap gap-2">
            {attachments.map((file, index) => (
              <div key={index} className="inline-flex items-center bg-gray-100 px-3 py-1 rounded-lg text-sm text-gray-700">
                📎 {file.name}
                <button
                  type="button"
                  onClick={() => removeAttachment(index)}
                  className="ml-2 text-gray-500 hover:text-gray-700 transition-colors"
                >
                  ×
                </button>
              </div>
            ))}
          </div>
        )}

                <form onSubmit={handleSubmit} className="flex items-end gap-3">
          <input
            type="file"
            ref={fileInputRef}
            onChange={handleFileSelect}
            multiple
            className="hidden"
          />
          
          <button
            type="button"
            className="p-2 rounded-lg bg-gray-100 hover:bg-gray-200 transition-colors duration-200 text-gray-600 hover:text-gray-800"
            onClick={() => fileInputRef.current?.click()}
            title="Attach files"
          >
            <Paperclip size={18} />
          </button>

          <div className="flex-1">
            <textarea
              ref={textareaRef}
              value={inputValue}
              onChange={(e) => setInputValue(e.target.value)}
              onKeyPress={handleKeyPress}
              placeholder="Type your message here..."
              className="w-full px-4 py-3 bg-white border border-gray-300 rounded-lg text-gray-800 placeholder-gray-500 resize-none focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              disabled={loading}
              rows={1}
            />
          </div>

          <button
            type="submit"
            className={`p-2 rounded-lg transition-all duration-200 ${
              canSend && !loading 
                ? 'bg-blue-600 hover:bg-blue-700 text-white shadow-md' 
                : 'bg-gray-100 text-gray-400 cursor-not-allowed'
            }`}
            disabled={!canSend || loading}
            title="Send message"
          >
            <Send size={18} />
          </button>
        </form>
      </div>
    </div>
  );
};

