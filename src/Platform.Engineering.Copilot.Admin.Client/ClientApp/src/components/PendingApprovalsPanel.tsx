import React, { useEffect, useState } from 'react';
import adminApi, { ApprovalWorkflow, OnboardingRequest } from '../services/adminApi';
import './PendingApprovalsPanel.css';

type ApprovalType = 'infrastructure' | 'onboarding';

interface UnifiedApproval {
  id: string;
  type: ApprovalType;
  title: string;
  requester: string;
  requestedAt: string;
  status: string;
  data: ApprovalWorkflow | OnboardingRequest;
}

const PendingApprovalsPanel: React.FC = () => {
  const [approvals, setApprovals] = useState<UnifiedApproval[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [currentUser] = useState('NNWC Admin'); // TODO: Get from auth context
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);
  const [expandedApproval, setExpandedApproval] = useState<string | null>(null);
  const [filterType, setFilterType] = useState<'all' | ApprovalType>('all');

  useEffect(() => {
    loadAllApprovals();
    // Refresh every 30 seconds
    const interval = setInterval(loadAllApprovals, 30000);
    return () => clearInterval(interval);
  }, []);

  const loadAllApprovals = async () => {
    try {
      setLoading(true);
      
      // Load both infrastructure approvals and onboarding requests in parallel
      const [infraApprovals, onboardingRequests] = await Promise.all([
        adminApi.getPendingApprovals().catch(() => []),
        adminApi.getPendingOnboardingRequests().catch(() => [])
      ]);

      // Unify the data structure
      const unified: UnifiedApproval[] = [
        ...infraApprovals.map((workflow: ApprovalWorkflow) => ({
          id: workflow.id,
          type: 'infrastructure' as ApprovalType,
          title: `${workflow.resourceType} - ${workflow.resourceName}`,
          requester: workflow.requestedBy,
          requestedAt: workflow.requestedAt,
          status: workflow.status,
          data: workflow
        })),
        ...onboardingRequests.map((request: OnboardingRequest) => ({
          id: request.id,
          type: 'onboarding' as ApprovalType,
          title: `${request.missionName || 'Mission'} - Flankspeed Onboarding`,
          requester: request.missionOwner || request.missionOwnerEmail,
          requestedAt: request.createdAt,
          status: request.status,
          data: request
        }))
      ];

      // Sort by requested date (newest first)
      unified.sort((a, b) => new Date(b.requestedAt).getTime() - new Date(a.requestedAt).getTime());

      setApprovals(unified);
      setError(null);
    } catch (err: any) {
      setError(err.message || 'Failed to load pending approvals');
    } finally {
      setLoading(false);
    }
  };

  const handleApproveInfra = async (workflowId: string) => {
    if (!window.confirm('Are you sure you want to approve this infrastructure provisioning request?')) {
      return;
    }

    setActionInProgress(workflowId);
    try {
      const response = await adminApi.approveWorkflow(workflowId, currentUser, 'Approved via Admin Dashboard');
      if (response.success) {
        window.alert('✅ Infrastructure approval successful!');
        await loadAllApprovals();
      } else {
        window.alert(`❌ Failed to approve: ${response.message}`);
      }
    } catch (err: any) {
      window.alert(`❌ Error approving workflow: ${err.message}`);
    } finally {
      setActionInProgress(null);
    }
  };

  const handleRejectInfra = async (workflowId: string) => {
    const reason = window.prompt('Please provide a reason for rejection:');
    if (!reason || reason.trim() === '') {
      return;
    }

    setActionInProgress(workflowId);
    try {
      const response = await adminApi.rejectWorkflow(workflowId, currentUser, reason);
      if (response.success) {
        window.alert('❌ Infrastructure request rejected');
        await loadAllApprovals();
      } else {
        window.alert(`❌ Failed to reject: ${response.message}`);
      }
    } catch (err: any) {
      window.alert(`❌ Error rejecting workflow: ${err.message}`);
    } finally {
      setActionInProgress(null);
    }
  };

  const handleApproveOnboarding = async (requestId: string) => {
    const comments = window.prompt('Optional: Add approval comments (or press OK to continue without comments):');
    
    setActionInProgress(requestId);
    try {
      const response = await adminApi.approveOnboardingRequest(requestId, currentUser, comments || undefined);
      if (response.success) {
        window.alert(`✅ Onboarding request approved successfully!\n\n${response.message}\n\nProvisioning Job ID: ${response.provisioningJobId || 'N/A'}`);
        await loadAllApprovals();
      } else {
        window.alert(`❌ Failed to approve: ${response.message}`);
      }
    } catch (err: any) {
      window.alert(`❌ Error approving request: ${err.message}`);
    } finally {
      setActionInProgress(null);
    }
  };

  const handleRejectOnboarding = async (requestId: string) => {
    const reason = window.prompt('⚠️ Please provide a reason for rejection (required):');
    
    if (!reason || reason.trim() === '') {
      window.alert('Rejection reason is required');
      return;
    }

    setActionInProgress(requestId);
    try {
      const response = await adminApi.rejectOnboardingRequest(requestId, currentUser, reason);
      if (response.success) {
        window.alert('❌ Onboarding request rejected. Mission owner will be notified.');
        await loadAllApprovals();
      } else {
        window.alert(`❌ Failed to reject: ${response.message}`);
      }
    } catch (err: any) {
      window.alert(`❌ Error rejecting request: ${err.message}`);
    } finally {
      setActionInProgress(null);
    }
  };

  const toggleExpanded = (id: string) => {
    setExpandedApproval(expandedApproval === id ? null : id);
  };

  const getStatusColor = (status: string): string => {
    switch (status.toLowerCase()) {
      case 'draft':
        return '#9e9e9e';
      case 'pending':
      case 'pendingreview':
      case 'underreview':
        return '#ff9800';
      case 'approved':
        return '#4caf50';
      case 'provisioning':
        return '#2196f3';
      case 'completed':
        return '#4caf50';
      case 'rejected':
        return '#f44336';
      case 'failed':
        return '#f44336';
      case 'cancelled':
      case 'expired':
        return '#9e9e9e';
      default:
        return '#2196f3';
    }
  };

  const getTypeIcon = (type: ApprovalType): string => {
    return type === 'infrastructure' ? '🏗️' : '⚓';
  };

  const getTypeLabel = (type: ApprovalType): string => {
    return type === 'infrastructure' ? 'Infrastructure' : 'Onboarding';
  };

  const filteredApprovals = filterType === 'all' 
    ? approvals 
    : approvals.filter(a => a.type === filterType);

  if (loading && approvals.length === 0) {
    return (
      <div className="pending-approvals-panel">
        <div className="approval-header">
          <h3>🔐 Pending Approvals</h3>
        </div>
        <div className="approval-loading">Loading pending approvals...</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="pending-approvals-panel">
        <div className="approval-header">
          <h3>🔐 Pending Approvals</h3>
        </div>
        <div className="approval-error">Error: {error}</div>
      </div>
    );
  }

  const infraCount = approvals.filter(a => a.type === 'infrastructure').length;
  const onboardingCount = approvals.filter(a => a.type === 'onboarding').length;

  if (approvals.length === 0) {
    return (
      <div className="pending-approvals-panel">
        <div className="approval-header">
          <h3>🔐 Pending Approvals</h3>
        </div>
        <div className="approval-empty">
          <p>✅ No pending approvals</p>
          <small>Infrastructure and onboarding requests requiring approval will appear here</small>
        </div>
      </div>
    );
  }

  return (
    <div className="pending-approvals-panel">
      <div className="approval-header">
        <div className="header-left">
          <h3>🔐 Pending Approvals</h3>
          <span className="approval-count">{approvals.length} total</span>
        </div>
        <div className="header-right">
          <div className="filter-buttons">
            <button 
              className={filterType === 'all' ? 'active' : ''}
              onClick={() => setFilterType('all')}
            >
              All ({approvals.length})
            </button>
            <button 
              className={filterType === 'infrastructure' ? 'active' : ''}
              onClick={() => setFilterType('infrastructure')}
            >
              🏗️ Infrastructure ({infraCount})
            </button>
            <button 
              className={filterType === 'onboarding' ? 'active' : ''}
              onClick={() => setFilterType('onboarding')}
            >
              ⚓ Onboarding ({onboardingCount})
            </button>
          </div>
          <button onClick={loadAllApprovals} className="refresh-btn" disabled={loading}>
            {loading ? '⟳ Refreshing...' : '↻ Refresh'}
          </button>
        </div>
      </div>

      <div className="approval-list">
        {filteredApprovals.map((approval) => {
          const isExpanded = expandedApproval === approval.id;
          const isInProgress = actionInProgress === approval.id;

          return (
            <div key={approval.id} className={`approval-card ${approval.type}`}>
              <div className="approval-card-header" onClick={() => toggleExpanded(approval.id)}>
                <div className="approval-type-badge">
                  {getTypeIcon(approval.type)} {getTypeLabel(approval.type)}
                </div>
                <div className="approval-info">
                  <h4>{approval.title}</h4>
                  <div className="approval-meta">
                    <span className="requester">👤 {approval.requester}</span>
                    <span className="requested-at">📅 {new Date(approval.requestedAt).toLocaleString()}</span>
                    <span 
                      className="status-badge"
                      style={{ backgroundColor: getStatusColor(approval.status) }}
                    >
                      {approval.status}
                    </span>
                  </div>
                </div>
                <div className="expand-icon">{isExpanded ? '▼' : '▶'}</div>
              </div>

              {isExpanded && (
                <div className="approval-details">
                  {approval.type === 'infrastructure' ? (
                    <InfrastructureDetails 
                      workflow={approval.data as ApprovalWorkflow}
                      onApprove={() => handleApproveInfra(approval.id)}
                      onReject={() => handleRejectInfra(approval.id)}
                      isInProgress={isInProgress}
                    />
                  ) : (
                    <OnboardingDetails 
                      request={approval.data as OnboardingRequest}
                      onApprove={() => handleApproveOnboarding(approval.id)}
                      onReject={() => handleRejectOnboarding(approval.id)}
                      isInProgress={isInProgress}
                    />
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};

// Sub-component for Infrastructure Approval Details
const InfrastructureDetails: React.FC<{
  workflow: ApprovalWorkflow;
  onApprove: () => void;
  onReject: () => void;
  isInProgress: boolean;
}> = ({ workflow, onApprove, onReject, isInProgress }) => {
  const isExpired = new Date(workflow.expiresAt) < new Date();

  return (
    <>
      <div className="details-grid">
        <div className="detail-item">
          <strong>Resource Type:</strong>
          <span>{workflow.resourceType}</span>
        </div>
        <div className="detail-item">
          <strong>Resource Name:</strong>
          <span>{workflow.resourceName}</span>
        </div>
        <div className="detail-item">
          <strong>Resource Group:</strong>
          <span>{workflow.resourceGroupName}</span>
        </div>
        <div className="detail-item">
          <strong>Environment:</strong>
          <span>{workflow.environment}</span>
        </div>
        <div className="detail-item">
          <strong>Location:</strong>
          <span>{workflow.location}</span>
        </div>
        <div className="detail-item">
          <strong>Expires:</strong>
          <span className={isExpired ? 'expired' : ''}>
            {new Date(workflow.expiresAt).toLocaleString()}
            {isExpired && ' (EXPIRED)'}
          </span>
        </div>
      </div>

      {workflow.reason && (
        <div className="metadata-section">
          <strong>Reason:</strong>
          <p>{workflow.reason}</p>
        </div>
      )}

      {workflow.policyViolations && workflow.policyViolations.length > 0 && (
        <div className="violations-section">
          <strong>Policy Violations:</strong>
          <ul>
            {workflow.policyViolations.map((violation, idx) => (
              <li key={idx}>{violation}</li>
            ))}
          </ul>
        </div>
      )}

      <div className="approval-actions">
        <button 
          onClick={onApprove} 
          className="approve-btn"
          disabled={isInProgress || isExpired}
        >
          {isInProgress ? '⟳ Processing...' : '✅ Approve'}
        </button>
        <button 
          onClick={onReject} 
          className="reject-btn"
          disabled={isInProgress}
        >
          {isInProgress ? '⟳ Processing...' : '❌ Reject'}
        </button>
      </div>
    </>
  );
};

// Sub-component for Onboarding Request Details
const OnboardingDetails: React.FC<{
  request: OnboardingRequest;
  onApprove: () => void;
  onReject: () => void;
  isInProgress: boolean;
}> = ({ request, onApprove, onReject, isInProgress }) => {
  return (
    <>
      <div className="details-grid">
        <div className="detail-item">
          <strong>Mission:</strong>
          <span>{request.missionName || 'N/A'}</span>
        </div>
        <div className="detail-item">
          <strong>Mission Owner:</strong>
          <span>{request.missionOwner || 'N/A'}</span>
        </div>
        <div className="detail-item">
          <strong>Rank:</strong>
          <span>{request.missionOwnerRank || 'N/A'}</span>
        </div>
        <div className="detail-item">
          <strong>Command:</strong>
          <span>{request.command || 'N/A'}</span>
        </div>
        <div className="detail-item">
          <strong>Classification:</strong>
          <span>{request.classificationLevel || 'N/A'}</span>
        </div>
        <div className="detail-item">
          <strong>Email:</strong>
          <span>{request.missionOwnerEmail}</span>
        </div>
      </div>

      <div className="details-grid">
        <div className="detail-item">
          <strong>Subscription:</strong>
          <span>{request.requestedSubscriptionName || 'N/A'}</span>
        </div>
        <div className="detail-item">
          <strong>VNet CIDR:</strong>
          <span>{request.requestedVNetCidr || 'N/A'}</span>
        </div>
        <div className="detail-item">
          <strong>Services:</strong>
          <span>{request.requiredServices?.join(', ') || 'N/A'}</span>
        </div>
        <div className="detail-item">
          <strong>Region:</strong>
          <span>{request.region || 'N/A'}</span>
        </div>
        <div className="detail-item">
          <strong>Users:</strong>
          <span>{request.estimatedUserCount || 'N/A'}</span>
        </div>
        <div className="detail-item">
          <strong>Data Volume:</strong>
          <span>{request.estimatedDataVolumeTB ? `${request.estimatedDataVolumeTB} TB` : 'N/A'}</span>
        </div>
      </div>

      {request.businessJustification && (
        <div className="justification-section">
          <strong>Business Justification:</strong>
          <p>{request.businessJustification}</p>
        </div>
      )}

      <div className="approval-actions">
        <button 
          onClick={onApprove} 
          className="approve-btn"
          disabled={isInProgress}
        >
          {isInProgress ? '⟳ Processing...' : '✅ Approve & Provision'}
        </button>
        <button 
          onClick={onReject} 
          className="reject-btn"
          disabled={isInProgress}
        >
          {isInProgress ? '⟳ Processing...' : '❌ Reject'}
        </button>
      </div>
    </>
  );
};

export default PendingApprovalsPanel;
