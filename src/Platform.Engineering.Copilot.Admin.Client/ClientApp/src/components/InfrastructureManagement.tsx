import React, { useState, useEffect } from 'react';
import adminApi from '../services/adminApi';
import './InfrastructureManagement.css';

interface ProvisionRequest {
  resourceType: string; // 'vnet' | 'storage' | 'keyvault' | 'subnet' | etc.
  resourceGroupName: string;
  location: string;
  parameters?: Record<string, any>;
}

interface ResourceGroup {
  name: string;
  location: string;
  status: string;
  resourceCount?: number;
}

const InfrastructureManagement: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'provision' | 'resources' | 'cost'>('provision');
  const [resourceGroups, setResourceGroups] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Provision form state
  const [provisionForm, setProvisionForm] = useState<ProvisionRequest>({
    resourceType: '',
    resourceGroupName: '',
    location: 'eastus',
    parameters: {}
  });

  // Cost estimation state
  const [costEstimate, setCostEstimate] = useState<any>(null);

  useEffect(() => {
    loadResourceGroups();
  }, []);

  const loadResourceGroups = async () => {
    try {
      const data = await adminApi.listResourceGroupNames();
      setResourceGroups(data);
    } catch (err: any) {
      console.error('Failed to load resource groups:', err);
    }
  };

  const handleProvision = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    setSuccess(null);

    try {
      const response = await adminApi.provisionInfrastructure({
        resourceType: provisionForm.resourceType,
        resourceGroupName: provisionForm.resourceGroupName,
        location: provisionForm.location,
        parameters: provisionForm.parameters
      });
      
      if (response.success) {
        setSuccess(`✅ Infrastructure provisioning initiated! ${response.message || ''}`);
      } else {
        setError(response.errorMessage || 'Provisioning failed');
      }
      
      // Reset form
      setProvisionForm({
        resourceType: '',
        resourceGroupName: '',
        location: 'eastus',
        parameters: {}
      });
      
      // Reload resource groups
      loadResourceGroups();
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || 'Failed to provision infrastructure');
    } finally {
      setLoading(false);
    }
  };

  const handleEstimateCost = async () => {
    if (!provisionForm.resourceType || !provisionForm.location) {
      setError('Please select a resource type and location');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const estimate = await adminApi.estimateCost({
        resourceType: provisionForm.resourceType,
        location: provisionForm.location,
        parameters: provisionForm.parameters
      });
      setCostEstimate(estimate);
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || 'Failed to estimate cost');
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteResourceGroup = async (resourceGroupName: string) => {
    if (!window.confirm(`Are you sure you want to delete resource group "${resourceGroupName}"? This action cannot be undone.`)) {
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await adminApi.deleteResourceGroup(resourceGroupName);
      setSuccess(`✅ Resource group "${resourceGroupName}" deleted successfully`);
      loadResourceGroups();
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || 'Failed to delete resource group');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="infrastructure-management">
      <div className="infrastructure-header">
        <h2>🏗️ Infrastructure Management</h2>
        <p>Provision and manage foundational cloud infrastructure resources (VNets, Storage, Key Vaults, etc.)</p>
      </div>

      {/* Tab Navigation */}
      <div className="tab-navigation">
        <button
          className={`tab-button ${activeTab === 'provision' ? 'active' : ''}`}
          onClick={() => setActiveTab('provision')}
        >
          🚀 Provision
        </button>
        <button
          className={`tab-button ${activeTab === 'resources' ? 'active' : ''}`}
          onClick={() => setActiveTab('resources')}
        >
          📦 Resources
        </button>
        <button
          className={`tab-button ${activeTab === 'cost' ? 'active' : ''}`}
          onClick={() => setActiveTab('cost')}
        >
          💰 Cost Estimation
        </button>
      </div>

      {/* Alerts */}
      {error && (
        <div className="alert alert-error">
          ⚠️ {error}
          <button onClick={() => setError(null)} className="alert-close">✕</button>
        </div>
      )}

      {success && (
        <div className="alert alert-success">
          {success}
          <button onClick={() => setSuccess(null)} className="alert-close">✕</button>
        </div>
      )}

      {/* Provision Tab */}
      {activeTab === 'provision' && (
        <div className="tab-content">
          <div className="provision-section">
            <h3>🚀 Provision Foundational Infrastructure</h3>
            <p className="section-description">
              Provision platform/infrastructure resources independent of service deployments.
              For service-specific deployments (apps, microservices), use the <strong>Environment Management</strong> section with service templates.
            </p>
            <form onSubmit={handleProvision} className="provision-form">
              <div className="form-group">
                <label>Infrastructure Resource Type *</label>
                <select
                  value={provisionForm.resourceType}
                  onChange={(e) => setProvisionForm({ ...provisionForm, resourceType: e.target.value })}
                  required
                >
                  <option value="">Select a resource type...</option>
                  <optgroup label="Networking">
                    <option value="vnet">Virtual Network (VNet)</option>
                    <option value="subnet">Subnet</option>
                    <option value="nsg">Network Security Group (NSG)</option>
                    <option value="load-balancer">Load Balancer</option>
                    <option value="app-gateway">Application Gateway</option>
                    <option value="vpn-gateway">VPN Gateway</option>
                  </optgroup>
                  <optgroup label="Storage">
                    <option value="storage-account">Storage Account</option>
                    <option value="blob-container">Blob Container</option>
                    <option value="file-share">File Share</option>
                  </optgroup>
                  <optgroup label="Security">
                    <option value="key-vault">Key Vault</option>
                    <option value="managed-identity">Managed Identity</option>
                  </optgroup>
                  <optgroup label="Management">
                    <option value="log-analytics">Log Analytics Workspace</option>
                    <option value="app-insights">Application Insights</option>
                  </optgroup>
                </select>
                <small className="form-help">
                  Select the type of foundational infrastructure resource to provision
                </small>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label>Resource Group Name *</label>
                  <input
                    type="text"
                    value={provisionForm.resourceGroupName}
                    onChange={(e) => setProvisionForm({ ...provisionForm, resourceGroupName: e.target.value })}
                    placeholder="e.g., rg-myapp-prod"
                    required
                  />
                </div>

                <div className="form-group">
                  <label>Location *</label>
                  <select
                    value={provisionForm.location}
                    onChange={(e) => setProvisionForm({ ...provisionForm, location: e.target.value })}
                    required
                  >
                    <optgroup label="US Commercial Cloud">
                      <option value="eastus">East US</option>
                      <option value="westus">West US</option>
                      <option value="westus2">West US 2</option>
                      <option value="centralus">Central US</option>
                      <option value="northcentralus">North Central US</option>
                      <option value="southcentralus">South Central US</option>
                      <option value="eastus2">East US 2</option>
                      <option value="westus3">West US 3</option>
                    </optgroup>
                    <optgroup label="US Government Cloud">
                      <option value="usgovvirginia">US Gov Virginia</option>
                      <option value="usgovarizona">US Gov Arizona</option>
                      <option value="usgovtexas">US Gov Texas</option>
                      <option value="usdodeast">US DoD East</option>
                      <option value="usdodcentral">US DoD Central</option>
                    </optgroup>
                    <optgroup label="Other Regions">
                      <option value="northeurope">North Europe</option>
                      <option value="westeurope">West Europe</option>
                      <option value="southeastasia">Southeast Asia</option>
                      <option value="australiaeast">Australia East</option>
                    </optgroup>
                  </select>
                </div>
              </div>

              <div className="form-actions">
                <button
                  type="button"
                  onClick={handleEstimateCost}
                  disabled={loading || !provisionForm.resourceType}
                  className="btn-secondary"
                >
                  💰 Estimate Cost
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="btn-primary"
                >
                  {loading ? '⏳ Provisioning...' : '🚀 Provision Infrastructure'}
                </button>
              </div>
            </form>

            {costEstimate && (
              <div className="cost-estimate-panel">
                <h4>💰 Cost Estimate</h4>
                <div className="cost-details">
                  <div className="cost-item">
                    <span className="cost-label">Resource Type:</span>
                    <span className="cost-value">{costEstimate.resourceType}</span>
                  </div>
                  <div className="cost-item">
                    <span className="cost-label">Location:</span>
                    <span className="cost-value">{costEstimate.location}</span>
                  </div>
                  <div className="cost-item">
                    <span className="cost-label">Monthly Estimate:</span>
                    <span className="cost-value">${costEstimate.estimatedMonthlyCost?.toFixed(2) || '0.00'} {costEstimate.currency}</span>
                  </div>
                  <div className="cost-item">
                    <span className="cost-label">Annual Estimate:</span>
                    <span className="cost-value">${costEstimate.estimatedAnnualCost?.toFixed(2) || '0.00'} {costEstimate.currency}</span>
                  </div>
                  {costEstimate.notes && (
                    <div className="cost-note">
                      <small>ℹ️ {costEstimate.notes}</small>
                    </div>
                  )}
                  {costEstimate.breakdown && Object.keys(costEstimate.breakdown).length > 0 && (
                    <div className="cost-breakdown">
                      <h5>Breakdown:</h5>
                      <ul>
                        {Object.entries(costEstimate.breakdown).map(([resource, cost]) => (
                          <li key={resource}>
                            <span>{resource}:</span>
                            <span>${(cost as number).toFixed(2)}</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Resources Tab */}
      {activeTab === 'resources' && (
        <div className="tab-content">
          <div className="resources-section">
            <div className="section-header">
              <h3>📦 Resource Groups</h3>
              <button onClick={loadResourceGroups} className="btn-refresh">
                🔄 Refresh
              </button>
            </div>

            {resourceGroups.length === 0 ? (
              <div className="empty-state">
                <p>No resource groups found</p>
                <p className="empty-hint">Create infrastructure using the Provision tab</p>
              </div>
            ) : (
              <div className="resource-groups-list">
                {resourceGroups.map((rgName) => (
                  <div key={rgName} className="resource-group-card">
                    <div className="rg-header">
                      <div className="rg-info">
                        <h4>{rgName}</h4>
                        <span className="rg-status status-active">✅ Active</span>
                      </div>
                      <div className="rg-actions">
                        <button
                          onClick={() => {
                            alert(`View details for ${rgName}\n\nThis will show:\n- Resources in the group\n- Cost breakdown\n- Activity logs`);
                          }}
                          className="btn-view"
                        >
                          👁️ View
                        </button>
                        <button
                          onClick={() => handleDeleteResourceGroup(rgName)}
                          className="btn-delete"
                          disabled={loading}
                        >
                          🗑️ Delete
                        </button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {/* Cost Tab */}
      {activeTab === 'cost' && (
        <div className="tab-content">
          <div className="cost-section">
            <h3>💰 Cost Estimation Tool</h3>
            <div className="cost-calculator">
              <div className="calculator-info">
                <p>💡 Use the <strong>Provision</strong> tab to estimate costs for specific infrastructure resources and configurations.</p>
                <p>The cost estimation tool provides:</p>
                <ul>
                  <li>Monthly cost projections</li>
                  <li>Annual cost estimates</li>
                  <li>Resource-level cost breakdown</li>
                  <li>Comparison between regions</li>
                </ul>
              </div>

              <div className="cost-features">
                <h4>📊 Cost Management Features</h4>
                <div className="feature-grid">
                  <div className="feature-card">
                    <div className="feature-icon">💵</div>
                    <h5>Cost Forecasting</h5>
                    <p>Predict future costs based on usage patterns</p>
                  </div>
                  <div className="feature-card">
                    <div className="feature-icon">📈</div>
                    <h5>Budget Alerts</h5>
                    <p>Set up alerts when costs exceed thresholds</p>
                  </div>
                  <div className="feature-card">
                    <div className="feature-icon">🔍</div>
                    <h5>Cost Analysis</h5>
                    <p>Detailed breakdown of resource costs</p>
                  </div>
                  <div className="feature-card">
                    <div className="feature-icon">💡</div>
                    <h5>Optimization Tips</h5>
                    <p>Recommendations to reduce costs</p>
                  </div>
                </div>
              </div>

              <div className="action-banner">
                <p>🚀 <strong>Ready to provision?</strong> Go to the Provision tab to get started!</p>
                <button onClick={() => setActiveTab('provision')} className="btn-primary">
                  Go to Provision
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default InfrastructureManagement;
