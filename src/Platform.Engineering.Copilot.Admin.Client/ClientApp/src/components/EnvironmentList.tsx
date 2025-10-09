import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import adminApi, { EnvironmentResponse } from '../services/adminApi';
import './EnvironmentList.css';

const EnvironmentList: React.FC = () => {
  const [environments, setEnvironments] = useState<EnvironmentResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filterResourceGroup, setFilterResourceGroup] = useState('');

  useEffect(() => {
    loadEnvironments();
  }, [filterResourceGroup]);

  const loadEnvironments = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await adminApi.listEnvironments(filterResourceGroup || undefined);
      setEnvironments(response.environments);
    } catch (err: any) {
      console.error('Failed to load environments:', err);
      setError(err.message || 'Failed to load environments');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (environmentName: string, resourceGroup: string) => {
    if (!window.confirm(`Are you sure you want to delete environment "${environmentName}"?`)) {
      return;
    }

    try {
      await adminApi.deleteEnvironment(environmentName, resourceGroup);
      alert('Environment deleted successfully');
      loadEnvironments();
    } catch (err: any) {
      alert(`Failed to delete environment: ${err.message}`);
    }
  };

  const getStatusBadgeClass = (status: string) => {
    const lowerStatus = status?.toLowerCase() || '';
    if (lowerStatus.includes('running') || lowerStatus.includes('healthy') || lowerStatus.includes('success')) {
      return 'status-badge status-success';
    }
    if (lowerStatus.includes('failed') || lowerStatus.includes('error')) {
      return 'status-badge status-error';
    }
    if (lowerStatus.includes('pending') || lowerStatus.includes('creating')) {
      return 'status-badge status-pending';
    }
    return 'status-badge status-unknown';
  };

  const formatDate = (dateString: string) => {
    try {
      return new Date(dateString).toLocaleString();
    } catch {
      return dateString;
    }
  };

  if (loading) {
    return (
      <div className="environment-list">
        <div className="loading">Loading environments...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="environment-list">
        <div className="error-banner">
          <h3>⚠️ Error Loading Environments</h3>
          <p>{error}</p>
          <button onClick={loadEnvironments} className="btn-retry">Retry</button>
        </div>
      </div>
    );
  }

  return (
    <div className="environment-list">
      <div className="page-header">
        <div>
          <h2>🌐 Environments</h2>
          <p className="page-subtitle">Manage deployed environments and their lifecycle</p>
        </div>
        <Link to="/environments/create" className="btn btn-primary">
          + Create Environment
        </Link>
      </div>

      <div className="filters">
        <input
          type="text"
          placeholder="Filter by Resource Group..."
          value={filterResourceGroup}
          onChange={(e) => setFilterResourceGroup(e.target.value)}
          className="filter-input"
        />
        <button onClick={loadEnvironments} className="btn btn-secondary">
          🔄 Refresh
        </button>
      </div>

      {environments.length === 0 ? (
        <div className="empty-state">
          <div className="empty-state-icon">🌐</div>
          <h3>No Environments Found</h3>
          <p>Get started by creating your first environment from a template</p>
          <Link to="/environments/create" className="btn btn-primary">
            Create Your First Environment
          </Link>
        </div>
      ) : (
        <div className="environments-grid">
          {environments.map((env) => (
            <div key={env.id} className="environment-card">
              <div className="environment-card-header">
                <h3>{env.name}</h3>
                <span className={getStatusBadgeClass(env.status)}>
                  {env.status || 'Unknown'}
                </span>
              </div>

              <div className="environment-card-body">
                <div className="environment-info">
                  <div className="info-row">
                    <span className="info-label">Resource Group:</span>
                    <span className="info-value">{env.resourceGroup}</span>
                  </div>
                  {env.templateId && (
                    <div className="info-row">
                      <span className="info-label">Template ID:</span>
                      <span className="info-value code">{env.templateId}</span>
                    </div>
                  )}
                  <div className="info-row">
                    <span className="info-label">Created:</span>
                    <span className="info-value">{formatDate(env.createdAt)}</span>
                  </div>
                  {env.tags && Object.keys(env.tags).length > 0 && (
                    <div className="info-row">
                      <span className="info-label">Tags:</span>
                      <div className="tags">
                        {Object.entries(env.tags).map(([key, value]) => (
                          <span key={key} className="tag">
                            {key}: {value}
                          </span>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              </div>

              <div className="environment-card-footer">
                <Link
                  to={`/environments/${env.name}?resourceGroup=${env.resourceGroup}`}
                  className="btn btn-small btn-secondary"
                >
                  📊 Details
                </Link>
                <button
                  onClick={() => handleDelete(env.name, env.resourceGroup)}
                  className="btn btn-small btn-danger"
                >
                  🗑️ Delete
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      <div className="list-footer">
        <p>Total Environments: {environments.length}</p>
      </div>
    </div>
  );
};

export default EnvironmentList;
