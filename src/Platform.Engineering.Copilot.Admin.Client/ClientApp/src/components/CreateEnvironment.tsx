import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import adminApi, { CreateEnvironmentRequest, Template } from '../services/adminApi';
import './CreateEnvironment.css';

interface AzureSubscription {
  id: string;
  name: string;
  tenantId: string;
  isDefault?: boolean;
}

interface SavedSettings {
  subscriptions: AzureSubscription[];
  preferences: {
    defaultRegion: string;
    defaultResourceGroup: string;
    defaultSku: string;
  };
}

const CreateEnvironment: React.FC = () => {
  const navigate = useNavigate();
  const [templates, setTemplates] = useState<Template[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [savedSubscriptions, setSavedSubscriptions] = useState<AzureSubscription[]>([]);
  const [savedSettings, setSavedSettings] = useState<SavedSettings | null>(null);

  const [formData, setFormData] = useState<CreateEnvironmentRequest>({
    environmentName: '',
    environmentType: '', // Will be set from selected template
    resourceGroup: '',
    location: 'eastus',
    subscriptionId: '',
    tags: {},
    templateId: '', // REQUIRED - must select a template
    // Note: Monitoring, logging, and other features come from the template
  });

  const [tagInput, setTagInput] = useState({ key: '', value: '' });
  const [selectedTemplate, setSelectedTemplate] = useState<Template | null>(null);
  
  // Template parameters state
  const [templateParameters, setTemplateParameters] = useState({
    // AKS Parameters
    // Security & Identity
    enablePrivateCluster: false,
    enableWorkloadIdentity: true,
    authorizedIPRanges: '',
    
    // Monitoring
    enableMonitoring: false,
    logAnalyticsWorkspaceId: '',
    enableAzurePolicy: true,
    
    // Cluster Configuration
    kubernetesVersion: '1.30',
    nodeCount: '3',
    nodeVmSize: 'Standard_D4s_v3',
    
    // Advanced Security
    enableImageCleaner: true,
    diskEncryptionSetId: '',
    
    // App Service Parameters
    // App Service Configuration
    appServicePlanSku: 'P1v3',
    runtimeStack: 'DOTNETCORE|8.0',
    alwaysOn: true,
    
    // App Service Security
    httpsOnly: true,
    enableVnetIntegration: false,
    vnetSubnetId: '',
    enablePrivateEndpoint: false,
    ftpsState: 'FtpsOnly',
    minTlsVersion: '1.2',
    enableManagedIdentity: true,
    
    // App Service Monitoring
    enableApplicationInsights: false,
    enableDetailedErrorMessages: false,
    enableHttpLogging: false,
    
    // App Service Advanced Security
    enableClientCertificate: false,
    clientCertMode: 'Optional',
    ipSecurityRestrictions: '',
    
    // Container Apps Parameters
    // Container Apps Configuration
    containerImage: 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest',
    containerPort: '80',
    minReplicas: '1',
    maxReplicas: '10',
    cpuCores: '0.5',
    memorySize: '1',
    
    // Container Apps Security
    allowInsecure: false,
    externalIngress: true,
    targetPort: '80',
    transport: 'auto',
    
    // Container Apps VNet Integration
    enableVnetIntegrationCA: false,
    vnetSubnetIdCA: '',
    infrastructureSubnetId: '',
    enablePrivateEndpointCA: false,
    
    // Container Apps DAPR
    enableDapr: true,
    daprAppId: '',
    daprAppPort: '80',
    daprEnableApiLogging: false,
    
    // Container Apps Advanced
    maxInactiveRevisions: '5',
    revisionMode: 'Single',
    enableIPRestrictions: false,
    allowedIPRanges: ''
  });

  useEffect(() => {
    loadTemplates();
    loadSavedSettings();
  }, []);

  const loadSavedSettings = () => {
    try {
      const settingsJson = localStorage.getItem('platformSettings');
      if (settingsJson) {
        const settings = JSON.parse(settingsJson);
        setSavedSettings(settings);
        setSavedSubscriptions(settings.subscriptions || []);
        
        console.log('📋 Loaded saved settings:', settings);
        
        // Auto-fill with default subscription and preferences
        const defaultSub = settings.subscriptions?.find((sub: AzureSubscription) => sub.isDefault);
        if (defaultSub) {
          console.log('✅ Auto-filling with default subscription:', defaultSub.name);
          setFormData(prev => ({
            ...prev,
            subscriptionId: defaultSub.id
          }));
        }
        
        // Apply default preferences
        if (settings.preferences) {
          console.log('⚙️ Applying default preferences:', settings.preferences);
          setFormData(prev => ({
            ...prev,
            location: settings.preferences.defaultRegion || prev.location,
            resourceGroup: settings.preferences.defaultResourceGroup 
              ? `${settings.preferences.defaultResourceGroup}-${prev.environmentName || 'env'}`
              : prev.resourceGroup
          }));
          
          // Apply default SKU to template parameters
          if (settings.preferences.defaultSku) {
            setTemplateParameters(prev => ({
              ...prev,
              nodeVmSize: settings.preferences.defaultSku
            }));
          }
        }
      }
    } catch (err) {
      console.error('Failed to load saved settings:', err);
    }
  };

  const loadTemplates = async () => {
    try {
      const templateList = await adminApi.listTemplates();
      console.log('Loaded templates:', templateList);
      
      // Filter to show only infrastructure templates
      // Infrastructure templates have templateType === 'infrastructure'
      const infrastructureTemplates = Array.isArray(templateList) 
        ? templateList.filter(t => t.templateType === 'infrastructure')
        : [];
      
      console.log('Filtered infrastructure templates:', infrastructureTemplates);
      setTemplates(infrastructureTemplates);
    } catch (err) {
      console.error('Failed to load templates:', err);
      setTemplates([]); // Set empty array on error
    }
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value, type } = e.target;
    const checked = (e.target as HTMLInputElement).checked;
    
    // If template is being selected, auto-populate environment type and resource group from template
    if (name === 'templateId' && value) {
      const template = templates.find(t => t.id === value);
      if (template) {
        setSelectedTemplate(template);
        
        // Try to parse template content to extract infrastructure configuration
        let suggestedResourceGroup = '';
        try {
          const templateContent = JSON.parse(template.content);
          console.log('Template content for RG extraction:', templateContent);
          
          // Check multiple possible locations for resource group information
          if (templateContent.infrastructure?.existingVNetResourceGroup) {
            suggestedResourceGroup = templateContent.infrastructure.existingVNetResourceGroup;
            console.log('Found RG from infrastructure.existingVNetResourceGroup:', suggestedResourceGroup);
          } else if (templateContent.infrastructure?.resourceGroup) {
            suggestedResourceGroup = templateContent.infrastructure.resourceGroup;
            console.log('Found RG from infrastructure.resourceGroup:', suggestedResourceGroup);
          } else if (templateContent.resourceGroup) {
            suggestedResourceGroup = templateContent.resourceGroup;
            console.log('Found RG from root resourceGroup:', suggestedResourceGroup);
          }
        } catch (err) {
          console.log('Could not parse template content for resource group:', err);
        }
        
        setFormData(prev => ({
          ...prev,
          templateId: value,
          environmentType: template.templateType, // Auto-set from template
          resourceGroup: suggestedResourceGroup || prev.resourceGroup // Pre-populate RG if available, allow override
        }));
        
        if (suggestedResourceGroup) {
          console.log('Resource Group pre-populated with:', suggestedResourceGroup);
        } else {
          console.log('No resource group found in template, field remains editable');
        }
        return;
      }
    }
    
    setFormData(prev => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value
    }));
  };

  const handleAddTag = () => {
    if (tagInput.key && tagInput.value) {
      setFormData(prev => ({
        ...prev,
        tags: {
          ...prev.tags,
          [tagInput.key]: tagInput.value
        }
      }));
      setTagInput({ key: '', value: '' });
    }
  };

  const handleRemoveTag = (key: string) => {
    setFormData(prev => {
      const newTags = { ...prev.tags };
      delete newTags[key];
      return { ...prev, tags: newTags };
    });
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    // Validate required fields
    if (!formData.templateId) {
      setError('Please select a service template');
      return;
    }
    
    if (!formData.environmentName) {
      setError('Please enter an environment name');
      return;
    }
    
    if (!formData.resourceGroup) {
      setError('Please enter a resource group name');
      return;
    }
    
    // Validate Log Analytics workspace ID if monitoring is enabled
    if (templateParameters.enableMonitoring && !templateParameters.logAnalyticsWorkspaceId) {
      setError('Log Analytics Workspace ID is required when monitoring is enabled');
      return;
    }
    
    setLoading(true);
    setError(null);

    try {
      // Parse authorized IP ranges from comma-separated string
      const ipRangesArray = templateParameters.authorizedIPRanges
        .split(',')
        .map(ip => ip.trim())
        .filter(ip => ip.length > 0);
      
      // Parse IP security restrictions JSON
      let ipRestrictions = [];
      try {
        if (templateParameters.ipSecurityRestrictions && templateParameters.ipSecurityRestrictions.trim()) {
          ipRestrictions = JSON.parse(templateParameters.ipSecurityRestrictions);
        }
      } catch (parseError) {
        console.warn('Failed to parse IP security restrictions, using empty array:', parseError);
      }
      
      // Parse Container Apps allowed IP ranges JSON
      let caAllowedIPRanges = [];
      try {
        if (templateParameters.allowedIPRanges && templateParameters.allowedIPRanges.trim()) {
          caAllowedIPRanges = JSON.parse(templateParameters.allowedIPRanges);
        }
      } catch (parseError) {
        console.warn('Failed to parse Container Apps IP ranges, using empty array:', parseError);
      }
      
      // Build request with template parameters
      const requestData = {
        ...formData,
        parameters: {
          // === AKS Parameters ===
          // Security & Identity
          enablePrivateCluster: templateParameters.enablePrivateCluster.toString(),
          enableWorkloadIdentity: templateParameters.enableWorkloadIdentity.toString(),
          authorizedIPRanges: ipRangesArray.length > 0 ? JSON.stringify(ipRangesArray) : '[]',
          
          // Monitoring parameters
          enableMonitoring: templateParameters.enableMonitoring.toString(),
          enableAzurePolicy: templateParameters.enableAzurePolicy.toString(),
          
          // AKS cluster parameters
          kubernetesVersion: templateParameters.kubernetesVersion,
          nodeCount: templateParameters.nodeCount,
          nodeVmSize: templateParameters.nodeVmSize,
          
          // Advanced Security
          enableImageCleaner: templateParameters.enableImageCleaner.toString(),
          diskEncryptionSetId: templateParameters.diskEncryptionSetId,
          
          // Conditionally include Log Analytics workspace ID
          ...(templateParameters.enableMonitoring && {
            logAnalyticsWorkspaceId: templateParameters.logAnalyticsWorkspaceId
          }),
          
          // === App Service Parameters ===
          // App Service Configuration
          appServicePlanSku: templateParameters.appServicePlanSku,
          runtimeStack: templateParameters.runtimeStack,
          alwaysOn: templateParameters.alwaysOn.toString(),
          
          // App Service Security
          httpsOnly: templateParameters.httpsOnly.toString(),
          enableVnetIntegration: templateParameters.enableVnetIntegration.toString(),
          vnetSubnetId: templateParameters.vnetSubnetId,
          enablePrivateEndpoint: templateParameters.enablePrivateEndpoint.toString(),
          ftpsState: templateParameters.ftpsState,
          minTlsVersion: templateParameters.minTlsVersion,
          enableManagedIdentity: templateParameters.enableManagedIdentity.toString(),
          
          // App Service Monitoring
          enableApplicationInsights: templateParameters.enableApplicationInsights.toString(),
          enableDetailedErrorMessages: templateParameters.enableDetailedErrorMessages.toString(),
          enableHttpLogging: templateParameters.enableHttpLogging.toString(),
          
          // App Service Advanced Security
          enableClientCertificate: templateParameters.enableClientCertificate.toString(),
          clientCertMode: templateParameters.clientCertMode,
          ipSecurityRestrictions: JSON.stringify(ipRestrictions),
          
          // === Container Apps Parameters ===
          // Container Configuration
          containerImage: templateParameters.containerImage,
          containerPort: templateParameters.containerPort,
          minReplicas: templateParameters.minReplicas,
          maxReplicas: templateParameters.maxReplicas,
          cpuCores: templateParameters.cpuCores,
          memorySize: templateParameters.memorySize,
          
          // Container Apps Security
          allowInsecure: templateParameters.allowInsecure.toString(),
          externalIngress: templateParameters.externalIngress.toString(),
          targetPort: templateParameters.targetPort,
          transport: templateParameters.transport,
          
          // Container Apps VNet Integration
          enableVnetIntegrationCA: templateParameters.enableVnetIntegrationCA.toString(),
          vnetSubnetIdCA: templateParameters.vnetSubnetIdCA,
          infrastructureSubnetId: templateParameters.infrastructureSubnetId,
          enablePrivateEndpointCA: templateParameters.enablePrivateEndpointCA.toString(),
          
          // Container Apps DAPR
          enableDapr: templateParameters.enableDapr.toString(),
          daprAppId: templateParameters.daprAppId,
          daprAppPort: templateParameters.daprAppPort,
          daprEnableApiLogging: templateParameters.daprEnableApiLogging.toString(),
          
          // Container Apps Advanced
          maxInactiveRevisions: templateParameters.maxInactiveRevisions,
          revisionMode: templateParameters.revisionMode,
          enableIPRestrictions: templateParameters.enableIPRestrictions.toString(),
          allowedIPRanges: JSON.stringify(caAllowedIPRanges)
        }
      };
      
      const result = await adminApi.createEnvironment(requestData);
      
      if (result.success) {
        // API now returns immediately with deployment ID
        // Redirect to environment list where deployment progress will be polled
        console.log(`Environment ${result.environmentName} deployment started with ID: ${result.deploymentId}`);
        
        // Show success message and redirect
        alert(`Environment "${result.environmentName}" deployment started. Redirecting to environment list...`);
        navigate('/environments');
      } else {
        setError(result.errorMessage || 'Failed to create environment');
        setLoading(false);
      }
    } catch (err: any) {
      console.error('Failed to create environment:', err);
      setError(err.response?.data?.error || err.message || 'Failed to create environment');
      setLoading(false);
    }
  };

  return (
    <div className="create-environment">
      <div className="page-header">
        <div>
          <h2>🚀 Create New Environment</h2>
          <p className="page-subtitle">Deploy a new environment from a template</p>
        </div>
        <button onClick={() => navigate('/environments')} className="btn btn-secondary">
          ← Back to Environments
        </button>
      </div>

      {error && (
        <div className="error-banner">
          <strong>⚠️ Error:</strong> {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="environment-form">
        <div className="form-section">
          <h3>Basic Information</h3>
          
          <div className="form-group">
            <label htmlFor="environmentName">
              Environment Name <span className="required">*</span>
            </label>
            <input
              type="text"
              id="environmentName"
              name="environmentName"
              value={formData.environmentName}
              onChange={handleInputChange}
              required
              placeholder="e.g., my-app-prod"
              className="form-control"
            />
            <small className="form-help">Must be unique and DNS-compatible</small>
          </div>

          <div className="form-row">
            <div className="form-group">
              <label htmlFor="location">
                Location <span className="required">*</span>
                {savedSettings?.preferences?.defaultRegion && 
                 formData.location === savedSettings.preferences.defaultRegion && (
                  <span style={{color: '#28a745', marginLeft: '8px', fontSize: '0.9em'}}>
                    ✓ Default
                  </span>
                )}
              </label>
              <select
                id="location"
                name="location"
                value={formData.location}
                onChange={handleInputChange}
                required
                className="form-control"
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
                <optgroup label="Europe">
                  <option value="northeurope">North Europe</option>
                  <option value="westeurope">West Europe</option>
                </optgroup>
              </select>
              {savedSettings?.preferences?.defaultRegion && 
               formData.location === savedSettings.preferences.defaultRegion && (
                <small className="form-help" style={{color: '#28a745'}}>
                  💾 Using default region from settings
                </small>
              )}
            </div>
          </div>

          <div className="form-group">
            <label htmlFor="resourceGroup">
              Resource Group <span className="required">*</span>
            </label>
            <input
              type="text"
              id="resourceGroup"
              name="resourceGroup"
              value={formData.resourceGroup}
              onChange={handleInputChange}
              required
              placeholder="e.g., rg-myapp-prod"
              className="form-control"
            />
            {selectedTemplate && formData.resourceGroup && (
              <small className="form-help" style={{color: '#0066cc'}}>
                💡 Pre-populated from template. You can override this value.
              </small>
            )}
          </div>

          <div className="form-group">
            <label htmlFor="subscriptionId">
              Azure Subscription {savedSubscriptions.length > 0 && <span style={{color: '#28a745'}}>✓</span>}
            </label>
            {savedSubscriptions.length > 0 ? (
              <>
                <select
                  id="subscriptionId"
                  name="subscriptionId"
                  value={formData.subscriptionId}
                  onChange={handleInputChange}
                  className="form-control"
                >
                  <option value="">-- Select a Subscription --</option>
                  {savedSubscriptions.map((sub) => (
                    <option key={sub.id} value={sub.id}>
                      {sub.name} {sub.isDefault ? '(Default)' : ''}
                    </option>
                  ))}
                </select>
                <small className="form-help" style={{color: '#28a745'}}>
                  💾 Using saved subscriptions from settings
                </small>
              </>
            ) : (
              <>
                <input
                  type="text"
                  id="subscriptionId"
                  name="subscriptionId"
                  value={formData.subscriptionId}
                  onChange={handleInputChange}
                  placeholder="Optional - uses default subscription"
                  className="form-control"
                />
                <small className="form-help" style={{color: '#ff9800'}}>
                  💡 Tip: Save subscriptions in Settings (⚙️) for quick access
                </small>
              </>
            )}
          </div>
        </div>

        <div className="form-section">
          <h3>Service Template <span className="required">*</span></h3>
          
          <div className="form-group">
            <label htmlFor="templateId">Select Service Template</label>
            <select
              id="templateId"
              name="templateId"
              value={formData.templateId}
              onChange={handleInputChange}
              required
              className="form-control"
            >
              <option value="">-- Select a Template --</option>
              {Array.isArray(templates) && templates.map(template => (
                <option key={template.id} value={template.id}>
                  {template.name} ({template.templateType})
                </option>
              ))}
            </select>
            <small className="form-help">
              Service templates define the infrastructure and configuration for your environment
            </small>
          </div>
          
          {selectedTemplate && (
            <div className="template-info">
              <h4>Selected Template Details</h4>
              <div className="info-grid">
                <div className="info-item">
                  <span className="label">Service Type:</span>
                  <span className="value">{selectedTemplate.templateType}</span>
                </div>
                <div className="info-item">
                  <span className="label">Azure Service:</span>
                  <span className="value">{selectedTemplate.azureService || 'N/A'}</span>
                </div>
                <div className="info-item">
                  <span className="label">Version:</span>
                  <span className="value">{selectedTemplate.version}</span>
                </div>
                <div className="info-item">
                  <span className="label">Format:</span>
                  <span className="value">{selectedTemplate.format}</span>
                </div>
                <div className="info-item">
                  <span className="label">Deployment Tier:</span>
                  <span className="value">{selectedTemplate.deploymentTier || 'Standard'}</span>
                </div>
              </div>
              
              {/* Template Features */}
              <div className="features-section">
                <h5>Template Features</h5>
                <div className="features-grid">
                  <div className={`feature-badge ${selectedTemplate.autoScalingEnabled ? 'enabled' : 'disabled'}`}>
                    <span className="feature-icon">{selectedTemplate.autoScalingEnabled ? '✓' : '✗'}</span>
                    <span className="feature-label">Auto-Scaling</span>
                  </div>
                  <div className={`feature-badge ${selectedTemplate.monitoringEnabled ? 'enabled' : 'disabled'}`}>
                    <span className="feature-icon">{selectedTemplate.monitoringEnabled ? '✓' : '✗'}</span>
                    <span className="feature-label">Monitoring</span>
                  </div>
                  <div className={`feature-badge ${selectedTemplate.backupEnabled ? 'enabled' : 'disabled'}`}>
                    <span className="feature-icon">{selectedTemplate.backupEnabled ? '✓' : '✗'}</span>
                    <span className="feature-label">Backup</span>
                  </div>
                  <div className={`feature-badge ${selectedTemplate.highAvailabilitySupported ? 'enabled' : 'disabled'}`}>
                    <span className="feature-icon">{selectedTemplate.highAvailabilitySupported ? '✓' : '✗'}</span>
                    <span className="feature-label">High Availability</span>
                  </div>
                  <div className={`feature-badge ${selectedTemplate.disasterRecoverySupported ? 'enabled' : 'disabled'}`}>
                    <span className="feature-icon">{selectedTemplate.disasterRecoverySupported ? '✓' : '✗'}</span>
                    <span className="feature-label">Disaster Recovery</span>
                  </div>
                  <div className={`feature-badge ${selectedTemplate.multiRegionSupported ? 'enabled' : 'disabled'}`}>
                    <span className="feature-icon">{selectedTemplate.multiRegionSupported ? '✓' : '✗'}</span>
                    <span className="feature-label">Multi-Region</span>
                  </div>
                </div>
              </div>
              
              {selectedTemplate.description && (
                <p className="template-description">{selectedTemplate.description}</p>
              )}
            </div>
          )}
        </div>

        {/* Template Parameters Section */}
        {selectedTemplate && selectedTemplate.name === 'dev-aks' && (
          <div className="form-section">
            <h3>Template Parameters <span className="optional-label">(Optional - Override Defaults)</span></h3>
            <p className="section-description">Configure Zero Trust security and cluster settings</p>
            
            {/* Security & Identity Section */}
            <div className="parameter-group">
              <h4>🔒 Security & Identity</h4>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enablePrivateCluster}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enablePrivateCluster: e.target.checked
                    })}
                  />
                  <span>Enable Private Cluster (Recommended for Production)</span>
                </label>
                <p className="field-help">
                  🔐 Restricts Kubernetes API server access to VNet only. Provides highest security by eliminating public endpoints.
                </p>
              </div>
              
              {!templateParameters.enablePrivateCluster && (
                <div className="form-group">
                  <label>Authorized IP Ranges (CIDR notation, comma-separated)</label>
                  <input
                    type="text"
                    className="form-control"
                    value={templateParameters.authorizedIPRanges}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      authorizedIPRanges: e.target.value
                    })}
                    placeholder="203.0.113.0/24, 198.51.100.0/24"
                  />
                  <p className="field-help">
                    🌐 IP ranges allowed to access the Kubernetes API server. Leave empty for unrestricted access (not recommended).
                  </p>
                </div>
              )}
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enableWorkloadIdentity}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enableWorkloadIdentity: e.target.checked
                    })}
                  />
                  <span>Enable Workload Identity (OIDC for Pods)</span>
                </label>
                <p className="field-help">
                  🎫 Enables passwordless authentication for pods using Azure AD. Pods can access Azure resources without managing secrets.
                </p>
              </div>
            </div>
            
            {/* Monitoring Section */}
            <div className="parameter-group">
              <h4>📊 Monitoring & Compliance</h4>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enableMonitoring}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enableMonitoring: e.target.checked
                    })}
                  />
                  <span>Enable Azure Monitor & Log Analytics</span>
                </label>
                <p className="field-help">Enables Container Insights, metrics collection, and log aggregation</p>
              </div>
              
              {templateParameters.enableMonitoring && (
                <div className="form-group">
                  <label>
                    Log Analytics Workspace Resource ID <span className="required">*</span>
                  </label>
                  <input
                    type="text"
                    className="form-control"
                    value={templateParameters.logAnalyticsWorkspaceId}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      logAnalyticsWorkspaceId: e.target.value
                    })}
                    placeholder="/subscriptions/{subscription-id}/resourceGroups/{rg-name}/providers/Microsoft.OperationalInsights/workspaces/{workspace-name}"
                    required={templateParameters.enableMonitoring}
                  />
                  <p className="field-help">Full Azure resource ID of existing Log Analytics workspace. Leave empty to create new workspace automatically.</p>
                </div>
              )}
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enableAzurePolicy}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enableAzurePolicy: e.target.checked
                    })}
                  />
                  <span>Enable Azure Policy for AKS</span>
                </label>
                <p className="field-help">
                  ✅ Enforces 7 critical security policies: allowed images (ACR/MCR), no privileged containers, HTTPS ingress, internal load balancers, AppArmor profiles, resource limits, read-only root filesystem
                </p>
              </div>
            </div>
            
            {/* Cluster Configuration Section */}
            <div className="parameter-group">
              <h4>⚙️ Cluster Configuration</h4>
              
              <div className="form-row">
                <div className="form-group">
                  <label>Kubernetes Version</label>
                  <select
                    className="form-control"
                    value={templateParameters.kubernetesVersion}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      kubernetesVersion: e.target.value
                    })}
                  >
                    <option value="1.30">1.30 (Standard Tier Compatible - Recommended)</option>
                    <option value="1.31">1.31 (Latest)</option>
                    <option value="1.29">1.29 (Premium Tier Required)</option>
                  </select>
                  <p className="field-help">Version 1.30+ works with Standard tier in Azure Government</p>
                </div>
                
                <div className="form-group">
                  <label>Node Count</label>
                  <input
                    type="number"
                    className="form-control"
                    min="1"
                    max="100"
                    value={templateParameters.nodeCount}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      nodeCount: e.target.value
                    })}
                  />
                  <p className="field-help">Number of nodes in the default node pool (min 3 for HA)</p>
                </div>
              </div>
              
              <div className="form-group">
                <label>Node VM Size</label>
                <select
                  className="form-control"
                  value={templateParameters.nodeVmSize}
                  onChange={(e) => setTemplateParameters({
                    ...templateParameters,
                    nodeVmSize: e.target.value
                  })}
                >
                  <option value="Standard_D2s_v3">Standard_D2s_v3 (2 vCPU, 8 GB RAM)</option>
                  <option value="Standard_D4s_v3">Standard_D4s_v3 (4 vCPU, 16 GB RAM) - Recommended</option>
                  <option value="Standard_D8s_v3">Standard_D8s_v3 (8 vCPU, 32 GB RAM)</option>
                  <option value="Standard_D16s_v3">Standard_D16s_v3 (16 vCPU, 64 GB RAM)</option>
                </select>
                <p className="field-help">VM size for cluster nodes (affects cost and performance)</p>
              </div>
            </div>
            
            {/* Advanced Security Section */}
            <div className="parameter-group">
              <h4>🛡️ Advanced Security</h4>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enableImageCleaner}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enableImageCleaner: e.target.checked
                    })}
                  />
                  <span>Enable Image Cleaner (Weekly Vulnerability Scanning)</span>
                </label>
                <p className="field-help">
                  🔍 Automatically scans and removes vulnerable container images on a weekly schedule. Enhances security posture.
                </p>
              </div>
              
              <div className="form-group">
                <label>Disk Encryption Set ID (Optional - Customer-Managed Keys)</label>
                <input
                  type="text"
                  className="form-control"
                  value={templateParameters.diskEncryptionSetId}
                  onChange={(e) => setTemplateParameters({
                    ...templateParameters,
                    diskEncryptionSetId: e.target.value
                  })}
                  placeholder="/subscriptions/{subscription-id}/resourceGroups/{rg-name}/providers/Microsoft.Compute/diskEncryptionSets/{des-name}"
                />
                <p className="field-help">
                  🔐 Azure Disk Encryption Set resource ID for customer-managed key encryption (CMK). Leave empty for platform-managed encryption. Required for compliance certifications (FedRAMP, HIPAA).
                </p>
              </div>
            </div>
            
            <div className="info-banner">
              <strong>ℹ️ Zero Trust Configuration:</strong> This template implements comprehensive Zero Trust security including private API server, Microsoft Defender for Containers, pod security admission, network policies (default-deny), and 7 Azure Policy assignments. All security features are configurable above.
            </div>
          </div>
        )}

        {/* App Service Template Parameters Section */}
        {selectedTemplate && (selectedTemplate.name === 'app-service' || selectedTemplate.name === 'dev-app-service') && (
          <div className="form-section">
            <h3>Template Parameters <span className="optional-label">(Optional - Override Defaults)</span></h3>
            <p className="section-description">Configure Zero Trust security and App Service settings</p>
            
            {/* App Service Configuration Section */}
            <div className="parameter-group">
              <h4>⚙️ App Service Configuration</h4>
              
              <div className="form-group">
                <label>App Service Plan SKU</label>
                <select
                  className="form-control"
                  value={templateParameters.appServicePlanSku}
                  onChange={(e) => setTemplateParameters({
                    ...templateParameters,
                    appServicePlanSku: e.target.value
                  })}
                >
                  <option value="B1">B1 - Basic (1 Core, 1.75 GB RAM)</option>
                  <option value="B2">B2 - Basic (2 Core, 3.5 GB RAM)</option>
                  <option value="S1">S1 - Standard (1 Core, 1.75 GB RAM)</option>
                  <option value="P1v3">P1v3 - Premium V3 (2 Core, 8 GB RAM) - Recommended</option>
                  <option value="P2v3">P2v3 - Premium V3 (4 Core, 16 GB RAM)</option>
                  <option value="P3v3">P3v3 - Premium V3 (8 Core, 32 GB RAM)</option>
                </select>
                <p className="field-help">Premium V3 required for VNet integration and private endpoints</p>
              </div>
              
              <div className="form-group">
                <label>Runtime Stack</label>
                <select
                  className="form-control"
                  value={templateParameters.runtimeStack}
                  onChange={(e) => setTemplateParameters({
                    ...templateParameters,
                    runtimeStack: e.target.value
                  })}
                >
                  <option value="DOTNETCORE|8.0">.NET 8.0</option>
                  <option value="DOTNETCORE|7.0">.NET 7.0</option>
                  <option value="NODE|20-lts">Node.js 20 LTS</option>
                  <option value="NODE|18-lts">Node.js 18 LTS</option>
                  <option value="PYTHON|3.11">Python 3.11</option>
                  <option value="PYTHON|3.10">Python 3.10</option>
                  <option value="JAVA|17-java17">Java 17</option>
                  <option value="JAVA|11-java11">Java 11</option>
                </select>
                <p className="field-help">Application runtime environment</p>
              </div>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.alwaysOn}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      alwaysOn: e.target.checked
                    })}
                  />
                  <span>Always On (Prevents App Unloading)</span>
                </label>
                <p className="field-help">Keep app loaded at all times (recommended for production, requires Basic tier or higher)</p>
              </div>
            </div>
            
            {/* Security Configuration Section */}
            <div className="parameter-group">
              <h4>🔒 Security Configuration</h4>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.httpsOnly}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      httpsOnly: e.target.checked
                    })}
                  />
                  <span>HTTPS Only (Recommended)</span>
                </label>
                <p className="field-help">🔐 Redirect all HTTP traffic to HTTPS</p>
              </div>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enableVnetIntegration}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enableVnetIntegration: e.target.checked
                    })}
                  />
                  <span>Enable VNet Integration (Premium Required)</span>
                </label>
                <p className="field-help">🌐 Deploy into Virtual Network defined in template's network configuration (configurable during template creation)</p>
              </div>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enablePrivateEndpoint}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enablePrivateEndpoint: e.target.checked
                    })}
                  />
                  <span>Enable Private Endpoint (Premium Required)</span>
                </label>
                <p className="field-help">🔒 Make app accessible only via private IP in VNet (Zero Trust ingress)</p>
              </div>
              
              <div className="form-group">
                <label>FTP State</label>
                <select
                  className="form-control"
                  value={templateParameters.ftpsState}
                  onChange={(e) => setTemplateParameters({
                    ...templateParameters,
                    ftpsState: e.target.value
                  })}
                >
                  <option value="Disabled">Disabled (Most Secure)</option>
                  <option value="FtpsOnly">FTPS Only (Recommended)</option>
                  <option value="AllAllowed">All Allowed (Not Recommended)</option>
                </select>
                <p className="field-help">FTP/FTPS deployment access control</p>
              </div>
              
              <div className="form-group">
                <label>Minimum TLS Version</label>
                <select
                  className="form-control"
                  value={templateParameters.minTlsVersion}
                  onChange={(e) => setTemplateParameters({
                    ...templateParameters,
                    minTlsVersion: e.target.value
                  })}
                >
                  <option value="1.3">TLS 1.3 (Most Secure)</option>
                  <option value="1.2">TLS 1.2 (Recommended)</option>
                  <option value="1.1">TLS 1.1 (Legacy)</option>
                  <option value="1.0">TLS 1.0 (Not Recommended)</option>
                </select>
                <p className="field-help">Minimum TLS version for HTTPS connections</p>
              </div>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enableManagedIdentity}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enableManagedIdentity: e.target.checked
                    })}
                  />
                  <span>Enable Managed Identity (Recommended)</span>
                </label>
                <p className="field-help">🎫 System-assigned managed identity for passwordless Azure service access</p>
              </div>
            </div>
            
            {/* Monitoring Section */}
            <div className="parameter-group">
              <h4>📊 Monitoring & Diagnostics</h4>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enableApplicationInsights}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enableApplicationInsights: e.target.checked
                    })}
                  />
                  <span>Enable Application Insights</span>
                </label>
                <p className="field-help">📈 Application performance monitoring and analytics</p>
              </div>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enableHttpLogging}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enableHttpLogging: e.target.checked
                    })}
                  />
                  <span>Enable HTTP Logging</span>
                </label>
                <p className="field-help">📝 Log HTTP requests and responses</p>
              </div>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enableDetailedErrorMessages}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enableDetailedErrorMessages: e.target.checked
                    })}
                  />
                  <span>Enable Detailed Error Messages</span>
                </label>
                <p className="field-help">🔍 Capture detailed error information (disable in production)</p>
              </div>
            </div>
            
            {/* Advanced Security Section */}
            <div className="parameter-group">
              <h4>🛡️ Advanced Security</h4>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enableClientCertificate}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enableClientCertificate: e.target.checked
                    })}
                  />
                  <span>Require Client Certificates (Mutual TLS)</span>
                </label>
                <p className="field-help">🔐 Enforce client certificate authentication</p>
              </div>
              
              {templateParameters.enableClientCertificate && (
                <div className="form-group">
                  <label>Client Certificate Mode</label>
                  <select
                    className="form-control"
                    value={templateParameters.clientCertMode}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      clientCertMode: e.target.value
                    })}
                  >
                    <option value="Required">Required (Enforce mTLS)</option>
                    <option value="Optional">Optional (Allow but not require)</option>
                    <option value="OptionalInteractiveUser">Optional Interactive User</option>
                  </select>
                  <p className="field-help">Client certificate validation mode</p>
                </div>
              )}
              
              <div className="form-group">
                <label>IP Security Restrictions (JSON Array)</label>
                <textarea
                  className="form-control"
                  rows={4}
                  value={templateParameters.ipSecurityRestrictions}
                  onChange={(e) => setTemplateParameters({
                    ...templateParameters,
                    ipSecurityRestrictions: e.target.value
                  })}
                  placeholder='[{"ipAddress": "203.0.113.0/24", "action": "Allow", "priority": 100, "name": "AllowOfficeIP"}]'
                />
                <p className="field-help">
                  🌐 IP-based access restrictions. Format: Array of objects with ipAddress, action, priority, name
                </p>
              </div>
            </div>
            
            <div className="info-banner">
              <strong>ℹ️ Zero Trust Configuration:</strong> This template implements App Service security best practices including HTTPS enforcement, VNet integration, private endpoints, TLS 1.2+, managed identity, and IP restrictions. Premium SKU required for advanced networking features.
            </div>
          </div>
        )}

        {/* Container Apps Template Parameters Section */}
        {selectedTemplate && (selectedTemplate.name === 'container-apps' || selectedTemplate.name === 'dev-container-apps') && (
          <div className="form-section">
            <h3>Template Parameters <span className="optional-label">(Optional - Override Defaults)</span></h3>
            <p className="section-description">Configure Zero Trust security and Container Apps settings</p>
            
            {/* Container Configuration Section */}
            <div className="parameter-group">
              <h4>📦 Container Configuration</h4>
              
              <div className="form-group">
                <label>Container Image</label>
                <input
                  type="text"
                  className="form-control"
                  value={templateParameters.containerImage}
                  onChange={(e) => setTemplateParameters({
                    ...templateParameters,
                    containerImage: e.target.value
                  })}
                  placeholder="mcr.microsoft.com/azuredocs/containerapps-helloworld:latest"
                />
                <p className="field-help">Container image from ACR, Docker Hub, or MCR</p>
              </div>
              
              <div className="form-row">
                <div className="form-group">
                  <label>Container Port</label>
                  <input
                    type="number"
                    className="form-control"
                    min="1"
                    max="65535"
                    value={templateParameters.containerPort}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      containerPort: e.target.value
                    })}
                  />
                  <p className="field-help">Port your container listens on</p>
                </div>
                
                <div className="form-group">
                  <label>CPU Cores</label>
                  <select
                    className="form-control"
                    value={templateParameters.cpuCores}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      cpuCores: e.target.value
                    })}
                  >
                    <option value="0.25">0.25 vCPU</option>
                    <option value="0.5">0.5 vCPU (Recommended)</option>
                    <option value="0.75">0.75 vCPU</option>
                    <option value="1">1 vCPU</option>
                    <option value="1.5">1.5 vCPU</option>
                    <option value="2">2 vCPU</option>
                  </select>
                </div>
                
                <div className="form-group">
                  <label>Memory (GB)</label>
                  <select
                    className="form-control"
                    value={templateParameters.memorySize}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      memorySize: e.target.value
                    })}
                  >
                    <option value="0.5">0.5 GB</option>
                    <option value="1">1 GB (Recommended)</option>
                    <option value="1.5">1.5 GB</option>
                    <option value="2">2 GB</option>
                    <option value="3">3 GB</option>
                    <option value="4">4 GB</option>
                  </select>
                </div>
              </div>
              
              <div className="form-row">
                <div className="form-group">
                  <label>Minimum Replicas</label>
                  <input
                    type="number"
                    className="form-control"
                    min="0"
                    max="30"
                    value={templateParameters.minReplicas}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      minReplicas: e.target.value
                    })}
                  />
                  <p className="field-help">Min replicas (0 = scale to zero)</p>
                </div>
                
                <div className="form-group">
                  <label>Maximum Replicas</label>
                  <input
                    type="number"
                    className="form-control"
                    min="1"
                    max="30"
                    value={templateParameters.maxReplicas}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      maxReplicas: e.target.value
                    })}
                  />
                  <p className="field-help">Max replicas for auto-scaling</p>
                </div>
              </div>
            </div>
            
            {/* Security & Ingress Section */}
            <div className="parameter-group">
              <h4>🔒 Security & Ingress Configuration</h4>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={!templateParameters.allowInsecure}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      allowInsecure: !e.target.checked
                    })}
                  />
                  <span>HTTPS Only (Recommended)</span>
                </label>
                <p className="field-help">🔐 Enforce HTTPS for all ingress traffic</p>
              </div>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.externalIngress}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      externalIngress: e.target.checked
                    })}
                  />
                  <span>Enable External Ingress</span>
                </label>
                <p className="field-help">🌐 Allow traffic from internet (uncheck for internal-only apps)</p>
              </div>
              
              <div className="form-group">
                <label>Transport Protocol</label>
                <select
                  className="form-control"
                  value={templateParameters.transport}
                  onChange={(e) => setTemplateParameters({
                    ...templateParameters,
                    transport: e.target.value
                  })}
                >
                  <option value="auto">Auto (HTTP/HTTP2)</option>
                  <option value="http">HTTP Only</option>
                  <option value="http2">HTTP2 Only</option>
                  <option value="tcp">TCP (for non-HTTP)</option>
                </select>
                <p className="field-help">Ingress transport protocol</p>
              </div>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enableVnetIntegrationCA}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enableVnetIntegrationCA: e.target.checked
                    })}
                  />
                  <span>Enable VNet Integration (Workload Profiles Required)</span>
                </label>
                <p className="field-help">🌐 Deploy into Virtual Network defined in template's network configuration</p>
              </div>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enablePrivateEndpointCA}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enablePrivateEndpointCA: e.target.checked
                    })}
                  />
                  <span>Enable Private Endpoint (Premium)</span>
                </label>
                <p className="field-help">🔒 Container Apps Environment accessible only via private network</p>
              </div>
            </div>
            
            {/* DAPR Configuration Section */}
            <div className="parameter-group">
              <h4>🔌 DAPR Service Mesh</h4>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enableDapr}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enableDapr: e.target.checked
                    })}
                  />
                  <span>Enable DAPR (Distributed Application Runtime)</span>
                </label>
                <p className="field-help">⚡ Enable service-to-service invocation, pub/sub, state management, and secrets</p>
              </div>
              
              {templateParameters.enableDapr && (
                <>
                  <div className="form-group">
                    <label>DAPR App ID</label>
                    <input
                      type="text"
                      className="form-control"
                      value={templateParameters.daprAppId}
                      onChange={(e) => setTemplateParameters({
                        ...templateParameters,
                        daprAppId: e.target.value
                      })}
                      placeholder="my-service"
                    />
                    <p className="field-help">Unique identifier for DAPR service discovery</p>
                  </div>
                  
                  <div className="form-group">
                    <label>DAPR App Port</label>
                    <input
                      type="number"
                      className="form-control"
                      min="1"
                      max="65535"
                      value={templateParameters.daprAppPort}
                      onChange={(e) => setTemplateParameters({
                        ...templateParameters,
                        daprAppPort: e.target.value
                      })}
                    />
                    <p className="field-help">Port DAPR should communicate with your app on</p>
                  </div>
                  
                  <div className="form-group">
                    <label className="checkbox-label">
                      <input 
                        type="checkbox"
                        checked={templateParameters.daprEnableApiLogging}
                        onChange={(e) => setTemplateParameters({
                          ...templateParameters,
                          daprEnableApiLogging: e.target.checked
                        })}
                      />
                      <span>Enable DAPR API Logging</span>
                    </label>
                    <p className="field-help">📝 Log all DAPR API calls for debugging</p>
                  </div>
                </>
              )}
            </div>
            
            {/* Advanced Configuration Section */}
            <div className="parameter-group">
              <h4>🛡️ Advanced Configuration</h4>
              
              <div className="form-group">
                <label>Revision Mode</label>
                <select
                  className="form-control"
                  value={templateParameters.revisionMode}
                  onChange={(e) => setTemplateParameters({
                    ...templateParameters,
                    revisionMode: e.target.value
                  })}
                >
                  <option value="Single">Single (Latest revision only)</option>
                  <option value="Multiple">Multiple (Traffic splitting enabled)</option>
                </select>
                <p className="field-help">Revision deployment mode</p>
              </div>
              
              <div className="form-group">
                <label>Max Inactive Revisions</label>
                <input
                  type="number"
                  className="form-control"
                  min="0"
                  max="100"
                  value={templateParameters.maxInactiveRevisions}
                  onChange={(e) => setTemplateParameters({
                    ...templateParameters,
                    maxInactiveRevisions: e.target.value
                  })}
                />
                <p className="field-help">Number of inactive revisions to keep for rollback</p>
              </div>
              
              <div className="form-group">
                <label className="checkbox-label">
                  <input 
                    type="checkbox"
                    checked={templateParameters.enableIPRestrictions}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      enableIPRestrictions: e.target.checked
                    })}
                  />
                  <span>Enable IP Restrictions</span>
                </label>
                <p className="field-help">🔒 Restrict ingress to specific IP ranges</p>
              </div>
              
              {templateParameters.enableIPRestrictions && (
                <div className="form-group">
                  <label>Allowed IP Ranges (JSON Array)</label>
                  <textarea
                    className="form-control"
                    rows={3}
                    value={templateParameters.allowedIPRanges}
                    onChange={(e) => setTemplateParameters({
                      ...templateParameters,
                      allowedIPRanges: e.target.value
                    })}
                    placeholder='["203.0.113.0/24", "198.51.100.0/24"]'
                  />
                  <p className="field-help">CIDR ranges allowed to access the app</p>
                </div>
              )}
            </div>
            
            <div className="info-banner">
              <strong>ℹ️ Zero Trust Configuration:</strong> Container Apps implements serverless security including HTTPS enforcement, VNet integration for network isolation, DAPR for secure service-to-service communication, managed identity, and IP restrictions. Supports scale-to-zero for cost optimization.
            </div>
          </div>
        )}

        <div className="form-section">
          <h3>Tags</h3>
          
          <div className="tags-input-group">
            <input
              type="text"
              placeholder="Key"
              value={tagInput.key}
              onChange={(e) => setTagInput({ ...tagInput, key: e.target.value })}
              className="form-control tag-input"
            />
            <input
              type="text"
              placeholder="Value"
              value={tagInput.value}
              onChange={(e) => setTagInput({ ...tagInput, value: e.target.value })}
              className="form-control tag-input"
            />
            <button
              type="button"
              onClick={handleAddTag}
              className="btn btn-secondary btn-small"
            >
              + Add Tag
            </button>
          </div>

          {formData.tags && Object.keys(formData.tags).length > 0 && (
            <div className="tags-list">
              {Object.entries(formData.tags).map(([key, value]) => (
                <div key={key} className="tag-item">
                  <span className="tag-text">
                    <strong>{key}:</strong> {value}
                  </span>
                  <button
                    type="button"
                    onClick={() => handleRemoveTag(key)}
                    className="tag-remove"
                  >
                    ×
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="form-actions">
          <button
            type="button"
            onClick={() => navigate('/environments')}
            className="btn btn-secondary"
            disabled={loading}
          >
            Cancel
          </button>
          <button
            type="submit"
            className="btn btn-primary"
            disabled={loading}
          >
            {loading ? '⏳ Creating Environment...' : '🚀 Create Environment'}
          </button>
        </div>
      </form>
    </div>
  );
};

export default CreateEnvironment;
