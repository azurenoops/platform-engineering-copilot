import React, { useState, useEffect } from 'react';
import adminApi, { 
  AzureSubscription, 
  AzureResourceGroup, 
  AzureVNet,
  ExistingSubnetReference,
  SubnetPurpose
} from '../services/adminApi';
import './ExistingNetworkSelector.css';

interface ExistingNetworkSelectorProps {
  existingVNetName?: string;
  existingVNetResourceGroup?: string;
  existingSubnets: ExistingSubnetReference[];
  onUpdate: (field: string, value: any) => void;
}

const ExistingNetworkSelector: React.FC<ExistingNetworkSelectorProps> = ({
  existingVNetName,
  existingVNetResourceGroup,
  existingSubnets,
  onUpdate
}) => {
  const [subscriptions, setSubscriptions] = useState<AzureSubscription[]>([]);
  const [resourceGroups, setResourceGroups] = useState<AzureResourceGroup[]>([]);
  const [vnets, setVNets] = useState<AzureVNet[]>([]);
  const [selectedSubscription, setSelectedSubscription] = useState<string>('');
  const [selectedResourceGroup, setSelectedResourceGroup] = useState<string>(existingVNetResourceGroup || '');
  const [selectedVNet, setSelectedVNet] = useState<AzureVNet | null>(null);
  const [loading, setLoading] = useState<{[key: string]: boolean}>({});
  const [error, setError] = useState<string | null>(null);

  // Load subscriptions on mount
  useEffect(() => {
    loadSubscriptions();
  }, []);

  // Load resource groups when subscription changes
  useEffect(() => {
    if (selectedSubscription) {
      loadResourceGroups();
    }
  }, [selectedSubscription]);

  // Load VNets when resource group changes
  useEffect(() => {
    if (selectedResourceGroup) {
      loadVNets();
    }
  }, [selectedResourceGroup]);

  const loadSubscriptions = async () => {
    try {
      setLoading(prev => ({ ...prev, subscriptions: true }));
      setError(null);
      const subs = await adminApi.listSubscriptions();
      setSubscriptions(subs);
      if (subs.length === 1) {
        setSelectedSubscription(subs[0].id);
      }
    } catch (err) {
      setError('Failed to load subscriptions');
      console.error(err);
    } finally {
      setLoading(prev => ({ ...prev, subscriptions: false }));
    }
  };

  const loadResourceGroups = async () => {
    try {
      setLoading(prev => ({ ...prev, resourceGroups: true }));
      setError(null);
      const rgs = await adminApi.listResourceGroups(selectedSubscription);
      setResourceGroups(rgs);
    } catch (err) {
      setError('Failed to load resource groups');
      console.error(err);
    } finally {
      setLoading(prev => ({ ...prev, resourceGroups: false }));
    }
  };

  const loadVNets = async () => {
    try {
      setLoading(prev => ({ ...prev, vnets: true }));
      setError(null);
      const vnetList = await adminApi.listVNets(selectedSubscription, selectedResourceGroup);
      setVNets(vnetList);
      
      // If VNet name is pre-selected, find and select it
      if (existingVNetName) {
        const vnet = vnetList.find(v => v.name === existingVNetName);
        if (vnet) {
          handleVNetSelect(vnet);
        }
      }
    } catch (err) {
      setError('Failed to load virtual networks');
      console.error(err);
    } finally {
      setLoading(prev => ({ ...prev, vnets: false }));
    }
  };

  const handleVNetSelect = (vnet: AzureVNet) => {
    setSelectedVNet(vnet);
    onUpdate('existingVNetName', vnet.name);
    onUpdate('existingVNetResourceGroup', selectedResourceGroup);
    onUpdate('existingVNetResourceId', vnet.id);
    
    // Clear existing subnet selections when VNet changes
    onUpdate('existingSubnets', []);
  };

  const handleSubnetToggle = (subnet: any) => {
    const isSelected = existingSubnets.some(s => s.subnetId === subnet.id);
    
    if (isSelected) {
      const updated = existingSubnets.filter(s => s.subnetId !== subnet.id);
      onUpdate('existingSubnets', updated);
    } else {
      const newSubnet: ExistingSubnetReference = {
        name: subnet.name,
        subnetId: subnet.id,
        addressPrefix: subnet.addressPrefix,
        purpose: SubnetPurpose.Application // Default
      };
      onUpdate('existingSubnets', [...existingSubnets, newSubnet]);
    }
  };

  const handleSubnetPurposeChange = (subnetId: string, purpose: SubnetPurpose) => {
    const updated = existingSubnets.map(s =>
      s.subnetId === subnetId ? { ...s, purpose } : s
    );
    onUpdate('existingSubnets', updated);
  };

  return (
    <div className="existing-network-selector">
      <h4>Select Existing Azure Network</h4>

      {error && (
        <div className="alert alert-error">
          ⚠️ {error}
        </div>
      )}

      {/* Subscription Selector */}
      <div className="form-group">
        <label>Azure Subscription *</label>
        <select
          value={selectedSubscription}
          onChange={(e) => setSelectedSubscription(e.target.value)}
          disabled={loading.subscriptions}
          required
        >
          <option value="">-- Select Subscription --</option>
          {subscriptions.map(sub => (
            <option key={sub.id} value={sub.id}>
              {sub.name} ({sub.state})
            </option>
          ))}
        </select>
        {loading.subscriptions && <span className="loading-spinner">Loading...</span>}
      </div>

      {/* Resource Group Selector */}
      <div className="form-group">
        <label>Resource Group *</label>
        <select
          value={selectedResourceGroup}
          onChange={(e) => setSelectedResourceGroup(e.target.value)}
          disabled={!selectedSubscription || loading.resourceGroups}
          required
        >
          <option value="">-- Select Resource Group --</option>
          {resourceGroups.map(rg => (
            <option key={rg.id} value={rg.name}>
              {rg.name} ({rg.location})
            </option>
          ))}
        </select>
        {loading.resourceGroups && <span className="loading-spinner">Loading...</span>}
      </div>

      {/* VNet Selector */}
      <div className="form-group">
        <label>Virtual Network *</label>
        <select
          value={selectedVNet?.name || ''}
          onChange={(e) => {
            const vnet = vnets.find(v => v.name === e.target.value);
            if (vnet) handleVNetSelect(vnet);
          }}
          disabled={!selectedResourceGroup || loading.vnets}
          required
        >
          <option value="">-- Select Virtual Network --</option>
          {vnets.map(vnet => (
            <option key={vnet.id} value={vnet.name}>
              {vnet.name} ({vnet.addressSpace.join(', ')})
            </option>
          ))}
        </select>
        {loading.vnets && <span className="loading-spinner">Loading...</span>}
      </div>

      {/* VNet Information Display */}
      {selectedVNet && (
        <div className="vnet-info-card">
          <h5>📊 Selected VNet Information</h5>
          <div className="info-grid">
            <div className="info-item">
              <strong>Name:</strong>
              <span>{selectedVNet.name}</span>
            </div>
            <div className="info-item">
              <strong>Location:</strong>
              <span>{selectedVNet.location}</span>
            </div>
            <div className="info-item">
              <strong>Address Space:</strong>
              <span>{selectedVNet.addressSpace.join(', ')}</span>
            </div>
            <div className="info-item">
              <strong>Subnets:</strong>
              <span>{selectedVNet.subnets.length} available</span>
            </div>
          </div>
        </div>
      )}

      {/* Subnet Selection */}
      {selectedVNet && selectedVNet.subnets.length > 0 && (
        <div className="subnet-selection">
          <h5>Select Subnets *</h5>
          <p className="section-description">
            Select one or more subnets and assign their purpose for template generation.
          </p>
          
          <div className="subnets-list">
            {selectedVNet.subnets.map(subnet => {
              const isSelected = existingSubnets.some(s => s.subnetId === subnet.id);
              const selectedSubnet = existingSubnets.find(s => s.subnetId === subnet.id);
              
              return (
                <div key={subnet.id} className={`subnet-item ${isSelected ? 'subnet-item-selected' : ''}`}>
                  <div className="subnet-item-header">
                    <label className="checkbox-label">
                      <input
                        type="checkbox"
                        checked={isSelected}
                        onChange={() => handleSubnetToggle(subnet)}
                      />
                      <strong>{subnet.name}</strong>
                    </label>
                  </div>
                  
                  <div className="subnet-item-details">
                    <span className="subnet-detail">
                      <strong>CIDR:</strong> {subnet.addressPrefix}
                    </span>
                    {subnet.delegation && (
                      <span className="subnet-detail">
                        <strong>Delegation:</strong> {subnet.delegation}
                      </span>
                    )}
                    {subnet.serviceEndpoints && subnet.serviceEndpoints.length > 0 && (
                      <span className="subnet-detail">
                        <strong>Service Endpoints:</strong> {subnet.serviceEndpoints.join(', ')}
                      </span>
                    )}
                  </div>
                  
                  {isSelected && selectedSubnet && (
                    <div className="subnet-purpose-selector">
                      <label>Purpose:</label>
                      <select
                        value={selectedSubnet.purpose}
                        onChange={(e) => handleSubnetPurposeChange(subnet.id, e.target.value as SubnetPurpose)}
                      >
                        <option value={SubnetPurpose.Application}>Application</option>
                        <option value={SubnetPurpose.PrivateEndpoints}>Private Endpoints</option>
                        <option value={SubnetPurpose.ApplicationGateway}>Application Gateway</option>
                        <option value={SubnetPurpose.Database}>Database</option>
                        <option value={SubnetPurpose.Other}>Other</option>
                      </select>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
          
          {existingSubnets.length === 0 && (
            <div className="alert alert-warning">
              ⚠️ Please select at least one subnet to proceed.
            </div>
          )}
          
          {existingSubnets.length > 0 && (
            <div className="selected-subnets-summary">
              <strong>✓ Selected:</strong> {existingSubnets.length} subnet(s)
            </div>
          )}
        </div>
      )}

      {selectedVNet && selectedVNet.subnets.length === 0 && (
        <div className="alert alert-error">
          ⚠️ The selected VNet has no subnets. Please create subnets in Azure or select a different VNet.
        </div>
      )}
    </div>
  );
};

export default ExistingNetworkSelector;
