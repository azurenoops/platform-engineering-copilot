import React, { useState, useEffect, useRef } from 'react';
import './DeploymentProgress.css';
import adminApi, { DeploymentStatusResponse, DeploymentStep } from '../services/adminApi';

interface DeploymentProgressProps {
  deploymentId: string;
  onComplete?: (success: boolean) => void;
  onClose?: () => void;
}

const DeploymentProgress: React.FC<DeploymentProgressProps> = ({ 
  deploymentId, 
  onComplete,
  onClose 
}) => {
  const [status, setStatus] = useState<DeploymentStatusResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isPolling, setIsPolling] = useState(true);
  const pollingIntervalRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    fetchDeploymentStatus();
    
    // Start polling every 3 seconds
    pollingIntervalRef.current = setInterval(() => {
      if (isPolling) {
        fetchDeploymentStatus();
      }
    }, 3000);

    return () => {
      if (pollingIntervalRef.current) {
        clearInterval(pollingIntervalRef.current);
      }
    };
  }, [deploymentId, isPolling]);

  const fetchDeploymentStatus = async () => {
    try {
      const response = await adminApi.getDeploymentStatus(deploymentId);
      setStatus(response);
      setError(null);

      // Stop polling if deployment is complete or failed
      if (response.state === 'Succeeded' || response.state === 'Failed' || response.state === 'Cancelled') {
        setIsPolling(false);
        if (onComplete) {
          onComplete(response.state === 'Succeeded');
        }
      }
    } catch (err: any) {
      console.error('Error fetching deployment status:', err);
      setError(err.message || 'Failed to fetch deployment status');
      setIsPolling(false);
    }
  };

  const getStateIcon = (state: string) => {
    switch (state.toLowerCase()) {
      case 'succeeded':
        return '✅';
      case 'failed':
        return '❌';
      case 'cancelled':
        return '🚫';
      case 'running':
      case 'inprogress':
        return '🔄';
      case 'pending':
        return '⏳';
      default:
        return '⚪';
    }
  };

  const getStateClass = (state: string) => {
    switch (state.toLowerCase()) {
      case 'succeeded':
        return 'state-success';
      case 'failed':
        return 'state-error';
      case 'cancelled':
        return 'state-cancelled';
      case 'running':
      case 'inprogress':
        return 'state-running';
      case 'pending':
        return 'state-pending';
      default:
        return 'state-unknown';
    }
  };

  const getStepIcon = (step: DeploymentStep) => {
    if (step.status === 'Completed') return '✓';
    if (step.status === 'Failed') return '✗';
    if (step.status === 'InProgress') return '↻';
    return '○';
  };

  const getStepClass = (step: DeploymentStep) => {
    if (step.status === 'Completed') return 'step-completed';
    if (step.status === 'Failed') return 'step-failed';
    if (step.status === 'InProgress') return 'step-in-progress';
    return 'step-pending';
  };

  if (error) {
    return (
      <div className="deployment-progress error">
        <div className="progress-header">
          <h3>Deployment Error</h3>
          {onClose && (
            <button className="close-button" onClick={onClose}>×</button>
          )}
        </div>
        <div className="error-content">
          <span className="error-icon">⚠️</span>
          <p>{error}</p>
        </div>
      </div>
    );
  }

  if (!status) {
    return (
      <div className="deployment-progress loading">
        <div className="progress-header">
          <h3>Loading Deployment Status...</h3>
        </div>
        <div className="loading-spinner">
          <div className="spinner"></div>
        </div>
      </div>
    );
  }

  const progressPercentage = status.percentComplete || 0;

  return (
    <div className={`deployment-progress ${getStateClass(status.state)}`}>
      <div className="progress-header">
        <div className="header-content">
          <span className="state-icon">{getStateIcon(status.state)}</span>
          <div className="header-text">
            <h3>{status.deploymentName}</h3>
            <span className="deployment-id">ID: {deploymentId}</span>
          </div>
        </div>
        {onClose && (
          <button className="close-button" onClick={onClose}>×</button>
        )}
      </div>

      <div className="progress-body">
        {/* Overall Progress Bar */}
        <div className="progress-section">
          <div className="section-header">
            <span className="section-title">Overall Progress</span>
            <span className="section-value">{progressPercentage}%</span>
          </div>
          <div className="progress-bar-container">
            <div 
              className="progress-bar-fill"
              style={{ width: `${progressPercentage}%` }}
            />
          </div>
          <div className="progress-info">
            <span className="info-label">State:</span>
            <span className={`info-value ${getStateClass(status.state)}`}>
              {status.state}
            </span>
          </div>
        </div>

        {/* Current Operation */}
        {status.currentOperation && (
          <div className="progress-section">
            <div className="section-header">
              <span className="section-title">Current Operation</span>
            </div>
            <div className="current-operation">
              <span className="operation-icon">🔧</span>
              <span className="operation-text">{status.currentOperation}</span>
            </div>
          </div>
        )}

        {/* Deployment Steps */}
        {status.steps && status.steps.length > 0 && (
          <div className="progress-section">
            <div className="section-header">
              <span className="section-title">Deployment Steps</span>
              <span className="section-value">
                {status.steps.filter(s => s.status === 'Completed').length} / {status.steps.length}
              </span>
            </div>
            <div className="steps-list">
              {status.steps.map((step, index) => (
                <div key={index} className={`step-item ${getStepClass(step)}`}>
                  <span className="step-icon">{getStepIcon(step)}</span>
                  <div className="step-content">
                    <div className="step-name">{step.name}</div>
                    {step.description && (
                      <div className="step-description">{step.description}</div>
                    )}
                    {step.duration && (
                      <div className="step-duration">{step.duration}</div>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Resources Created */}
        {status.resourcesCreated && status.resourcesCreated.length > 0 && (
          <div className="progress-section">
            <div className="section-header">
              <span className="section-title">Resources Created</span>
              <span className="section-value">{status.resourcesCreated.length}</span>
            </div>
            <div className="resources-list">
              {status.resourcesCreated.map((resource, index) => (
                <div key={index} className="resource-item">
                  <span className="resource-icon">📦</span>
                  <span className="resource-name">{resource}</span>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Error Message */}
        {status.errorMessage && (
          <div className="progress-section error-section">
            <div className="section-header">
              <span className="section-title">Error Details</span>
            </div>
            <div className="error-message">
              <span className="error-icon">⚠️</span>
              <span className="error-text">{status.errorMessage}</span>
            </div>
          </div>
        )}

        {/* Timestamps */}
        <div className="progress-section timestamps">
          <div className="timestamp-row">
            <span className="timestamp-label">Started:</span>
            <span className="timestamp-value">
              {status.startTime ? new Date(status.startTime).toLocaleString() : 'N/A'}
            </span>
          </div>
          {status.endTime && (
            <div className="timestamp-row">
              <span className="timestamp-label">Ended:</span>
              <span className="timestamp-value">
                {new Date(status.endTime).toLocaleString()}
              </span>
            </div>
          )}
          {status.estimatedCompletion && !status.endTime && (
            <div className="timestamp-row">
              <span className="timestamp-label">Est. Completion:</span>
              <span className="timestamp-value">
                {new Date(status.estimatedCompletion).toLocaleString()}
              </span>
            </div>
          )}
        </div>
      </div>

      {/* Action Buttons */}
      <div className="progress-footer">
        {isPolling && (
          <button 
            className="btn-secondary"
            onClick={() => setIsPolling(false)}
          >
            Pause Updates
          </button>
        )}
        {!isPolling && status.state !== 'Succeeded' && status.state !== 'Failed' && (
          <button 
            className="btn-primary"
            onClick={() => setIsPolling(true)}
          >
            Resume Updates
          </button>
        )}
        {onClose && (status.state === 'Succeeded' || status.state === 'Failed') && (
          <button 
            className="btn-primary"
            onClick={onClose}
          >
            Close
          </button>
        )}
      </div>
    </div>
  );
};

export default DeploymentProgress;
