import React, { useState, useEffect } from 'react';
import './Settings.css';
import {
  FeatureFlags,
  loadFeatureFlags,
  saveFeatureFlags,
  resetFeatureFlags,
  enableAllFeatures,
  disableAllFeatures,
  FEATURE_FLAG_LABELS,
  FEATURE_FLAG_DESCRIPTIONS,
} from '../utils/featureFlags';

interface AzureSubscription {
  id: string;
  name: string;
  tenantId: string;
  isDefault?: boolean;
}

interface DefaultPreferences {
  defaultRegion: string;
  defaultResourceGroup: string;
  defaultSku: string;
}

interface SettingsState {
  subscriptions: AzureSubscription[];
  preferences: DefaultPreferences;
  apiEndpoint: string;
  theme: 'light' | 'dark' | 'auto';
  autoSave: boolean;
}

interface SettingsProps {
  isOpen: boolean;
  onClose: () => void;
}

const Settings: React.FC<SettingsProps> = ({ isOpen, onClose }) => {
  const [activeTab, setActiveTab] = useState<'subscriptions' | 'preferences' | 'api' | 'display' | 'features'>('subscriptions');
  const [settings, setSettings] = useState<SettingsState>({
    subscriptions: [],
    preferences: {
      defaultRegion: 'eastus',
      defaultResourceGroup: '',
      defaultSku: 'Standard_D2s_v3'
    },
    apiEndpoint: 'http://localhost:5002',
    theme: 'auto',
    autoSave: true
  });
  const [featureFlags, setFeatureFlags] = useState<FeatureFlags>(loadFeatureFlags());

  const [newSubscription, setNewSubscription] = useState({
    id: '',
    name: '',
    tenantId: ''
  });

  // Load settings from localStorage
  useEffect(() => {
    const savedSettings = localStorage.getItem('platformSettings');
    if (savedSettings) {
      try {
        setSettings(JSON.parse(savedSettings));
      } catch (err) {
        console.error('Failed to load settings:', err);
      }
    }
  }, []);

  // Apply theme when settings change
  useEffect(() => {
    const applyTheme = () => {
      const root = document.documentElement;
      
      if (settings.theme === 'light') {
        root.setAttribute('data-theme', 'light');
      } else if (settings.theme === 'dark') {
        root.setAttribute('data-theme', 'dark');
      } else if (settings.theme === 'auto') {
        // Use system preference
        const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
        root.setAttribute('data-theme', prefersDark ? 'dark' : 'light');
      }
    };

    applyTheme();
  }, [settings.theme]);

  // Save settings to localStorage
  const saveSettings = (newSettings: SettingsState) => {
    setSettings(newSettings);
    localStorage.setItem('platformSettings', JSON.stringify(newSettings));
    console.log('💾 Settings saved to localStorage:', {
      key: 'platformSettings',
      subscriptions: newSettings.subscriptions.length,
      data: newSettings
    });
  };

  const handleAddSubscription = () => {
    if (!newSubscription.id || !newSubscription.name || !newSubscription.tenantId) {
      alert('Please fill in all subscription fields');
      return;
    }

    console.log('➕ Adding new subscription:', newSubscription);

    const updatedSettings = {
      ...settings,
      subscriptions: [
        ...settings.subscriptions,
        {
          ...newSubscription,
          isDefault: settings.subscriptions.length === 0
        }
      ]
    };
    
    saveSettings(updatedSettings);
    setNewSubscription({ id: '', name: '', tenantId: '' });
    console.log('✅ Subscription added successfully. Total subscriptions:', updatedSettings.subscriptions.length);
  };

  const handleRemoveSubscription = (index: number) => {
    const updatedSettings = {
      ...settings,
      subscriptions: settings.subscriptions.filter((_, i) => i !== index)
    };
    saveSettings(updatedSettings);
  };

  const handleSetDefaultSubscription = (index: number) => {
    const updatedSettings = {
      ...settings,
      subscriptions: settings.subscriptions.map((sub, i) => ({
        ...sub,
        isDefault: i === index
      }))
    };
    saveSettings(updatedSettings);
  };

  const handlePreferenceChange = (key: keyof DefaultPreferences, value: string) => {
    const updatedSettings = {
      ...settings,
      preferences: {
        ...settings.preferences,
        [key]: value
      }
    };
    saveSettings(updatedSettings);
  };

  const handleApiEndpointChange = (value: string) => {
    const updatedSettings = {
      ...settings,
      apiEndpoint: value
    };
    saveSettings(updatedSettings);
  };

  const handleThemeChange = (theme: 'light' | 'dark' | 'auto') => {
    console.log('Theme change requested:', theme);
    const updatedSettings = {
      ...settings,
      theme
    };
    saveSettings(updatedSettings);
    console.log('Theme saved:', updatedSettings.theme);
  };

  const handleAutoSaveToggle = () => {
    console.log('Auto-save toggle clicked. Current:', settings.autoSave);
    const updatedSettings = {
      ...settings,
      autoSave: !settings.autoSave
    };
    saveSettings(updatedSettings);
    console.log('Auto-save updated to:', updatedSettings.autoSave);
  };

  const handleFeatureFlagToggle = (flagKey: keyof FeatureFlags) => {
    const updatedFlags = {
      ...featureFlags,
      [flagKey]: !featureFlags[flagKey],
    };
    setFeatureFlags(updatedFlags);
    saveFeatureFlags(updatedFlags);
    // Dispatch custom event to notify App.tsx
    window.dispatchEvent(new Event('featureFlagsUpdated'));
    console.log(`Feature flag '${flagKey}' updated to:`, updatedFlags[flagKey]);
  };

  const handleEnableAllFeatures = () => {
    const updatedFlags = enableAllFeatures();
    setFeatureFlags(updatedFlags);
    window.dispatchEvent(new Event('featureFlagsUpdated'));
    console.log('All features enabled');
  };

  const handleDisableAllFeatures = () => {
    const updatedFlags = disableAllFeatures();
    setFeatureFlags(updatedFlags);
    window.dispatchEvent(new Event('featureFlagsUpdated'));
    console.log('All features disabled');
  };

  const handleResetFeatures = () => {
    const updatedFlags = resetFeatureFlags();
    setFeatureFlags(updatedFlags);
    window.dispatchEvent(new Event('featureFlagsUpdated'));
    console.log('Feature flags reset to defaults');
  };

  const handleExportSettings = () => {
    const dataStr = JSON.stringify(settings, null, 2);
    const dataBlob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(dataBlob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'platform-settings.json';
    link.click();
  };

  const handleImportSettings = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (file) {
      const reader = new FileReader();
      reader.onload = (e) => {
        try {
          const imported = JSON.parse(e.target?.result as string);
          saveSettings(imported);
          alert('Settings imported successfully!');
        } catch (err) {
          alert('Failed to import settings. Invalid file format.');
        }
      };
      reader.readAsText(file);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="settings-overlay" onClick={onClose}>
      <div className="settings-panel" onClick={(e) => e.stopPropagation()}>
        <div className="settings-header">
          <h2>⚙️ Settings</h2>
          <button className="close-btn" onClick={onClose}>✕</button>
        </div>

        <div className="settings-tabs">
          <button 
            className={`tab ${activeTab === 'subscriptions' ? 'active' : ''}`}
            onClick={() => setActiveTab('subscriptions')}
          >
            🔐 Subscriptions
          </button>
          <button 
            className={`tab ${activeTab === 'preferences' ? 'active' : ''}`}
            onClick={() => setActiveTab('preferences')}
          >
            🎯 Defaults
          </button>
          <button 
            className={`tab ${activeTab === 'api' ? 'active' : ''}`}
            onClick={() => setActiveTab('api')}
          >
            🔌 API
          </button>
          <button 
            className={`tab ${activeTab === 'display' ? 'active' : ''}`}
            onClick={() => setActiveTab('display')}
          >
            🎨 Display
          </button>
          <button 
            className={`tab ${activeTab === 'features' ? 'active' : ''}`}
            onClick={() => setActiveTab('features')}
          >
            🎛️ Features
          </button>
        </div>

        <div className="settings-content">
          {/* Subscriptions Tab */}
          {activeTab === 'subscriptions' && (
            <div className="settings-section">
              <h3>Azure Subscriptions</h3>
              <p className="section-description">
                Save your frequently used Azure subscriptions for quick access
              </p>

              <div className="subscription-list">
                {settings.subscriptions.map((sub, index) => (
                  <div key={index} className="subscription-item">
                    <div className="subscription-info">
                      <div className="subscription-name">
                        {sub.name}
                        {sub.isDefault && <span className="default-badge">DEFAULT</span>}
                      </div>
                      <div className="subscription-details">
                        <span>ID: {sub.id}</span>
                        <span>Tenant: {sub.tenantId}</span>
                      </div>
                    </div>
                    <div className="subscription-actions">
                      {!sub.isDefault && (
                        <button 
                          className="set-default-btn"
                          onClick={() => handleSetDefaultSubscription(index)}
                        >
                          Set Default
                        </button>
                      )}
                      <button 
                        className="remove-btn"
                        onClick={() => handleRemoveSubscription(index)}
                      >
                        Remove
                      </button>
                    </div>
                  </div>
                ))}

                {settings.subscriptions.length === 0 && (
                  <div className="empty-state">
                    No saved subscriptions. Add one below.
                  </div>
                )}
              </div>

              <div className="add-subscription-form">
                <h4>Add New Subscription</h4>
                <div className="form-row">
                  <input
                    type="text"
                    placeholder="Subscription Name"
                    value={newSubscription.name}
                    onChange={(e) => setNewSubscription({...newSubscription, name: e.target.value})}
                  />
                </div>
                <div className="form-row">
                  <input
                    type="text"
                    placeholder="Subscription ID"
                    value={newSubscription.id}
                    onChange={(e) => setNewSubscription({...newSubscription, id: e.target.value})}
                  />
                </div>
                <div className="form-row">
                  <input
                    type="text"
                    placeholder="Tenant ID"
                    value={newSubscription.tenantId}
                    onChange={(e) => setNewSubscription({...newSubscription, tenantId: e.target.value})}
                  />
                </div>
                <button className="add-btn" onClick={handleAddSubscription}>
                  + Add Subscription
                </button>
              </div>
            </div>
          )}

          {/* Preferences Tab */}
          {activeTab === 'preferences' && (
            <div className="settings-section">
              <h3>Default Preferences</h3>
              <p className="section-description">
                Set default values to speed up environment creation
              </p>

              <div className="preference-item">
                <label>Default Azure Region</label>
                <select 
                  value={settings.preferences.defaultRegion}
                  onChange={(e) => handlePreferenceChange('defaultRegion', e.target.value)}
                >
                  <optgroup label="US Commercial">
                    <option value="eastus">East US</option>
                    <option value="eastus2">East US 2</option>
                    <option value="westus">West US</option>
                    <option value="westus2">West US 2</option>
                    <option value="westus3">West US 3</option>
                    <option value="centralus">Central US</option>
                    <option value="northcentralus">North Central US</option>
                    <option value="southcentralus">South Central US</option>
                  </optgroup>
                  <optgroup label="US Government">
                    <option value="usgovvirginia">US Gov Virginia</option>
                    <option value="usgovtexas">US Gov Texas</option>
                    <option value="usgovarizona">US Gov Arizona</option>
                    <option value="usdodeast">US DoD East</option>
                    <option value="usdodcentral">US DoD Central</option>
                  </optgroup>
                  <optgroup label="Europe">
                    <option value="northeurope">North Europe</option>
                    <option value="westeurope">West Europe</option>
                    <option value="uksouth">UK South</option>
                    <option value="ukwest">UK West</option>
                  </optgroup>
                  <optgroup label="Asia Pacific">
                    <option value="southeastasia">Southeast Asia</option>
                    <option value="eastasia">East Asia</option>
                    <option value="japaneast">Japan East</option>
                    <option value="japanwest">Japan West</option>
                    <option value="australiaeast">Australia East</option>
                  </optgroup>
                </select>
              </div>

              <div className="preference-item">
                <label>Default Resource Group Prefix</label>
                <input
                  type="text"
                  placeholder="e.g., rg-mycompany"
                  value={settings.preferences.defaultResourceGroup}
                  onChange={(e) => handlePreferenceChange('defaultResourceGroup', e.target.value)}
                />
                <small className="help-text">
                  Will be used as a prefix for new resource groups
                </small>
              </div>

              <div className="preference-item">
                <label>Default VM SKU</label>
                <select 
                  value={settings.preferences.defaultSku}
                  onChange={(e) => handlePreferenceChange('defaultSku', e.target.value)}
                >
                  <option value="Standard_D2s_v3">Standard_D2s_v3 (2 vCPU, 8 GB RAM)</option>
                  <option value="Standard_D4s_v3">Standard_D4s_v3 (4 vCPU, 16 GB RAM)</option>
                  <option value="Standard_D8s_v3">Standard_D8s_v3 (8 vCPU, 32 GB RAM)</option>
                  <option value="Standard_E2s_v3">Standard_E2s_v3 (2 vCPU, 16 GB RAM)</option>
                  <option value="Standard_E4s_v3">Standard_E4s_v3 (4 vCPU, 32 GB RAM)</option>
                </select>
              </div>
            </div>
          )}

          {/* API Tab */}
          {activeTab === 'api' && (
            <div className="settings-section">
              <h3>API Configuration</h3>
              <p className="section-description">
                Configure API endpoint and connection settings
              </p>

              <div className="preference-item">
                <label>Admin API Endpoint</label>
                <input
                  type="text"
                  placeholder="http://localhost:5002"
                  value={settings.apiEndpoint}
                  onChange={(e) => handleApiEndpointChange(e.target.value)}
                />
                <small className="help-text">
                  Base URL for the Admin API service
                </small>
              </div>

              <div className="api-status">
                <h4>Connection Status</h4>
                <div className="status-indicator">
                  <span className="status-dot connected"></span>
                  <span>Connected to {settings.apiEndpoint}</span>
                </div>
              </div>
            </div>
          )}

          {/* Display Tab */}
          {activeTab === 'display' && (
            <div className="settings-section">
              <h3>Display Settings</h3>
              <p className="section-description">
                Customize your workspace appearance and behavior
              </p>

              <div className="preference-item">
                <label>Theme</label>
                <div className="theme-options">
                  <button 
                    className={`theme-btn ${settings.theme === 'light' ? 'active' : ''}`}
                    onClick={() => handleThemeChange('light')}
                  >
                    ☀️ Light
                  </button>
                  <button 
                    className={`theme-btn ${settings.theme === 'dark' ? 'active' : ''}`}
                    onClick={() => handleThemeChange('dark')}
                  >
                    🌙 Dark
                  </button>
                  <button 
                    className={`theme-btn ${settings.theme === 'auto' ? 'active' : ''}`}
                    onClick={() => handleThemeChange('auto')}
                  >
                    🔄 Auto
                  </button>
                </div>
              </div>

              <div className="preference-item">
                <label className="checkbox-label">
                  <input
                    type="checkbox"
                    checked={settings.autoSave}
                    onChange={handleAutoSaveToggle}
                  />
                  <span>Auto-save form data</span>
                </label>
                <small className="help-text">
                  Automatically save form data as you type
                </small>
              </div>
            </div>
          )}

          {/* Features Tab */}
          {activeTab === 'features' && (
            <div className="settings-section">
              <h3>Feature Flags</h3>
              <p className="section-description">
                Enable or disable features to customize your workspace. Changes take effect immediately.
              </p>

              <div className="feature-flags-actions">
                <button className="feature-action-btn" onClick={handleEnableAllFeatures}>
                  ✅ Enable All
                </button>
                <button className="feature-action-btn" onClick={handleDisableAllFeatures}>
                  ❌ Disable All
                </button>
                <button className="feature-action-btn" onClick={handleResetFeatures}>
                  🔄 Reset to Defaults
                </button>
              </div>

              <div className="feature-flags-list">
                {(Object.keys(featureFlags) as Array<keyof FeatureFlags>).map((flagKey) => (
                  <div key={flagKey} className="feature-flag-item">
                    <div className="feature-flag-info">
                      <div className="feature-flag-name">
                        {FEATURE_FLAG_LABELS[flagKey]}
                      </div>
                      <div className="feature-flag-description">
                        {FEATURE_FLAG_DESCRIPTIONS[flagKey]}
                      </div>
                    </div>
                    <label className="toggle-switch">
                      <input
                        type="checkbox"
                        checked={featureFlags[flagKey]}
                        onChange={() => handleFeatureFlagToggle(flagKey)}
                      />
                      <span className="toggle-slider"></span>
                    </label>
                  </div>
                ))}
              </div>

              <div className="feature-flags-info">
                <p className="info-text">
                  ℹ️ <strong>Note:</strong> Disabled features will be hidden from the navigation menu and their routes will be inaccessible.
                </p>
              </div>
            </div>
          )}
        </div>

        <div className="settings-footer">
          <div className="footer-actions">
            <button className="export-btn" onClick={handleExportSettings}>
              📤 Export Settings
            </button>
            <label className="import-btn">
              📥 Import Settings
              <input 
                type="file" 
                accept=".json" 
                onChange={handleImportSettings}
                style={{ display: 'none' }}
              />
            </label>
          </div>
          <button className="save-close-btn" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </div>
  );
};

export default Settings;
