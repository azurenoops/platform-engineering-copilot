import React, { useState, useEffect } from 'react';
import SubnetCard from './SubnetCard';
import ExistingNetworkSelector from './ExistingNetworkSelector';
import { 
  NetworkConfiguration, 
  NetworkMode, 
  SubnetConfiguration,
  SubnetPurpose,
  VNetPeering
} from '../services/adminApi';
import { 
  validateSubnets, 
  isValidCIDR,
  isValidVNetName,
  calculateNextSubnetCIDR
} from '../utils/networkValidation';
import './NetworkConfigurationForm.css';

interface NetworkConfigurationFormProps {
  value: NetworkConfiguration;
  onChange: (config: NetworkConfiguration) => void;
}

const NetworkConfigurationForm: React.FC<NetworkConfigurationFormProps> = ({
  value,
  onChange
}) => {
  const [networkMode, setNetworkMode] = useState<NetworkMode>(
    value.mode || NetworkMode.CreateNew
  );
  const [showModeChangeConfirmation, setShowModeChangeConfirmation] = useState(false);
  const [pendingMode, setPendingMode] = useState<NetworkMode | null>(null);
  const [validationErrors, setValidationErrors] = useState<{[key: number]: string}>({});
  const [globalValidationMessage, setGlobalValidationMessage] = useState<string>('');

  // Initialize with default subnet if none exist
  useEffect(() => {
    if (networkMode === NetworkMode.CreateNew && (!value.subnets || value.subnets.length === 0)) {
      const defaultSubnet: SubnetConfiguration = {
        name: 'subnet-app',
        addressPrefix: '10.0.1.0/24',
        purpose: SubnetPurpose.Application,
        enableServiceEndpoints: false,
        serviceEndpoints: []
      };
      handleUpdateField('subnets', [defaultSubnet]);
    }
  }, []);

  // Validate subnets whenever they change
  useEffect(() => {
    if (networkMode === NetworkMode.CreateNew && value.subnets && value.subnets.length > 0) {
      const validation = validateSubnets(value.subnets, value.addressSpace || '10.0.0.0/16');
      
      if (!validation.valid) {
        // For simple string array errors, show the first error as global message
        setGlobalValidationMessage(validation.errors[0] || '');
      } else {
        setValidationErrors({});
        setGlobalValidationMessage('');
      }
    }
  }, [value.subnets, value.addressSpace, networkMode]);

  const handleUpdateField = (field: string, fieldValue: any) => {
    onChange({
      ...value,
      [field]: fieldValue
    });
  };

  const handleModeChange = (newMode: NetworkMode) => {
    if (newMode === networkMode) return;

    // If there's existing data, ask for confirmation
    const hasCreateNewData = value.subnets && value.subnets.length > 0;
    const hasExistingData = value.existingSubnets && value.existingSubnets.length > 0;

    if ((newMode === NetworkMode.UseExisting && hasCreateNewData) ||
        (newMode === NetworkMode.CreateNew && hasExistingData)) {
      setPendingMode(newMode);
      setShowModeChangeConfirmation(true);
    } else {
      applyModeChange(newMode);
    }
  };

  const applyModeChange = (newMode: NetworkMode) => {
    setNetworkMode(newMode);
    handleUpdateField('mode', newMode);
    
    // Clear validation errors when switching modes
    setValidationErrors({});
    setGlobalValidationMessage('');
    
    setShowModeChangeConfirmation(false);
    setPendingMode(null);
  };

  const cancelModeChange = () => {
    setShowModeChangeConfirmation(false);
    setPendingMode(null);
  };

  // Create New Network Handlers
  const handleAddSubnet = () => {
    const currentSubnets = value.subnets || [];
    
    // Calculate next available CIDR
    const existingCidrs = currentSubnets.map(s => s.addressPrefix).filter(Boolean);
    const suggestedCidr = calculateNextSubnetCIDR(
      value.addressSpace || '10.0.0.0/16',
      existingCidrs,
      24
    );
    
    const newSubnet: SubnetConfiguration = {
      name: `subnet-${currentSubnets.length + 1}`,
      addressPrefix: suggestedCidr || '',
      purpose: SubnetPurpose.Application,
      enableServiceEndpoints: false,
      serviceEndpoints: []
    };
    
    handleUpdateField('subnets', [...currentSubnets, newSubnet]);
  };

  const handleRemoveSubnet = (index: number) => {
    const currentSubnets = value.subnets || [];
    const updated = currentSubnets.filter((_, i) => i !== index);
    handleUpdateField('subnets', updated);
    
    // Clear validation error for this subnet
    const { [index]: removed, ...remainingErrors } = validationErrors;
    setValidationErrors(remainingErrors);
  };

  const handleSubnetUpdate = (index: number, field: string, fieldValue: any) => {
    console.log(`handleSubnetUpdate called - index: ${index}, field: ${field}, value:`, fieldValue);
    const currentSubnets = value.subnets || [];
    const updated = currentSubnets.map((subnet, i) => {
      if (i === index) {
        // Explicitly handle boolean fields to ensure proper state update
        if (field === 'enableServiceEndpoints') {
          const newSubnet = { ...subnet, enableServiceEndpoints: Boolean(fieldValue) };
          console.log('Updated subnet with enableServiceEndpoints:', newSubnet);
          return newSubnet;
        }
        return { ...subnet, [field]: fieldValue };
      }
      return subnet;
    });
    console.log('Updated subnets array:', updated);
    handleUpdateField('subnets', updated);
  };

  // VNet Peering Handlers
  const handleAddVNetPeering = () => {
    const currentPeerings = value.vnetPeerings || [];
    const newPeering: VNetPeering = {
      name: `peering-${currentPeerings.length + 1}`,
      remoteVNetResourceId: '',
      allowVirtualNetworkAccess: true,
      allowForwardedTraffic: false,
      allowGatewayTransit: false,
      useRemoteGateways: false
    };
    handleUpdateField('vnetPeerings', [...currentPeerings, newPeering]);
  };

  const handleRemoveVNetPeering = (index: number) => {
    const currentPeerings = value.vnetPeerings || [];
    const updated = currentPeerings.filter((_, i) => i !== index);
    handleUpdateField('vnetPeerings', updated);
  };

  const handleVNetPeeringUpdate = (index: number, field: keyof VNetPeering, fieldValue: any) => {
    const currentPeerings = value.vnetPeerings || [];
    const updated = currentPeerings.map((peering, i) => 
      i === index ? { ...peering, [field]: fieldValue } : peering
    );
    handleUpdateField('vnetPeerings', updated);
  };

  return (
    <div className="network-configuration-form">
      <h3>🌐 Network Configuration</h3>
      
      {/* Network Mode Toggle */}
      <div className="network-mode-toggle">
        <h4>Network Mode</h4>
        <div className="radio-group">
          <label className={`radio-option ${networkMode === NetworkMode.CreateNew ? 'radio-option-selected' : ''}`}>
            <input
              type="radio"
              name="networkMode"
              value={NetworkMode.CreateNew}
              checked={networkMode === NetworkMode.CreateNew}
              onChange={() => handleModeChange(NetworkMode.CreateNew)}
            />
            <div className="radio-content">
              <strong>Create New Network</strong>
              <span className="radio-description">
                Generate templates to create a new Virtual Network with custom subnets
              </span>
            </div>
          </label>
          
          <label className={`radio-option ${networkMode === NetworkMode.UseExisting ? 'radio-option-selected' : ''}`}>
            <input
              type="radio"
              name="networkMode"
              value={NetworkMode.UseExisting}
              checked={networkMode === NetworkMode.UseExisting}
              onChange={() => handleModeChange(NetworkMode.UseExisting)}
            />
            <div className="radio-content">
              <strong>Use Existing Network</strong>
              <span className="radio-description">
                Reference existing Azure Virtual Network and subnets in your templates
              </span>
            </div>
          </label>
        </div>
      </div>

      {/* Mode Change Confirmation Modal */}
      {showModeChangeConfirmation && (
        <div className="modal-overlay" onClick={cancelModeChange}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <h4>⚠️ Confirm Mode Change</h4>
            <p>
              Switching network modes will clear your current configuration. Are you sure you want to continue?
            </p>
            <div className="modal-actions">
              <button 
                className="btn btn-secondary" 
                onClick={cancelModeChange}
              >
                Cancel
              </button>
              <button 
                className="btn btn-danger" 
                onClick={() => applyModeChange(pendingMode!)}
              >
                Yes, Switch Mode
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Global Validation Message */}
      {globalValidationMessage && (
        <div className="alert alert-error">
          ⚠️ {globalValidationMessage}
        </div>
      )}

      {/* Create New Network Section */}
      {networkMode === NetworkMode.CreateNew && (
        <div className="create-new-section">
          {/* VNet Configuration */}
          <div className="vnet-config-section">
            <h4>Virtual Network Configuration</h4>
            
            <div className="form-row">
              <div className="form-group">
                <label>VNet Name *</label>
                <input
                  type="text"
                  value={value.vnetName || ''}
                  onChange={(e) => handleUpdateField('vnetName', e.target.value)}
                  placeholder="e.g., vnet-prod"
                  required
                  className={value.vnetName && !isValidVNetName(value.vnetName) ? 'input-error' : ''}
                />
                {value.vnetName && !isValidVNetName(value.vnetName) && (
                  <span className="error-text">
                    VNet name must be 2-64 characters, alphanumeric, hyphens, underscores, and periods
                  </span>
                )}
              </div>
              
              <div className="form-group">
                <label>Address Space (CIDR) *</label>
                <input
                  type="text"
                  value={value.addressSpace || ''}
                  onChange={(e) => handleUpdateField('addressSpace', e.target.value)}
                  placeholder="e.g., 10.0.0.0/16"
                  required
                  className={value.addressSpace && !isValidCIDR(value.addressSpace) ? 'input-error' : ''}
                />
                {value.addressSpace && !isValidCIDR(value.addressSpace) && (
                  <span className="error-text">
                    Invalid CIDR format. Use format: x.x.x.x/y (e.g., 10.0.0.0/16)
                  </span>
                )}
              </div>
            </div>

            <div className="form-row">
              <div className="form-group">
                <label>Location *</label>
                <input
                  type="text"
                  value={value.location || ''}
                  onChange={(e) => handleUpdateField('location', e.target.value)}
                  placeholder="e.g., eastus"
                  required
                />
              </div>
              
              <div className="form-group">
                <label>Resource Group</label>
                <input
                  type="text"
                  value={value.resourceGroup || ''}
                  onChange={(e) => handleUpdateField('resourceGroup', e.target.value)}
                  placeholder="e.g., rg-networking"
                />
              </div>
            </div>
          </div>

          {/* VNet Peering/Links Section */}
          <div className="vnet-peering-section">
            <div className="subnets-header">
              <h4>🔗 VNet Peering (Optional)</h4>
              {value.enableVNetPeering && (
                <button 
                  type="button"
                  className="btn btn-primary btn-add-subnet"
                  onClick={handleAddVNetPeering}
                >
                  ➕ Add VNet Peering
                </button>
              )}
            </div>
            
            <div className="form-group">
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={value.enableVNetPeering || false}
                  onChange={(e) => handleUpdateField('enableVNetPeering', e.target.checked)}
                />
                Enable VNet Peering/Links
              </label>
              <span className="help-text">
                Connect this VNet to other VNets for cross-network communication
              </span>
            </div>

            {value.enableVNetPeering && (
              <>
                {(!value.vnetPeerings || value.vnetPeerings.length === 0) && (
                  <div className="alert alert-info">
                    ℹ️ Click "Add VNet Peering" to configure peering connections to other VNets
                  </div>
                )}

                {(value.vnetPeerings || []).map((peering, index) => (
                  <div key={index} className="vnet-peering-card">
                    <div className="subnet-card-header">
                      <h5>Peering {index + 1}</h5>
                      <button
                        type="button"
                        onClick={() => handleRemoveVNetPeering(index)}
                        className="btn-remove"
                        title="Remove peering"
                      >
                        🗑️
                      </button>
                    </div>

                    <div className="form-row">
                      <div className="form-group">
                        <label>Peering Name *</label>
                        <input
                          type="text"
                          value={peering.name}
                          onChange={(e) => handleVNetPeeringUpdate(index, 'name', e.target.value)}
                          placeholder="e.g., peer-to-hub-vnet"
                          required
                        />
                      </div>

                      <div className="form-group">
                        <label>Remote VNet Name (Optional)</label>
                        <input
                          type="text"
                          value={peering.remoteVNetName || ''}
                          onChange={(e) => handleVNetPeeringUpdate(index, 'remoteVNetName', e.target.value)}
                          placeholder="e.g., vnet-hub"
                        />
                        <span className="help-text">Display name for reference</span>
                      </div>
                    </div>

                    <div className="form-group">
                      <label>Remote VNet Resource ID *</label>
                      <input
                        type="text"
                        value={peering.remoteVNetResourceId}
                        onChange={(e) => handleVNetPeeringUpdate(index, 'remoteVNetResourceId', e.target.value)}
                        placeholder="/subscriptions/.../resourceGroups/.../providers/Microsoft.Network/virtualNetworks/..."
                        required
                      />
                      <span className="help-text">Full Azure Resource ID of the VNet to peer with</span>
                    </div>

                    <div className="peering-options">
                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          checked={peering.allowVirtualNetworkAccess !== false}
                          onChange={(e) => handleVNetPeeringUpdate(index, 'allowVirtualNetworkAccess', e.target.checked)}
                        />
                        🔓 Allow Virtual Network Access
                      </label>
                      <span className="help-text">Enable resources in either VNet to communicate with each other</span>

                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          checked={peering.allowForwardedTraffic || false}
                          onChange={(e) => handleVNetPeeringUpdate(index, 'allowForwardedTraffic', e.target.checked)}
                        />
                        ↔️ Allow Forwarded Traffic
                      </label>
                      <span className="help-text">Allow traffic forwarded from other networks</span>

                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          checked={peering.allowGatewayTransit || false}
                          onChange={(e) => handleVNetPeeringUpdate(index, 'allowGatewayTransit', e.target.checked)}
                        />
                        🚪 Allow Gateway Transit
                      </label>
                      <span className="help-text">Allow remote VNet to use this VNet's gateway</span>

                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          checked={peering.useRemoteGateways || false}
                          onChange={(e) => handleVNetPeeringUpdate(index, 'useRemoteGateways', e.target.checked)}
                        />
                        🔌 Use Remote Gateways
                      </label>
                      <span className="help-text">Use the remote VNet's gateway</span>
                    </div>
                  </div>
                ))}
              </>
            )}
          </div>

          {/* Subnets Section */}
          <div className="subnets-section">
            <div className="subnets-header">
              <h4>Subnets</h4>
              <button 
                type="button"
                className="btn btn-primary btn-add-subnet"
                onClick={handleAddSubnet}
              >
                ➕ Add Subnet
              </button>
            </div>
            
            <p className="section-description">
              Configure one or more subnets within your Virtual Network. Each subnet requires a unique name and CIDR range.
            </p>

            <div className="subnets-list">
              {(value.subnets || []).map((subnet, index) => (
                <SubnetCard
                  key={index}
                  subnet={subnet}
                  index={index}
                  onUpdate={handleSubnetUpdate}
                  onRemove={handleRemoveSubnet}
                  canRemove={(value.subnets?.length || 0) > 1}
                  vnetCidr={value.addressSpace}
                  validationError={validationErrors[index]}
                />
              ))}
            </div>

            {(!value.subnets || value.subnets.length === 0) && (
              <div className="alert alert-warning">
                ⚠️ At least one subnet is required. Click "Add Subnet" to get started.
              </div>
            )}
          </div>

          {/* DNS Configuration (Optional) */}
          <div className="dns-config-section">
            <h4>DNS Configuration (Optional)</h4>
            <div className="form-group">
              <label>Custom DNS Servers</label>
              <input
                type="text"
                value={value.dnsServers?.join(', ') || ''}
                onChange={(e) => {
                  const dnsArray = e.target.value
                    .split(',')
                    .map(s => s.trim())
                    .filter(s => s.length > 0);
                  handleUpdateField('dnsServers', dnsArray);
                }}
                placeholder="e.g., 8.8.8.8, 8.8.4.4 (comma-separated)"
              />
              <span className="help-text">
                Leave empty to use Azure default DNS
              </span>
            </div>
          </div>

          {/* Security Features (Optional) */}
          <div className="security-features-section">
            <h4>🔒 Security Features (Optional)</h4>
            
            {/* Network Security Group */}
            <div className="form-group">
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={value.enableNetworkSecurityGroup || false}
                  onChange={(e) => {
                    handleUpdateField('enableNetworkSecurityGroup', e.target.checked);
                    if (e.target.checked && !value.nsgMode) {
                      handleUpdateField('nsgMode', 'new');
                    }
                  }}
                />
                🛡️ Enable Network Security Group (NSG)
              </label>
              <span className="help-text">
                Create NSGs to control inbound and outbound network traffic to subnets
              </span>
            </div>

            {value.enableNetworkSecurityGroup && (
              <div className="form-group indent">
                <label>NSG Mode</label>
                <div className="radio-group-inline">
                  <label className="radio-label">
                    <input
                      type="radio"
                      name="nsgMode"
                      value="new"
                      checked={value.nsgMode === 'new' || !value.nsgMode}
                      onChange={(e) => handleUpdateField('nsgMode', 'new')}
                    />
                    Create New NSG
                  </label>
                  <label className="radio-label">
                    <input
                      type="radio"
                      name="nsgMode"
                      value="existing"
                      checked={value.nsgMode === 'existing'}
                      onChange={(e) => handleUpdateField('nsgMode', 'existing')}
                    />
                    Use Existing NSG
                  </label>
                </div>
                
                {(value.nsgMode === 'new' || !value.nsgMode) && (
                  <div className="form-group" style={{ marginTop: '12px' }}>
                    <label>NSG Name (Optional)</label>
                    <input
                      type="text"
                      value={value.nsgName || ''}
                      onChange={(e) => handleUpdateField('nsgName', e.target.value)}
                      placeholder="e.g., nsg-prod-vnet"
                    />
                    <span className="help-text">
                      Leave empty to auto-generate based on VNet name
                    </span>
                  </div>
                )}
                
                {value.nsgMode === 'existing' && (
                  <div className="form-group" style={{ marginTop: '12px' }}>
                    <label>Existing NSG Resource ID *</label>
                    <input
                      type="text"
                      value={value.existingNsgResourceId || ''}
                      onChange={(e) => handleUpdateField('existingNsgResourceId', e.target.value)}
                      placeholder="/subscriptions/.../resourceGroups/.../providers/Microsoft.Network/networkSecurityGroups/..."
                      required
                    />
                  </div>
                )}
              </div>
            )}

            {/* DDoS Protection */}
            <div className="form-group">
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={value.enableDdosProtection || false}
                  onChange={(e) => {
                    handleUpdateField('enableDdosProtection', e.target.checked);
                    if (e.target.checked && !value.ddosMode) {
                      handleUpdateField('ddosMode', 'new');
                    }
                  }}
                />
                🛡️ Enable DDoS Protection Standard
              </label>
              <span className="help-text">
                Azure DDoS Protection Standard provides enhanced DDoS mitigation features
              </span>
            </div>

            {value.enableDdosProtection && (
              <div className="form-group indent">
                <label>DDoS Protection Mode</label>
                <div className="radio-group-inline">
                  <label className="radio-label">
                    <input
                      type="radio"
                      name="ddosMode"
                      value="new"
                      checked={value.ddosMode === 'new' || !value.ddosMode}
                      onChange={(e) => handleUpdateField('ddosMode', 'new')}
                    />
                    Create New DDoS Plan
                  </label>
                  <label className="radio-label">
                    <input
                      type="radio"
                      name="ddosMode"
                      value="existing"
                      checked={value.ddosMode === 'existing'}
                      onChange={(e) => handleUpdateField('ddosMode', 'existing')}
                    />
                    Use Existing DDoS Plan
                  </label>
                </div>
                
                {value.ddosMode === 'existing' && (
                  <div className="form-group" style={{ marginTop: '12px' }}>
                    <label>DDoS Protection Plan ID *</label>
                    <input
                      type="text"
                      value={value.ddosProtectionPlanId || ''}
                      onChange={(e) => handleUpdateField('ddosProtectionPlanId', e.target.value)}
                      placeholder="/subscriptions/.../resourceGroups/.../providers/Microsoft.Network/ddosProtectionPlans/..."
                      required
                    />
                  </div>
                )}
              </div>
            )}

            {/* Private DNS */}
            <div className="form-group">
              <label className="checkbox-label">
                <input
                  type="checkbox"
                  checked={value.enablePrivateDns || false}
                  onChange={(e) => {
                    handleUpdateField('enablePrivateDns', e.target.checked);
                    if (e.target.checked && !value.privateDnsMode) {
                      handleUpdateField('privateDnsMode', 'new');
                    }
                  }}
                />
                🔐 Enable Private DNS Zone
              </label>
              <span className="help-text">
                Link a private DNS zone to the virtual network for name resolution
              </span>
            </div>

            {value.enablePrivateDns && (
              <div className="form-group indent">
                <label>Private DNS Mode</label>
                <div className="radio-group-inline">
                  <label className="radio-label">
                    <input
                      type="radio"
                      name="privateDnsMode"
                      value="new"
                      checked={value.privateDnsMode === 'new' || !value.privateDnsMode}
                      onChange={(e) => handleUpdateField('privateDnsMode', 'new')}
                    />
                    Create New DNS Zone
                  </label>
                  <label className="radio-label">
                    <input
                      type="radio"
                      name="privateDnsMode"
                      value="existing"
                      checked={value.privateDnsMode === 'existing'}
                      onChange={(e) => handleUpdateField('privateDnsMode', 'existing')}
                    />
                    Use Existing DNS Zone
                  </label>
                </div>
                
                {(value.privateDnsMode === 'new' || !value.privateDnsMode) && (
                  <div className="form-group" style={{ marginTop: '12px' }}>
                    <label>Private DNS Zone Name *</label>
                    <input
                      type="text"
                      value={value.privateDnsZoneName || ''}
                      onChange={(e) => handleUpdateField('privateDnsZoneName', e.target.value)}
                      placeholder="e.g., privatelink.azurewebsites.net"
                      required
                    />
                  </div>
                )}
                
                {value.privateDnsMode === 'existing' && (
                  <div className="form-group" style={{ marginTop: '12px' }}>
                    <label>Existing Private DNS Zone Resource ID *</label>
                    <input
                      type="text"
                      value={value.existingPrivateDnsZoneResourceId || ''}
                      onChange={(e) => handleUpdateField('existingPrivateDnsZoneResourceId', e.target.value)}
                      placeholder="/subscriptions/.../resourceGroups/.../providers/Microsoft.Network/privateDnsZones/..."
                      required
                    />
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      )}

      {/* Use Existing Network Section */}
      {networkMode === NetworkMode.UseExisting && (
        <div className="use-existing-section">
          <ExistingNetworkSelector
            existingVNetName={value.existingVNetName}
            existingVNetResourceGroup={value.existingVNetResourceGroup}
            existingSubnets={value.existingSubnets || []}
            onUpdate={handleUpdateField}
          />
        </div>
      )}

      {/* Configuration Summary */}
      <div className="config-summary">
        <h4>📋 Configuration Summary</h4>
        {networkMode === NetworkMode.CreateNew ? (
          <div className="summary-content">
            <div className="summary-item">
              <strong>Mode:</strong> Create New Network
            </div>
            <div className="summary-item">
              <strong>VNet Name:</strong> {value.vnetName || '(not set)'}
            </div>
            <div className="summary-item">
              <strong>Address Space:</strong> {value.addressSpace || '(not set)'}
            </div>
            <div className="summary-item">
              <strong>Subnets:</strong> {value.subnets?.length || 0} configured
            </div>
            {value.dnsServers && value.dnsServers.length > 0 && (
              <div className="summary-item">
                <strong>DNS Servers:</strong> {value.dnsServers.join(', ')}
              </div>
            )}
            {value.enableNetworkSecurityGroup && (
              <div className="summary-item">
                <strong>🛡️ NSG:</strong> {value.nsgMode === 'existing' ? 'Using Existing' : 'Creating New'} 
                {value.nsgMode === 'new' && value.nsgName && ` (${value.nsgName})`}
                {value.nsgMode === 'existing' && ' (from Resource ID)'}
              </div>
            )}
            {value.enableDdosProtection && (
              <div className="summary-item">
                <strong>🛡️ DDoS Protection:</strong> {value.ddosMode === 'existing' ? 'Using Existing Plan' : 'Creating New Plan'}
              </div>
            )}
            {value.enablePrivateDns && (
              <div className="summary-item">
                <strong>🔐 Private DNS:</strong> {value.privateDnsMode === 'existing' ? 'Using Existing Zone' : 'Creating New Zone'}
                {value.privateDnsMode === 'new' && value.privateDnsZoneName && ` (${value.privateDnsZoneName})`}
              </div>
            )}
            {value.enableVNetPeering && (
              <div className="summary-item">
                <strong>🔗 VNet Peering:</strong> {value.vnetPeerings?.length || 0} connection(s) configured
              </div>
            )}
          </div>
        ) : (
          <div className="summary-content">
            <div className="summary-item">
              <strong>Mode:</strong> Use Existing Network
            </div>
            <div className="summary-item">
              <strong>VNet Name:</strong> {value.existingVNetName || '(not selected)'}
            </div>
            <div className="summary-item">
              <strong>Resource Group:</strong> {value.existingVNetResourceGroup || '(not selected)'}
            </div>
            <div className="summary-item">
              <strong>Selected Subnets:</strong> {value.existingSubnets?.length || 0}
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default NetworkConfigurationForm;
