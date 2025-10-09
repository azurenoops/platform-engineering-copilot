import React, { useState } from 'react';
import { Menu, MessageSquare, Settings, X } from 'lucide-react';

interface HeaderProps {
  onToggleSidebar: () => void;
  sidebarOpen: boolean;
  currentConversationTitle?: string;
}

export const Header: React.FC<HeaderProps> = ({
  onToggleSidebar,
  sidebarOpen,
  currentConversationTitle
}) => {
  const [showSettings, setShowSettings] = useState(false);

  return (
    <>
      {/* Settings Modal */}
      {showSettings && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg p-6 w-96 max-w-90vw">
            <div className="flex justify-between items-center mb-4">
              <h2 className="text-lg font-semibold text-gray-800">Settings</h2>
              <button
                onClick={() => setShowSettings(false)}
                className="p-1 hover:bg-gray-100 rounded transition-colors"
              >
                <X size={20} className="text-gray-600" />
              </button>
            </div>
            
            <div className="space-y-4">
              <div>
                <h3 className="font-medium text-gray-700 mb-2">About</h3>
                <p className="text-sm text-gray-600">Platform Engineering Copilot</p>
                <p className="text-xs text-gray-500 mt-1">Version 1.0.0</p>
              </div>
              
              <div>
                <h3 className="font-medium text-gray-700 mb-2">Features</h3>
                <ul className="text-sm text-gray-600 space-y-1">
                  <li>• ATO Compliance Scanning</li>
                  <li>• Azure Resource Discovery</li>
                  <li>• Container Deployment</li>
                  <li>• Cost Monitoring</li>
                  <li>• Security Assessment</li>
                </ul>
              </div>
              
              <div>
                <h3 className="font-medium text-gray-700 mb-2">Keyboard Shortcuts</h3>
                <ul className="text-sm text-gray-600 space-y-1">
                  <li><kbd className="bg-gray-100 px-1 rounded text-xs">Ctrl+K</kbd> Toggle sidebar</li>
                  <li><kbd className="bg-gray-100 px-1 rounded text-xs">Ctrl+N</kbd> New conversation</li>
                  <li><kbd className="bg-gray-100 px-1 rounded text-xs">Enter</kbd> Send message</li>
                  <li><kbd className="bg-gray-100 px-1 rounded text-xs">Shift+Enter</kbd> New line</li>
                </ul>
              </div>
            </div>
            
            <div className="mt-6 flex justify-end">
              <button
                onClick={() => setShowSettings(false)}
                className="px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 transition-colors"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Header */}
    <div className="flex items-center justify-between h-14 px-4 bg-white/80 backdrop-blur-md border-b border-gray-300 shadow-sm">
      <div className="flex items-center gap-3">
        <button 
          className="p-2 rounded-lg hover:bg-gray-100 transition-colors duration-200 text-gray-700" 
          onClick={onToggleSidebar}
          title={sidebarOpen ? "Close sidebar" : "Open sidebar"}
        >
          <Menu size={20} />
        </button>
        <MessageSquare size={24} className="text-blue-600" />
        <h1 className="text-lg font-semibold truncate text-gray-800">
          {currentConversationTitle || 'Platform Engineering Copilot'}
        </h1>
      </div>
      <div className="flex items-center gap-2">
        <button 
          className="p-2 rounded-lg hover:bg-gray-100 transition-colors duration-200 text-gray-700"
          onClick={() => setShowSettings(true)}
          title="Settings"
        >
          <Settings size={20} />
        </button>
      </div>
    </div>
    </>
  );
};