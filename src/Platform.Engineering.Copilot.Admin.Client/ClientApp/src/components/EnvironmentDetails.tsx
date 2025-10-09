import React, { useState, useEffect } from 'react';
import { useParams, useNavigate, useSearchParams } from 'react-router-dom';
import adminApi, { 
  EnvironmentDetailResponse, 
  EnvironmentStatus, 
  EnvironmentMetrics 
} from '../services/adminApi';
import './EnvironmentDetails.css';

const EnvironmentDetails: React.FC = () => {
  const { name } = useParams<{ name: string }>();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const resourceGroup = searchParams.get('resourceGroup') || '';

  const [environment, setEnvironment] = useState<EnvironmentDetailResponse | null>(null);
  const [status, setStatus] = useState<EnvironmentStatus | null>(null);
  const [metrics, setMetrics] = useState<EnvironmentMetrics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<'overview' | 'history' | 'metrics'>('overview');

  useEffect(() => {
    if (name && resourceGroup) {
      loadEnvironmentDetails();
    }
  }, [name, resourceGroup]);

  const loadEnvironmentDetails = async () => {
    if (!name || !resourceGroup) return;

    try {
      setLoading(true);
      setError(null);

      const [envData, statusData] = await Promise.all([
        adminApi.getEnvironment(name, resourceGroup),
        adminApi.getEnvironmentStatus(name, resourceGroup).catch(() => null)
      ]);

      setEnvironment(envData);
      setStatus(statusData);

      // Load metrics if available
      try {
        const metricsData = await adminApi.getEnvironmentMetrics(name, resourceGroup, 24);
        setMetrics(metricsData);
      } catch (err) {
        console.log('Metrics not available:', err);
      }
    } catch (err: any) {
      console.error('Failed to load environment:', err);
      setError(err.response?.data?.error || err.message || 'Failed to load environment');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async () => {
    if (!name || !resourceGroup) return;

    if (!window.confirm(`Are you sure you want to delete environment "${name}"? This action cannot be undone.`)) {
      return;
    }

    try {
      await adminApi.deleteEnvironment(name, resourceGroup);
      alert('Environment deleted successfully');
      navigate('/environments');
    } catch (err: any) {
      alert(`Failed to delete environment: ${err.response?.data?.error || err.message}`);
    }
  };

  const handleScale = async () => {
    if (!name || !resourceGroup) return;

    const replicas = prompt('Enter target number of replicas:');
    if (!replicas) return;

    try {
      await adminApi.scaleEnvironment(name, resourceGroup, {
        targetReplicas: parseInt(replicas, 10)
      });
      alert('Scaling operation initiated');
      loadEnvironmentDetails();
    } catch (err: any) {
      alert(`Failed to scale: ${err.response?.data?.error || err.message}`);
    }
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
      <div className="environment-details">
        <div className="loading">Loading environment details...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="environment-details">
        <div className="error-banner">
          <h3>⚠️ Error</h3>
          <p>{error}</p>
          <button onClick={() => navigate('/environments')} className="btn btn-secondary">
            ← Back to Environments
          </button>
        </div>
      </div>
    );
  }

  if (!environment) {
    return (
      <div className="environment-details">
        <div className="error-banner">
          <h3>Environment Not Found</h3>
          <button onClick={() => navigate('/environments')} className="btn btn-secondary">
            ← Back to Environments
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="environment-details">
      <div className="details-header">
        <div>
          <h2>🌐 {environment.name}</h2>
          <p className="subtitle">{environment.resourceGroup}</p>
        </div>
        <div className="header-actions">
          <button onClick={() => navigate('/environments')} className="btn btn-secondary">
            ← Back
          </button>
          <button onClick={loadEnvironmentDetails} className="btn btn-secondary">
            🔄 Refresh
          </button>
          <button onClick={handleScale} className="btn btn-primary">
            📊 Scale
          </button>
          <button onClick={handleDelete} className="btn btn-danger">
            🗑️ Delete
          </button>
        </div>
      </div>

      <div className="tabs">
        <button
          className={`tab ${activeTab === 'overview' ? 'active' : ''}`}
          onClick={() => setActiveTab('overview')}
        >
          Overview
        </button>
        <button
          className={`tab ${activeTab === 'history' ? 'active' : ''}`}
          onClick={() => setActiveTab('history')}
        >
          History
        </button>
        <button
          className={`tab ${activeTab === 'metrics' ? 'active' : ''}`}
          onClick={() => setActiveTab('metrics')}
        >
          Metrics
        </button>
      </div>

      <div className="tab-content">
        {activeTab === 'overview' && (
          <div className="overview-tab">
            <div className="info-cards">
              <div className="info-card">
                <h4>Status</h4>
                <p className="status-text">{environment.status || 'Unknown'}</p>
              </div>
              
              {status && (
                <>
                  <div className="info-card">
                    <h4>Health</h4>
                    <p className="status-text">{status.health || 'Unknown'}</p>
                  </div>
                  <div className="info-card">
                    <h4>Resources</h4>
                    <p className="number">{status.resourceCount || 0}</p>
                  </div>
                </>
              )}

              <div className="info-card">
                <h4>Created</h4>
                <p className="date-text">{formatDate(environment.createdAt)}</p>
              </div>
            </div>

            <div className="details-section">
              <h3>Configuration</h3>
              <div className="details-grid">
                <div className="detail-item">
                  <span className="detail-label">Environment ID:</span>
                  <span className="detail-value code">{environment.id}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Resource Group:</span>
                  <span className="detail-value">{environment.resourceGroup}</span>
                </div>
                {environment.templateId && (
                  <div className="detail-item">
                    <span className="detail-label">Template ID:</span>
                    <span className="detail-value code">{environment.templateId}</span>
                  </div>
                )}
              </div>
            </div>

            {environment.tags && Object.keys(environment.tags).length > 0 && (
              <div className="details-section">
                <h3>Tags</h3>
                <div className="tags">
                  {Object.entries(environment.tags).map(([key, value]) => (
                    <div key={key} className="tag">
                      <strong>{key}:</strong> {value}
                    </div>
                  ))}
                </div>
              </div>
            )}

            {status?.resources && status.resources.length > 0 && (
              <div className="details-section">
                <h3>Resources</h3>
                <div className="resources-list">
                  {status.resources.map((resource: any, index: number) => (
                    <div key={index} className="resource-item">
                      <div className="resource-name">{resource.name || resource.id}</div>
                      <div className="resource-type">{resource.type}</div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}

        {activeTab === 'history' && (
          <div className="history-tab">
            {environment.history && environment.history.length > 0 ? (
              <div className="history-list">
                {environment.history.map((item, index) => (
                  <div key={index} className="history-item">
                    <div className="history-header">
                      <span className="history-action">{item.action}</span>
                      <span className={`history-status status-${item.status.toLowerCase()}`}>
                        {item.status}
                      </span>
                    </div>
                    <div className="history-details">
                      <div className="history-time">
                        Started: {formatDate(item.startedAt)}
                        {item.completedAt && ` • Completed: ${formatDate(item.completedAt)}`}
                        {item.duration && ` • Duration: ${item.duration}`}
                      </div>
                      {item.triggeredBy && (
                        <div className="history-user">Triggered by: {item.triggeredBy}</div>
                      )}
                      {item.errorMessage && (
                        <div className="history-error">{item.errorMessage}</div>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="empty-state">
                <p>No deployment history available</p>
              </div>
            )}
          </div>
        )}

        {activeTab === 'metrics' && (
          <div className="metrics-tab">
            {metrics ? (
              <div className="metrics-content">
                <h3>Performance Metrics (Last 24 Hours)</h3>
                {metrics.metrics && metrics.metrics.length > 0 ? (
                  metrics.metrics.map((metric, index) => (
                    <div key={index} className="metric-section">
                      <h4>{metric.name}</h4>
                      <div className="metric-chart">
                        {metric.values.map((value, idx) => (
                          <div key={idx} className="metric-point">
                            <span className="metric-time">{new Date(value.timestamp).toLocaleTimeString()}</span>
                            <span className="metric-value">{value.value.toFixed(2)}</span>
                          </div>
                        ))}
                      </div>
                    </div>
                  ))
                ) : (
                  <p>No metric data available</p>
                )}
              </div>
            ) : (
              <div className="empty-state">
                <p>Metrics not available for this environment</p>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

export default EnvironmentDetails;
