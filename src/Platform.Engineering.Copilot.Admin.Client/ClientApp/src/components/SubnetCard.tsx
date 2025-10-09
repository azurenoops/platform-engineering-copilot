import React from 'react';
import { SubnetConfiguration, SubnetPurpose } from '../services/adminApi';
import { isValidCIDR, isSubnetSizeSufficient, getSubnetSizeRecommendation } from '../utils/networkValidation';
import './SubnetCard.css';

interface SubnetCardProps {
  subnet: SubnetConfiguration;
  index: number;
  onUpdate: (index: number, field: keyof SubnetConfiguration, value: any) => void;
  onRemove: (index: number) => void;
  canRemove: boolean;
  vnetCidr?: string;
  validationError?: string;
}

const SubnetCard: React.FC<SubnetCardProps> = ({
  subnet,
  index,
  onUpdate,
  onRemove,
  canRemove,
  vnetCidr,
  validationError
}) => {
  const handleServiceEndpointToggle = (endpoint: string, checked: boolean) => {
    const currentEndpoints = subnet.serviceEndpoints || [];
    const updatedEndpoints = checked
      ? [...currentEndpoints, endpoint]
      : currentEndpoints.filter(e => e !== endpoint);
    onUpdate(index, 'serviceEndpoints', updatedEndpoints);
  };

  const serviceEndpointOptions = [
    { value: 'Microsoft.Storage', label: '💾 Azure Storage' },
    { value: 'Microsoft.Sql', label: '🗄️ Azure SQL' },
    { value: 'Microsoft.KeyVault', label: '🔑 Key Vault' },
    { value: 'Microsoft.ContainerRegistry', label: '📦 Container Registry' },
    { value: 'Microsoft.AzureCosmosDB', label: '🌐 Cosmos DB' },
    { value: 'Microsoft.ServiceBus', label: '📨 Service Bus' }
  ];

  const delegationOptions = [
    { value: '', label: 'None' },
    { value: 'Microsoft.Web/serverFarms', label: 'App Service' },
    { value: 'Microsoft.ContainerInstance/containerGroups', label: 'Container Instances' },
    { value: 'Microsoft.App/environments', label: 'Container Apps' }
  ];

  const isCidrValid = subnet.addressPrefix ? isValidCIDR(subnet.addressPrefix) : false;
  const isSizeSufficient = subnet.addressPrefix && subnet.purpose 
    ? isSubnetSizeSufficient(subnet.addressPrefix, subnet.purpose) 
    : true;

  return (
    <div className={`subnet-card ${validationError ? 'subnet-card-error' : ''}`}>
      <div className="subnet-card-header">
        <h4>Subnet {index + 1}</h4>
        <button
          type="button"
          onClick={() => onRemove(index)}
          disabled={!canRemove}
          className="remove-subnet-btn"
          title={canRemove ? 'Remove subnet' : 'At least one subnet is required'}
        >
          ✕ Remove
        </button>
      </div>

      {validationError && (
        <div className="subnet-validation-error">
          ⚠️ {validationError}
        </div>
      )}

      <div className="subnet-form-grid">
        <div className="form-group">
          <label>Subnet Name *</label>
          <input
            type="text"
            value={subnet.name}
            onChange={(e) => onUpdate(index, 'name', e.target.value)}
            placeholder="e.g., appservice-subnet"
            required
          />
          <small>Alphanumeric, hyphens, underscores, periods (1-80 chars)</small>
        </div>

        <div className="form-group">
          <label>CIDR *</label>
          <input
            type="text"
            value={subnet.addressPrefix}
            onChange={(e) => onUpdate(index, 'addressPrefix', e.target.value)}
            placeholder="e.g., 10.0.1.0/24"
            className={subnet.addressPrefix && !isCidrValid ? 'input-error' : ''}
            required
          />
          {subnet.addressPrefix && !isCidrValid && (
            <small className="error-text">Invalid CIDR format</small>
          )}
          {subnet.addressPrefix && isCidrValid && !isSizeSufficient && (
            <small className="warning-text">
              ⚠️ Subnet may be too small for {subnet.purpose}. {getSubnetSizeRecommendation(subnet.purpose)}
            </small>
          )}
          {vnetCidr && (
            <small>Must be within VNet address space: {vnetCidr}</small>
          )}
        </div>

        <div className="form-group">
          <label>Purpose *</label>
          <select
            value={subnet.purpose}
            onChange={(e) => onUpdate(index, 'purpose', e.target.value as SubnetPurpose)}
            required
          >
            <option value={SubnetPurpose.Application}>Application</option>
            <option value={SubnetPurpose.PrivateEndpoints}>Private Endpoints</option>
            <option value={SubnetPurpose.ApplicationGateway}>Application Gateway</option>
            <option value={SubnetPurpose.Database}>Database</option>
            <option value={SubnetPurpose.Other}>Other</option>
          </select>
          <small>Purpose helps generators allocate resources correctly</small>
        </div>

        <div className="form-group">
          <label>Delegation</label>
          <select
            value={subnet.delegation || ''}
            onChange={(e) => onUpdate(index, 'delegation', e.target.value || undefined)}
          >
            {delegationOptions.map(opt => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
          <small>Required for App Service, Container Instances, and Container Apps</small>
        </div>
      </div>

      <div className="subnet-service-endpoints">
        <label className="checkbox-label">
          <input
            type="checkbox"
            checked={Boolean(subnet.enableServiceEndpoints)}
            onChange={(e) => {
              e.stopPropagation(); // Prevent any event bubbling issues
              const newValue = e.target.checked;
              console.log(`Service Endpoints checkbox changed to: ${newValue} for subnet ${index}`);
              onUpdate(index, 'enableServiceEndpoints', newValue);
              if (!newValue) {
                onUpdate(index, 'serviceEndpoints', []);
              }
            }}
          />
          Enable Service Endpoints
        </label>

        {subnet.enableServiceEndpoints && (
          <div className="service-endpoints-grid">
            {serviceEndpointOptions.map((endpoint) => (
              <label key={endpoint.value} className="checkbox-label endpoint-option">
                <input
                  type="checkbox"
                  checked={subnet.serviceEndpoints?.includes(endpoint.value) || false}
                  onChange={(e) => handleServiceEndpointToggle(endpoint.value, e.target.checked)}
                />
                {endpoint.label}
              </label>
            ))}
          </div>
        )}

        {subnet.enableServiceEndpoints && subnet.serviceEndpoints && subnet.serviceEndpoints.length > 0 && (
          <div className="selected-endpoints-summary">
            <strong>Selected:</strong> {subnet.serviceEndpoints.length} endpoint(s)
          </div>
        )}
      </div>
    </div>
  );
};

export default SubnetCard;
