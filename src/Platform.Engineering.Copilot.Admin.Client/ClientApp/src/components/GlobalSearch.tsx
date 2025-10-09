import React, { useState, useEffect, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import adminApi, { Template, EnvironmentListResponse } from '../services/adminApi';
import './GlobalSearch.css';

interface SearchResult {
  id: string;
  title: string;
  subtitle: string;
  type: 'template' | 'environment' | 'page';
  path: string;
  icon: string;
}

interface GlobalSearchProps {
  isOpen: boolean;
  onClose: () => void;
}

const GlobalSearch: React.FC<GlobalSearchProps> = ({ isOpen, onClose }) => {
  const navigate = useNavigate();
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<SearchResult[]>([]);
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [loading, setLoading] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  // Static page results
  const pageResults: SearchResult[] = [
    { id: 'dashboard', title: 'Dashboard', subtitle: 'Overview and statistics', type: 'page', path: '/', icon: '📊' },
    { id: 'catalog', title: 'Service Catalog', subtitle: 'Browse all service templates', type: 'page', path: '/catalog', icon: '�' },
    { id: 'environments', title: 'Environments', subtitle: 'View all environments', type: 'page', path: '/environments', icon: '🌐' },
    { id: 'infrastructure', title: 'Infrastructure', subtitle: 'Provision resources', type: 'page', path: '/infrastructure', icon: '🏗️' },
    { id: 'costs', title: 'Cost Insights', subtitle: 'Track cloud spending', type: 'page', path: '/costs', icon: '💰' },
    { id: 'costs', title: 'Cost Insights', subtitle: 'Track cloud spending', type: 'page', path: '/costs', icon: '�' },
    { id: 'insights', title: 'Insights', subtitle: 'Analytics and metrics', type: 'page', path: '/insights', icon: '💡' },
  ];

  useEffect(() => {
    if (isOpen && inputRef.current) {
      inputRef.current.focus();
    }
  }, [isOpen]);

  useEffect(() => {
    if (query.length > 0) {
      performSearch(query);
    } else {
      setResults(pageResults);
    }
  }, [query]);

  const performSearch = async (searchQuery: string) => {
    setLoading(true);
    try {
      const lowerQuery = searchQuery.toLowerCase();
      const searchResults: SearchResult[] = [];

      // Search pages
      const matchingPages = pageResults.filter(page =>
        page.title.toLowerCase().includes(lowerQuery) ||
        page.subtitle.toLowerCase().includes(lowerQuery)
      );
      searchResults.push(...matchingPages);

      // Search templates
      try {
        const templates = await adminApi.listTemplates();
        const matchingTemplates = templates
          .filter((t: Template) =>
            t.name.toLowerCase().includes(lowerQuery) ||
            t.description?.toLowerCase().includes(lowerQuery) ||
            t.templateType?.toLowerCase().includes(lowerQuery)
          )
          .slice(0, 5)
          .map((t: Template) => ({
            id: `template-${t.id}`,
            title: t.name,
            subtitle: `${t.templateType} template - ${t.description || 'No description'}`,
            type: 'template' as const,
            path: `/templates/${t.id}`,
            icon: '📄'
          }));
        searchResults.push(...matchingTemplates);
      } catch (err) {
        console.error('Failed to search templates:', err);
      }

      // Search environments
      try {
        const envResponse = await adminApi.listEnvironments();
        const matchingEnvs = envResponse.environments
          .filter((env: any) =>
            env.name.toLowerCase().includes(lowerQuery) ||
            env.environmentType?.toLowerCase().includes(lowerQuery) ||
            env.location?.toLowerCase().includes(lowerQuery) ||
            env.resourceGroup?.toLowerCase().includes(lowerQuery)
          )
          .slice(0, 5)
          .map((env: any) => ({
            id: `env-${env.id}`,
            title: env.name,
            subtitle: `${env.environmentType} in ${env.location} - ${env.status}`,
            type: 'environment' as const,
            path: `/environments/${env.name}`,
            icon: '🌐'
          }));
        searchResults.push(...matchingEnvs);
      } catch (err) {
        console.error('Failed to search environments:', err);
      }

      setResults(searchResults);
      setSelectedIndex(0);
    } catch (err) {
      console.error('Search failed:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    switch (e.key) {
      case 'ArrowDown':
        e.preventDefault();
        setSelectedIndex(prev => (prev + 1) % results.length);
        break;
      case 'ArrowUp':
        e.preventDefault();
        setSelectedIndex(prev => (prev - 1 + results.length) % results.length);
        break;
      case 'Enter':
        e.preventDefault();
        if (results[selectedIndex]) {
          handleSelect(results[selectedIndex]);
        }
        break;
      case 'Escape':
        e.preventDefault();
        onClose();
        break;
    }
  };

  const handleSelect = (result: SearchResult) => {
    navigate(result.path);
    onClose();
    setQuery('');
  };

  if (!isOpen) return null;

  return (
    <div className="search-overlay" onClick={onClose}>
      <div className="search-modal" onClick={(e) => e.stopPropagation()}>
        <div className="search-header">
          <div className="search-input-container">
            <span className="search-icon">🔍</span>
            <input
              ref={inputRef}
              type="text"
              className="search-input"
              placeholder="Search templates, environments, pages..."
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={handleKeyDown}
            />
            {query && (
              <button className="search-clear" onClick={() => setQuery('')}>
                ✕
              </button>
            )}
          </div>
          <div className="search-hints">
            <span className="hint">↑↓ Navigate</span>
            <span className="hint">↵ Select</span>
            <span className="hint">Esc Close</span>
          </div>
        </div>

        <div className="search-results">
          {loading && (
            <div className="search-loading">Searching...</div>
          )}
          
          {!loading && results.length === 0 && query && (
            <div className="search-empty">
              <p>No results found for "{query}"</p>
              <small>Try searching for templates, environments, or pages</small>
            </div>
          )}

          {!loading && results.length > 0 && (
            <div className="results-list">
              {results.map((result, index) => (
                <div
                  key={result.id}
                  className={`result-item ${index === selectedIndex ? 'selected' : ''}`}
                  onClick={() => handleSelect(result)}
                  onMouseEnter={() => setSelectedIndex(index)}
                >
                  <span className="result-icon">{result.icon}</span>
                  <div className="result-content">
                    <div className="result-title">{result.title}</div>
                    <div className="result-subtitle">{result.subtitle}</div>
                  </div>
                  <span className="result-type">{result.type}</span>
                </div>
              ))}
            </div>
          )}

          {!query && (
            <div className="search-tips">
              <p>💡 Quick tips:</p>
              <ul>
                <li>Search by template name, type, or description</li>
                <li>Find environments by name, location, or status</li>
                <li>Navigate to pages quickly</li>
                <li>Use <kbd>Ctrl/Cmd + K</kbd> to open search anytime</li>
              </ul>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default GlobalSearch;
