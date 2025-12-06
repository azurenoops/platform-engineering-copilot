import React, { useEffect, useState } from 'react';
import adminApi, { AgentConfigurationListResponse, AgentCategoryGroup, AgentConfiguration, UpdateAgentConfigurationRequest } from '../services/adminApi';
import './Agents.css';

const Agents: React.FC = () => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [agentData, setAgentData] = useState<AgentConfigurationListResponse | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string>('All');
  const [updatingAgents, setUpdatingAgents] = useState<Set<string>>(new Set());
  const [editingAgent, setEditingAgent] = useState<AgentConfiguration | null>(null);
  const [editForm, setEditForm] = useState<UpdateAgentConfigurationRequest>({});
  const [configFields, setConfigFields] = useState<Record<string, any>>({});
  const [showRawJson, setShowRawJson] = useState(false);

  useEffect(() => {
    loadAgents();
  }, []);

  const loadAgents = async () => {
    try {
      setLoading(true);
      setError(null);
      const data = await adminApi.getAgents();
      setAgentData(data);
    } catch (err: any) {
      console.error('Failed to load agents:', err);
      setError(err.response?.data?.message || 'Failed to load agent configurations');
    } finally {
      setLoading(false);
    }
  };

  const handleToggleAgent = async (agentName: string, currentStatus: boolean) => {
    setUpdatingAgents(prev => new Set(prev).add(agentName));
    try {
      await adminApi.updateAgentStatus(agentName, !currentStatus, 'admin');
      await loadAgents(); // Reload to get updated data
    } catch (err: any) {
      console.error(`Failed to toggle agent ${agentName}:`, err);
      setError(err.response?.data?.message || `Failed to update agent ${agentName}`);
    } finally {
      setUpdatingAgents(prev => {
        const next = new Set(prev);
        next.delete(agentName);
        return next;
      });
    }
  };

  const handleSyncAgents = async () => {
    try {
      setLoading(true);
      await adminApi.syncAgentConfigurations();
      await loadAgents();
    } catch (err: any) {
      console.error('Failed to sync agents:', err);
      setError(err.response?.data?.message || 'Failed to sync agent configurations');
    } finally {
      setLoading(false);
    }
  };

  const handleEditAgent = (agent: AgentConfiguration) => {
    setEditingAgent(agent);
    
    // Parse the configuration JSON into editable fields
    let parsedConfig: Record<string, any> = {};
    try {
      parsedConfig = agent.configurationJson ? JSON.parse(agent.configurationJson) : {};
    } catch (err) {
      console.error('Failed to parse config JSON:', err);
      parsedConfig = {};
    }
    
    setConfigFields(parsedConfig);
    setEditForm({
      displayName: agent.displayName,
      description: agent.description,
      isEnabled: agent.isEnabled,
      configurationJson: agent.configurationJson,
      iconName: agent.iconName,
      displayOrder: agent.displayOrder,
      dependencies: agent.dependencies || undefined,
      modifiedBy: 'admin'
    });
    setShowRawJson(false);
  };

  const handleCloseEditModal = () => {
    setEditingAgent(null);
    setEditForm({});
    setConfigFields({});
    setShowRawJson(false);
  };

  const handleSaveAgent = async () => {
    if (!editingAgent) return;

    setUpdatingAgents(prev => new Set(prev).add(editingAgent.agentName));
    try {
      // Convert config fields back to JSON string
      const updatedConfigJson = JSON.stringify(configFields);
      const finalForm = { ...editForm, configurationJson: updatedConfigJson };
      
      await adminApi.updateAgentConfiguration(editingAgent.agentName, finalForm);
      await loadAgents();
      handleCloseEditModal();
    } catch (err: any) {
      console.error(`Failed to update agent ${editingAgent.agentName}:`, err);
      setError(err.response?.data?.message || `Failed to update agent ${editingAgent.agentName}`);
    } finally {
      setUpdatingAgents(prev => {
        const next = new Set(prev);
        next.delete(editingAgent.agentName);
        return next;
      });
    }
  };

  const handleConfigFieldChange = (path: string, value: any) => {
    setConfigFields(prev => {
      const newConfig = { ...prev };
      const keys = path.split('.');
      let current: any = newConfig;
      
      for (let i = 0; i < keys.length - 1; i++) {
        if (!current[keys[i]]) current[keys[i]] = {};
        current = current[keys[i]];
      }
      
      current[keys[keys.length - 1]] = value;
      return newConfig;
    });
  };

  const handleDeleteConfigField = (path: string) => {
    setConfigFields(prev => {
      const newConfig = { ...prev };
      const keys = path.split('.');
      let current: any = newConfig;
      
      for (let i = 0; i < keys.length - 1; i++) {
        if (!current[keys[i]]) return prev;
        current = current[keys[i]];
      }
      
      delete current[keys[keys.length - 1]];
      return newConfig;
    });
  };

  const handleConfigJsonChange = (value: string) => {
    try {
      const parsed = JSON.parse(value);
      setConfigFields(parsed);
      setEditForm(prev => ({ ...prev, configurationJson: value }));
    } catch (err) {
      // Allow invalid JSON while typing
      setEditForm(prev => ({ ...prev, configurationJson: value }));
    }
  };

  const formatJson = () => {
    try {
      const formatted = JSON.stringify(configFields, null, 2);
      setEditForm(prev => ({ ...prev, configurationJson: formatted }));
    } catch (err) {
      setError('Invalid JSON format');
    }
  };

  const renderConfigField = (key: string, value: any, path: string = ''): JSX.Element => {
    const fullPath = path ? `${path}.${key}` : key;
    
    if (value === null || value === undefined) {
      return (
        <div key={fullPath} className="config-field">
          <label>{key}</label>
          <input
            type="text"
            value=""
            onChange={(e) => handleConfigFieldChange(fullPath, e.target.value)}
            className="form-input"
            placeholder="null"
          />
          <button
            type="button"
            className="delete-field-button"
            onClick={() => handleDeleteConfigField(fullPath)}
            title="Delete field"
          >
            🗑️
          </button>
        </div>
      );
    }
    
    if (typeof value === 'boolean') {
      return (
        <div key={fullPath} className="config-field">
          <label className="checkbox-label">
            <input
              type="checkbox"
              checked={value}
              onChange={(e) => handleConfigFieldChange(fullPath, e.target.checked)}
            />
            <span>{key}</span>
          </label>
          <button
            type="button"
            className="delete-field-button"
            onClick={() => handleDeleteConfigField(fullPath)}
            title="Delete field"
          >
            🗑️
          </button>
        </div>
      );
    }
    
    if (typeof value === 'number') {
      return (
        <div key={fullPath} className="config-field">
          <label>{key}</label>
          <input
            type="number"
            value={value}
            onChange={(e) => handleConfigFieldChange(fullPath, parseFloat(e.target.value) || 0)}
            className="form-input"
            step="any"
          />
          <button
            type="button"
            className="delete-field-button"
            onClick={() => handleDeleteConfigField(fullPath)}
            title="Delete field"
          >
            🗑️
          </button>
        </div>
      );
    }
    
    if (typeof value === 'string') {
      return (
        <div key={fullPath} className="config-field">
          <label>{key}</label>
          <input
            type="text"
            value={value}
            onChange={(e) => handleConfigFieldChange(fullPath, e.target.value)}
            className="form-input"
          />
          <button
            type="button"
            className="delete-field-button"
            onClick={() => handleDeleteConfigField(fullPath)}
            title="Delete field"
          >
            🗑️
          </button>
        </div>
      );
    }
    
    if (Array.isArray(value)) {
      return (
        <div key={fullPath} className="config-field-group">
          <div className="config-field-header">
            <label>{key} (Array)</label>
            <button
              type="button"
              className="delete-field-button"
              onClick={() => handleDeleteConfigField(fullPath)}
              title="Delete field"
            >
              🗑️
            </button>
          </div>
          <div className="config-array">
            {value.map((item, index) => (
              <div key={`${fullPath}[${index}]`} className="array-item">
                <input
                  type="text"
                  value={item}
                  onChange={(e) => {
                    const newArray = [...value];
                    newArray[index] = e.target.value;
                    handleConfigFieldChange(fullPath, newArray);
                  }}
                  className="form-input"
                />
                <button
                  type="button"
                  className="delete-array-item"
                  onClick={() => {
                    const newArray = value.filter((_, i) => i !== index);
                    handleConfigFieldChange(fullPath, newArray);
                  }}
                >
                  ✕
                </button>
              </div>
            ))}
            <button
              type="button"
              className="add-array-item"
              onClick={() => handleConfigFieldChange(fullPath, [...value, ''])}
            >
              + Add Item
            </button>
          </div>
        </div>
      );
    }
    
    if (typeof value === 'object') {
      return (
        <div key={fullPath} className="config-field-group">
          <div className="config-field-header">
            <label>{key}</label>
            <button
              type="button"
              className="delete-field-button"
              onClick={() => handleDeleteConfigField(fullPath)}
              title="Delete field"
            >
              🗑️
            </button>
          </div>
          <div className="config-nested">
            {Object.entries(value).map(([nestedKey, nestedValue]) =>
              renderConfigField(nestedKey, nestedValue, fullPath)
            )}
          </div>
        </div>
      );
    }
    
    return <div key={fullPath}></div>;
  };

  const getAgentIcon = (category: string, iconName?: string): string => {
    if (iconName) return iconName;
    
    // Default icons by category
    switch (category.toLowerCase()) {
      case 'core': return '🏗️';
      case 'compliance': return '✓';
      case 'cost': return '💰';
      case 'discovery': return '🔍';
      case 'security': return '🔒';
      case 'knowledge': return '📚';
      case 'environment': return '🌍';
      case 'document': return '📋';
      default: return '🤖';
    }
  };

  const getCategoryColor = (category: string): string => {
    switch (category.toLowerCase()) {
      case 'core': return '#0078d4';
      case 'compliance': return '#107c10';
      case 'cost': return '#d83b01';
      case 'discovery': return '#8661c5';
      case 'security': return '#c239b3';
      case 'knowledge': return '#00b7c3';
      case 'environment': return '#ffb900';
      case 'document': return '#00cc6a';
      default: return '#605e5c';
    }
  };

  const filteredCategories = agentData?.categories.filter(cat => {
    if (selectedCategory !== 'All' && cat.category !== selectedCategory) return false;
    if (!searchTerm) return true;
    return cat.agents.some(agent => 
      agent.displayName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      agent.agentName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      agent.description?.toLowerCase().includes(searchTerm.toLowerCase())
    );
  });

  const getFilteredAgents = (category: AgentCategoryGroup): AgentConfiguration[] => {
    if (!searchTerm) return category.agents;
    return category.agents.filter(agent =>
      agent.displayName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      agent.agentName.toLowerCase().includes(searchTerm.toLowerCase()) ||
      agent.description?.toLowerCase().includes(searchTerm.toLowerCase())
    );
  };

  const allCategories = ['All', ...(agentData?.categories.map(c => c.category) || [])];

  if (loading && !agentData) {
    return (
      <div className="agents-container">
        <div className="loading-message">
          <div className="spinner"></div>
          <p>Loading agent configurations...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="agents-container">
      <div className="agents-header">
        <div className="agents-title-section">
          <h1>🤖 Agent Management</h1>
          <p className="agents-subtitle">
            Manage and configure AI agents for platform engineering operations
          </p>
        </div>
        <div className="agents-stats">
          <div className="stat-card">
            <div className="stat-value">{agentData?.totalAgents || 0}</div>
            <div className="stat-label">Total Agents</div>
          </div>
          <div className="stat-card enabled">
            <div className="stat-value">{agentData?.enabledAgents || 0}</div>
            <div className="stat-label">Enabled</div>
          </div>
          <div className="stat-card disabled">
            <div className="stat-value">{(agentData?.totalAgents || 0) - (agentData?.enabledAgents || 0)}</div>
            <div className="stat-label">Disabled</div>
          </div>
        </div>
      </div>

      <div className="agents-controls">
        <div className="search-filter-section">
          <input
            type="text"
            className="search-input"
            placeholder="Search agents..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
          <select
            className="category-filter"
            value={selectedCategory}
            onChange={(e) => setSelectedCategory(e.target.value)}
          >
            {allCategories.map(cat => (
              <option key={cat} value={cat}>{cat}</option>
            ))}
          </select>
        </div>
        <button className="sync-button" onClick={handleSyncAgents} disabled={loading}>
          🔄 Sync All
        </button>
      </div>

      {error && (
        <div className="error-banner">
          <span className="error-icon">⚠️</span>
          {error}
          <button className="close-error" onClick={() => setError(null)}>×</button>
        </div>
      )}

      <div className="agents-content">
        {filteredCategories && filteredCategories.length > 0 ? (
          filteredCategories.map(category => {
            const filteredAgents = getFilteredAgents(category);
            if (filteredAgents.length === 0) return null;

            return (
              <div key={category.category} className="category-section">
                <div className="category-header" style={{ borderLeftColor: getCategoryColor(category.category) }}>
                  <div className="category-title">
                    <h2>{category.category}</h2>
                    <span className="category-badge">
                      {category.enabledCount}/{category.totalCount} enabled
                    </span>
                  </div>
                </div>
                <div className="agents-grid">
                  {filteredAgents.map(agent => (
                    <div
                      key={agent.agentName}
                      className={`agent-card ${agent.isEnabled ? 'enabled' : 'disabled'} ${updatingAgents.has(agent.agentName) ? 'updating' : ''}`}
                    >
                      <div className="agent-card-header">
                        <div className="agent-icon" style={{ backgroundColor: getCategoryColor(category.category) }}>
                          {getAgentIcon(category.category, agent.iconName)}
                        </div>
                        <div className="agent-info">
                          <h3 className="agent-name">{agent.displayName}</h3>
                          <p className="agent-description">{agent.description || 'No description available'}</p>
                        </div>
                      </div>
                      <div className="agent-card-footer">
                        <div className="agent-status">
                          <span className={`status-indicator ${agent.isEnabled ? 'active' : 'inactive'}`}></span>
                          <span className="status-text">{agent.isEnabled ? 'Active' : 'Inactive'}</span>
                        </div>
                        <div className="agent-actions">
                          <button
                            className="edit-button"
                            onClick={() => handleEditAgent(agent)}
                            disabled={updatingAgents.has(agent.agentName)}
                            title="Edit configuration"
                          >
                            ✏️
                          </button>
                          <label className="toggle-switch">
                            <input
                              type="checkbox"
                              checked={agent.isEnabled}
                              onChange={() => handleToggleAgent(agent.agentName, agent.isEnabled)}
                              disabled={updatingAgents.has(agent.agentName)}
                            />
                            <span className="toggle-slider"></span>
                          </label>
                        </div>
                      </div>
                      {agent.dependencies && (
                        <div className="agent-dependencies">
                          <small>Dependencies: {agent.dependencies}</small>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            );
          })
        ) : (
          <div className="no-results">
            <p>No agents found matching your criteria.</p>
          </div>
        )}
      </div>

      {editingAgent && (
        <div className="modal-overlay" onClick={handleCloseEditModal}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h2>Edit Agent Configuration</h2>
              <button className="modal-close" onClick={handleCloseEditModal}>×</button>
            </div>
            <div className="modal-body">
              <div className="form-group">
                <label htmlFor="displayName">Display Name</label>
                <input
                  type="text"
                  id="displayName"
                  value={editForm.displayName || ''}
                  onChange={(e) => setEditForm(prev => ({ ...prev, displayName: e.target.value }))}
                  className="form-input"
                />
              </div>

              <div className="form-group">
                <label htmlFor="description">Description</label>
                <textarea
                  id="description"
                  value={editForm.description || ''}
                  onChange={(e) => setEditForm(prev => ({ ...prev, description: e.target.value }))}
                  className="form-textarea"
                  rows={2}
                />
              </div>

              <div className="form-group">
                <label htmlFor="iconName">Icon</label>
                <input
                  type="text"
                  id="iconName"
                  value={editForm.iconName || ''}
                  onChange={(e) => setEditForm(prev => ({ ...prev, iconName: e.target.value }))}
                  className="form-input"
                  placeholder="e.g., 🏗️, 🔒, 💰"
                />
              </div>

              <div className="form-group">
                <label htmlFor="displayOrder">Display Order</label>
                <input
                  type="number"
                  id="displayOrder"
                  value={editForm.displayOrder ?? 0}
                  onChange={(e) => setEditForm(prev => ({ ...prev, displayOrder: parseInt(e.target.value) }))}
                  className="form-input"
                />
              </div>

              <div className="form-group">
                <label htmlFor="dependencies">Dependencies (comma-separated)</label>
                <input
                  type="text"
                  id="dependencies"
                  value={editForm.dependencies || ''}
                  onChange={(e) => setEditForm(prev => ({ ...prev, dependencies: e.target.value }))}
                  className="form-input"
                  placeholder="e.g., InfrastructureAgent, ComplianceAgent"
                />
              </div>

              <div className="form-group">
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    checked={editForm.isEnabled ?? false}
                    onChange={(e) => setEditForm(prev => ({ ...prev, isEnabled: e.target.checked }))}
                  />
                  <span>Enabled</span>
                </label>
              </div>

              <div className="form-group">
                <div className="config-section-header">
                  <h3>Configuration Settings</h3>
                  <button
                    type="button"
                    className={`view-toggle ${showRawJson ? 'active' : ''}`}
                    onClick={() => setShowRawJson(!showRawJson)}
                  >
                    {showRawJson ? '📝 Form View' : '{ } JSON View'}
                  </button>
                </div>
                
                {showRawJson ? (
                  <div className="raw-json-editor">
                    <div className="config-json-header">
                      <button
                        type="button"
                        className="format-json-button"
                        onClick={formatJson}
                        title="Format JSON"
                      >
                        Format JSON
                      </button>
                    </div>
                    <textarea
                      id="configurationJson"
                      value={JSON.stringify(configFields, null, 2)}
                      onChange={(e) => handleConfigJsonChange(e.target.value)}
                      className="form-textarea code-editor"
                      rows={12}
                      placeholder='{"Enabled": true, "Temperature": 0.4, ...}'
                      spellCheck={false}
                    />
                  </div>
                ) : (
                  <div className="config-form-fields">
                    {Object.keys(configFields).length > 0 ? (
                      Object.entries(configFields).map(([key, value]) =>
                        renderConfigField(key, value)
                      )
                    ) : (
                      <p className="no-config-message">No configuration fields. Switch to JSON view to add fields.</p>
                    )}
                  </div>
                )}
              </div>
            </div>
            <div className="modal-footer">
              <button className="button-secondary" onClick={handleCloseEditModal}>
                Cancel
              </button>
              <button
                className="button-primary"
                onClick={handleSaveAgent}
                disabled={updatingAgents.has(editingAgent.agentName)}
              >
                {updatingAgents.has(editingAgent.agentName) ? 'Saving...' : 'Save Changes'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default Agents;
