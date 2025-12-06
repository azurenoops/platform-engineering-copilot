import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import adminApi, { TemplateStats, EnvironmentListResponse, DeploymentStatusResponse, AgentConfigurationListResponse } from '../services/adminApi';
import PendingApprovalsPanel from './PendingApprovalsPanel';
import ChatPanel from './ChatPanel';
import DeploymentProgress from './DeploymentProgress';
import InsightsSummary from './InsightsSummary';
import { loadFeatureFlags, FeatureFlags } from '../utils/featureFlags';
import './Dashboard.css';

const Dashboard: React.FC = () => {
  const navigate = useNavigate();
  const [stats, setStats] = useState<TemplateStats | null>(null);
  const [environments, setEnvironments] = useState<EnvironmentListResponse | null>(null);
  const [activeDeployments, setActiveDeployments] = useState<DeploymentStatusResponse[]>([]);
  const [agentData, setAgentData] = useState<AgentConfigurationListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isChatOpen, setIsChatOpen] = useState(false);
  const [featureFlags, setFeatureFlags] = useState<FeatureFlags>(loadFeatureFlags());

  useEffect(() => {
    loadDashboardData();
    // Poll for active deployments every 30 seconds
    const interval = setInterval(loadActiveDeployments, 30000);
    
    // Listen for feature flag changes
    const handleFeatureFlagsUpdate = () => {
      setFeatureFlags(loadFeatureFlags());
    };
    
    window.addEventListener('featureFlagsUpdated', handleFeatureFlagsUpdate);
    window.addEventListener('storage', handleFeatureFlagsUpdate);
    
    return () => {
      clearInterval(interval);
      window.removeEventListener('featureFlagsUpdated', handleFeatureFlagsUpdate);
      window.removeEventListener('storage', handleFeatureFlagsUpdate);
    };
  }, []);

  const loadDashboardData = async () => {
    try {
      setLoading(true);
      const [statsData, envData, agents] = await Promise.all([
        adminApi.getStats(),
        adminApi.listEnvironments(),
        adminApi.getAgents()
      ]);
      setStats(statsData);
      setEnvironments(envData);
      setAgentData(agents);
      setError(null);
      
      // Also load active deployments
      await loadActiveDeployments();
    } catch (err: any) {
      setError(err.message || 'Failed to load dashboard data');
    } finally {
      setLoading(false);
    }
  };

  const loadActiveDeployments = async () => {
    try {
      const deployments = await adminApi.getActiveDeployments();
      setActiveDeployments(deployments);
    } catch (err) {
      console.error('Failed to load active deployments:', err);
      // Don't show error to user - this is a non-critical feature
    }
  };

  const handleDeploymentComplete = (deploymentId: string) => {
    // Remove the completed deployment from the list
    setActiveDeployments(prev => prev.filter(d => d.deploymentId !== deploymentId));
    // Refresh environments list
    loadDashboardData();
  };

  if (loading) {
    return <div className="dashboard-loading">Loading dashboard...</div>;
  }

  if (error) {
    return <div className="dashboard-error">Error: {error}</div>;
  }

  return (
    <div className="dashboard">
      <h2>📊 Platform Engineering Admin Dashboard</h2>
      
      <div className="stats-grid">
        <div className="stat-card">
          <h3>Total Service Templates</h3>
          <div className="stat-value">{stats?.totalTemplates || 0}</div>
          <div className="stat-details">
            <span className="stat-active">✅ {stats?.activeTemplates || 0} Active</span>
            <span className="stat-inactive">❌ {stats?.inactiveTemplates || 0} Inactive</span>
          </div>
        </div>

        {featureFlags.environments && (
          <div className="stat-card">
            <h3>Environments</h3>
            <div className="stat-value">{environments?.totalCount || 0}</div>
            <div className="stat-details">
              <span className="stat-active">🟢 Running</span>
              <button 
                className="stat-link"
                onClick={() => navigate('/environments')}
              >
                View All →
              </button>
            </div>
          </div>
        )}

        <div className="stat-card">
          <h3>Template Types</h3>
          <div className="stat-list">
            {stats?.byType && stats.byType.map((item) => (
              <div key={item.type} className="stat-item">
                <span>{item.type}</span>
                <span className="stat-count">{item.count}</span>
              </div>
            ))}
          </div>
        </div>

        <div className="stat-card">
          <h3>Active Agents</h3>
          <div className="stat-value">{agentData?.enabledAgents || 0}/{agentData?.totalAgents || 0}</div>
          <div className="stat-list">
            {agentData?.categories && agentData.categories
              .filter(cat => cat.enabledCount > 0)
              .map((cat) => (
                <div key={cat.category} className="stat-item">
                  <span>{cat.category}</span>
                  <span className="stat-count">{cat.enabledCount}</span>
                </div>
              ))}
          </div>
          <button 
            className="stat-link"
            onClick={() => navigate('/agents')}
            style={{ marginTop: '8px' }}
          >
            Manage Agents →
          </button>
        </div>
      </div>

      {/* Insights Summary */}
      {featureFlags.platformInsights && <InsightsSummary />}

      {/* Active Deployments Section */}
      {activeDeployments && activeDeployments.length > 0 && (
        <div className="deployments-section">
          <div className="section-header">
            <h3>🚀 Active Deployments</h3>
            <span className="deployment-count">{activeDeployments.length} running</span>
          </div>
          <div className="deployments-list">
            {activeDeployments.map((deployment) => (
              <DeploymentProgress
                key={deployment.deploymentId}
                deploymentId={deployment.deploymentId}
                onComplete={(success) => handleDeploymentComplete(deployment.deploymentId)}
              />
            ))}
          </div>
        </div>
      )}

      {/* Active Environments Section */}
      {featureFlags.environments && environments && environments.totalCount > 0 && (
        <div className="environments-section">
          <div className="section-header">
            <h3>🌐 Active Environments</h3>
            <button 
              onClick={() => navigate('/environments/create')}
              className="btn-create"
            >
              ➕ Create Environment
            </button>
          </div>
          <div className="environments-grid">
            {environments.environments.slice(0, 6).map((env) => (
              <div 
                key={env.id} 
                className="environment-card"
                onClick={() => navigate(`/environments/${env.name}`)}
              >
                <div className="env-header">
                  <h4>{env.name}</h4>
                  <span className={`env-status status-${env.status.toLowerCase()}`}>
                    {env.status}
                  </span>
                </div>
                <div className="env-details">
                  <div className="env-detail">
                    <span className="label">Type:</span>
                    <span className="value">{env.environmentType || 'N/A'}</span>
                  </div>
                  <div className="env-detail">
                    <span className="label">Location:</span>
                    <span className="value">{env.location}</span>
                  </div>
                  <div className="env-detail">
                    <span className="label">Resource Group:</span>
                    <span className="value">{env.resourceGroup}</span>
                  </div>
                  {env.templateId && (
                    <div className="env-detail">
                      <span className="label">Template:</span>
                      <span className="value">✓ Service Template</span>
                    </div>
                  )}
                </div>
                <div className="env-footer">
                  <small>Created {new Date(env.createdAt).toLocaleDateString()}</small>
                </div>
              </div>
            ))}
          </div>
          {environments.totalCount > 6 && (
            <div className="view-all-link">
              <button onClick={() => navigate('/environments')}>
                View all {environments.totalCount} environments →
              </button>
            </div>
          )}
        </div>
      )}

      {/* Unified Pending Approvals Panel (Infrastructure + ServiceCreation) */}
      {featureFlags.provision && <PendingApprovalsPanel />}

      {featureFlags.serviceCatalog && (
        <div className="recent-templates">
          <h3>📅 Recently Created Templates</h3>
          <div className="recent-list">
            {stats?.recentlyCreated && stats.recentlyCreated.map((template, index) => (
              <div key={index} className="recent-item">
                <div className="recent-name">{template.name}</div>
                <div className="recent-meta">
                  Created {new Date(template.createdAt).toLocaleDateString()} by {template.createdBy}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
      <br />
      {(featureFlags.serviceCatalog || featureFlags.provision) && (
        <div className="quick-actions">
          <h3>Quick Actions</h3>
          <div className="action-buttons">
            {featureFlags.serviceCatalog && (
              <>
                <button onClick={() => window.location.href = '/templates/create'} className="btn-primary">
                  ➕ Create New Template
                </button>
                <button onClick={() => window.location.href = '/templates'} className="btn-secondary">
                  📋 View All Templates
                </button>
              </>
            )}
            {featureFlags.provision && (
              <button onClick={() => window.location.href = '/infrastructure'} className="btn-secondary">
                🏗️ Manage Infrastructure
              </button>
            )}
          </div>
        </div>
      )}

      <div className="info-panel">
        <h3>ℹ️ System Information</h3>
        <ul>
          <li><strong>Admin Console:</strong> http://localhost:5003</li>
          <li><strong>Admin API:</strong> http://localhost:5002</li>
          <li><strong>Swagger UI:</strong> <a href="http://localhost:5002" target="_blank" rel="noopener noreferrer">Open API Docs</a></li>
          <li><strong>MCP Server:</strong> http://localhost:5100</li>
          <li><strong>Chat App:</strong> http://localhost:5001</li>
        </ul>
      </div>

      {/* Floating Chat Button */}
      {!isChatOpen && (
        <button 
          className="chat-toggle-button"
          onClick={() => setIsChatOpen(true)}
          title="Open Platform Assistant"
        >
          <span className="chat-icon">💬</span>
          <span className="chat-pulse"></span>
        </button>
      )}

      {/* Chat Panel */}
      <ChatPanel 
        isOpen={isChatOpen} 
        onClose={() => setIsChatOpen(false)} 
      />
    </div>
  );
};

export default Dashboard;
