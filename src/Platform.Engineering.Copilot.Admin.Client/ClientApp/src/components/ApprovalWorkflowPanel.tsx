import React, { useEffect, useState } from 'react';
import adminApi, { ApprovalWorkflow } from '../services/adminApi';
import './ApprovalWorkflowPanel.css';

const ApprovalWorkflowPanel: React.FC = () => {
  const [workflows, setWorkflows] = useState<ApprovalWorkflow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [currentUser] = useState('admin'); // TODO: Get from auth context
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);

  useEffect(() => {
    loadApprovals();
    // Refresh every 30 seconds
    const interval = setInterval(loadApprovals, 30000);
    return () => clearInterval(interval);
  }, []);

  const loadApprovals = async () => {
    try {
      setLoading(true);
      const data = await adminApi.getPendingApprovals();
      setWorkflows(data);
      setError(null);
    } catch (err: any) {
      setError(err.message || 'Failed to load pending approvals');
    } finally {
      setLoading(false);
    }
  };

  const handleApprove = async (workflowId: string) => {
    if (!window.confirm('Are you sure you want to approve this infrastructure provisioning request?')) {
      return;
    }

    setActionInProgress(workflowId);
    try {
      const response = await adminApi.approveWorkflow(workflowId, currentUser, 'Approved via Admin Dashboard');
      if (response.success) {
        window.alert('✅ Approval workflow approved successfully!');
        await loadApprovals(); // Refresh list
      } else {
        window.alert(`❌ Failed to approve: ${response.message}`);
      }
    } catch (err: any) {
      window.alert(`❌ Error approving workflow: ${err.message}`);
    } finally {
      setActionInProgress(null);
    }
  };

  const handleReject = async (workflowId: string) => {
    const reason = window.prompt('Please provide a reason for rejection:');
    if (!reason || reason.trim() === '') {
      return;
    }

    setActionInProgress(workflowId);
    try {
      const response = await adminApi.rejectWorkflow(workflowId, currentUser, reason);
      if (response.success) {
        window.alert('❌ Approval workflow rejected successfully');
        await loadApprovals(); // Refresh list
      } else {
        window.alert(`❌ Failed to reject: ${response.message}`);
      }
    } catch (err: any) {
      window.alert(`❌ Error rejecting workflow: ${err.message}`);
    } finally {
      setActionInProgress(null);
    }
  };

  const getStatusColor = (status: string): string => {
    switch (status.toLowerCase()) {
      case 'pending':
        return '#ff9800';
      case 'approved':
        return '#4caf50';
      case 'rejected':
        return '#f44336';
      case 'expired':
        return '#9e9e9e';
      default:
        return '#2196f3';
    }
  };

  const isExpiringSoon = (expiresAt: string): boolean => {
    const expiryDate = new Date(expiresAt);
    const now = new Date();
    const hoursUntilExpiry = (expiryDate.getTime() - now.getTime()) / (1000 * 60 * 60);
    return hoursUntilExpiry < 4 && hoursUntilExpiry > 0;
  };

  const isExpired = (expiresAt: string): boolean => {
    return new Date(expiresAt) < new Date();
  };

  if (loading && workflows.length === 0) {
    return (
      <div className="approval-panel">
        <div className="approval-header">
          <h3>🔐 Pending Approvals</h3>
        </div>
        <div className="approval-loading">Loading pending approvals...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="approval-panel">
        <div className="approval-header">
          <h3>🔐 Pending Approvals</h3>
        </div>
        <div className="approval-error">Error: {error}</div>
      </div>
    );
  }

  if (workflows.length === 0) {
    return (
      <div className="approval-panel">
        <div className="approval-header">
          <h3>🔐 Pending Approvals</h3>
        </div>
        <div className="approval-empty">
          <p>✅ No pending approvals</p>
          <small>Infrastructure requests requiring approval will appear here</small>
        </div>
      </div>
    );
  }

  return (
    <div className="approval-panel">
      <div className="approval-header">
        <h3>🔐 Pending Approvals</h3>
        <button onClick={loadApprovals} className="refresh-btn" disabled={loading}>
          {loading ? '⟳ Refreshing...' : '↻ Refresh'}
        </button>
      </div>

      <div className="approval-list">
        {workflows.map((workflow) => {
          const expired = isExpired(workflow.expiresAt);
          const expiringSoon = isExpiringSoon(workflow.expiresAt);

          return (
            <div key={workflow.id} className={`approval-card ${expired ? 'expired' : ''}`}>
              <div className="approval-card-header">
                <div className="approval-info">
                  <h4>{workflow.resourceName}</h4>
                  <span className="resource-type">{workflow.resourceType}</span>
                  <span
                    className="approval-status"
                    style={{ backgroundColor: getStatusColor(workflow.status) }}
                  >
                    {workflow.status}
                  </span>
                </div>
                {expiringSoon && !expired && (
                  <div className="expiry-warning">⚠️ Expires soon</div>
                )}
                {expired && <div className="expired-badge">⏰ Expired</div>}
              </div>

              <div className="approval-details">
                <div className="detail-row">
                  <span className="label">Environment:</span>
                  <span className="value">{workflow.environment.toUpperCase()}</span>
                </div>
                <div className="detail-row">
                  <span className="label">Location:</span>
                  <span className="value">{workflow.location}</span>
                </div>
                <div className="detail-row">
                  <span className="label">Resource Group:</span>
                  <span className="value">{workflow.resourceGroupName}</span>
                </div>
                <div className="detail-row">
                  <span className="label">Requested By:</span>
                  <span className="value">{workflow.requestedBy}</span>
                </div>
                <div className="detail-row">
                  <span className="label">Requested:</span>
                  <span className="value">{new Date(workflow.requestedAt).toLocaleString()}</span>
                </div>
                <div className="detail-row">
                  <span className="label">Expires:</span>
                  <span className="value">{new Date(workflow.expiresAt).toLocaleString()}</span>
                </div>
              </div>

              {workflow.reason && (
                <div className="approval-reason">
                  <strong>Reason:</strong> {workflow.reason}
                </div>
              )}

              {workflow.policyViolations && workflow.policyViolations.length > 0 && (
                <div className="policy-violations">
                  <strong>⚠️ Policy Considerations:</strong>
                  <ul>
                    {workflow.policyViolations.map((violation, idx) => (
                      <li key={idx}>{violation}</li>
                    ))}
                  </ul>
                </div>
              )}

              {workflow.requiredApprovers && workflow.requiredApprovers.length > 0 && (
                <div className="required-approvers">
                  <strong>Required Approvers:</strong> {workflow.requiredApprovers.join(', ')}
                </div>
              )}

              <div className="approval-actions">
                <button
                  className="approve-btn"
                  onClick={() => handleApprove(workflow.id)}
                  disabled={actionInProgress === workflow.id || expired}
                >
                  {actionInProgress === workflow.id ? '⏳ Processing...' : '✅ Approve'}
                </button>
                <button
                  className="reject-btn"
                  onClick={() => handleReject(workflow.id)}
                  disabled={actionInProgress === workflow.id || expired}
                >
                  {actionInProgress === workflow.id ? '⏳ Processing...' : '❌ Reject'}
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};

export default ApprovalWorkflowPanel;
