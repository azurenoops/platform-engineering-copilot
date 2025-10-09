import React, { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import adminApi, { CreateTemplateRequest, ValidationResult } from '../services/adminApi';
import NetworkConfigurationForm from './NetworkConfigurationForm';
import ValidationResults from './ValidationResults';
import './CreateTemplate.css';

const CreateTemplate: React.FC = () => {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const isEditMode = !!id;
  
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [showNetworkConfig, setShowNetworkConfig] = useState(false);
  const [showComputeConfig, setShowComputeConfig] = useState(false);
  const [validationResult, setValidationResult] = useState<ValidationResult | null>(null);
  const [showValidationResults, setShowValidationResults] = useState(false);
  
  const [formData, setFormData] = useState<CreateTemplateRequest>({
    templateName: '',
    serviceName: '',
    templateType: 'microservice',
    description: '',
    application: {
      language: 'Python',
      type: 'WebAPI',  // Changed from 'API' to match enum
      port: 8000,
      framework: 'FastAPI'
    },
    databases: [],
    infrastructure: {
      format: 'Bicep',
      computePlatform: 'AKS',
      cloudProvider: 'Azure',
      // Zero Trust defaults
      enablePrivateCluster: false,
      authorizedIPRanges: '',
      enableWorkloadIdentity: true,
      logAnalyticsWorkspaceId: '',
      enableAzurePolicy: true,
      enableImageCleaner: true,
      diskEncryptionSetId: '',
      enablePrivateEndpointACR: false,
      // Container Apps Zero Trust defaults
      enablePrivateEndpointCA: false,
      enableManagedIdentityCA: true,
      enableIPRestrictionsCA: false,
      
      // === AWS TERRAFORM ZERO TRUST DEFAULTS ===
      
      // ECS (AWS Elastic Container Service)
      enableServiceConnect: true,
      enableECSExec: true,
      enableSecretsManager: true,
      httpsListener: true,
      sslCertificateArn: '',
      enableWAF: true,
      webAclArn: '',
      allowedCIDRBlocks: '',
      enableVPCEndpoints: true,
      enableGuardDuty: true,
      enableCloudTrail: true,
      enableECRScanning: true,
      allowedRegistries: '',
      enableNetworkIsolation: true,
      enableReadOnlyRootFS: true,
      enableDropCapabilities: true,
      enableKMSEncryption: true,
      kmsKeyId: '',
      
      // EKS (AWS Elastic Kubernetes Service)
      enablePrivateEndpointEKS: true,
      enableIRSA: true,
      enablePodSecurity: true,
      enableNetworkPolicies: true,
      enableGuardDutyEKS: true,
      enableKMSEncryptionEKS: true,
      enableVPCCNIEncryption: true,
      allowedAPIBlocks: '',
      enableFargateProfiles: false,
      enableManagedNodeGroups: true,
      enableSpotInstancesEKS: false,
      enableClusterAutoscaler: true,
      enableALBController: true,
      enableEBSCSI: true,
      enableEFSCSI: false,
      enableContainerInsights: true,
      enablePodIdentity: true,
      enableIMDSv2: true,
      enableECRPrivateEndpoint: true,
      eksKMSKeyId: '',
      
      // Lambda (AWS Serverless)
      enableVPCConfig: true,
      enableKMSEncryptionLambda: true,
      enableSecretsManagerLambda: true,
      enableCodeSigning: true,
      enableFunctionURLAuth: true,
      enablePrivateAPI: true,
      enableWAFLambda: true,
      enableAPIKeyRequired: true,
      enableCloudWatchLogsEncryption: true,
      enableGuardDutyLambda: true,
      enableResourceBasedPolicy: true,
      allowedPrincipals: '',
      enableLayerVersionValidation: true,
      lambdaVPCSubnetIds: '',
      lambdaKMSKeyId: '',
      
      // === GCP TERRAFORM ZERO TRUST DEFAULTS ===
      
      // Cloud Run (GCP Serverless Containers)
      enableVPCConnector: true,
      ingressSettings: 'internal-and-cloud-load-balancing',
      enableServiceIdentity: true,
      enableBinaryAuthorization: true,
      enableCloudArmor: true,
      enableCMEK: true,
      enableCloudAuditLogs: true,
      enableCloudMonitoring: true,
      maxInstanceConcurrency: 80,
      enableHTTPSOnly: true,
      allowedIngressSources: '',
      enableVPCEgress: true,
      egressSettings: 'private-ranges-only',
      enableExecutionEnvironmentV2: true,
      enableCPUThrottling: false,
      enableStartupCPUBoost: true,
      enableSessionAffinity: true,
      cloudRunKMSKeyId: '',
      
      // GKE (Google Kubernetes Engine)
      enablePrivateClusterGKE: true,
      masterIPV4CIDRBlock: '172.16.0.0/28',
      enableWorkloadIdentityGKE: true,
      enableBinaryAuthorizationGKE: true,
      enableShieldedNodes: true,
      enableGKEAutopilot: false,
      enableNetworkPoliciesGKE: true,
      enableCloudArmorGKE: true,
      enablePodSecurityPolicy: true,
      enableSecureBoot: true,
      enableIntegrityMonitoring: true,
      enableKMSEncryptionGKE: true,
      masterAuthorizedNetworks: '',
      enableVPCNative: true,
      enablePrivateEndpointGKE: true,
      enableIntranodeVisibility: true,
      enableDataplaneV2: true,
      enableVulnerabilityScanning: true,
      enableSecurityPosture: true,
      gkeKMSKeyId: '',
      
      // === AZURE TERRAFORM ZERO TRUST DEFAULTS ===
      
      // AKS Terraform (mirrors Bicep)
      enablePrivateClusterTF: false,
      authorizedIPRangesTF: '',
      enableWorkloadIdentityTF: true,
      logAnalyticsWorkspaceIdTF: '',
      enableAzurePolicyTF: true,
      enableImageCleanerTF: true,
      imageCleanerIntervalHours: 168,
      diskEncryptionSetIdTF: '',
      enableDefender: true,
      enablePrivateEndpointACRTF: false,
      acrSubnetId: '',
      enableAzureRBAC: true,
      enableOIDCIssuer: true,
      enablePodSecurityPolicyTF: true,
      networkPolicy: 'azure',
      enableHTTPApplicationRouting: false,
      enableKeyVaultSecretsProvider: true,
      secretRotationPollInterval: '2m',
      enableAutoScalingTF: true,
      maintenanceWindow: '',
      nodePoolSubnetId: '',
      
      // App Service Terraform
      enableVnetIntegrationTF: true,
      appServiceSubnetId: '',
      enablePrivateEndpointTF: false,
      enableManagedIdentityTF: true,
      httpsOnlyTF: true,
      minTlsVersionTF: '1.3',
      ftpsStateTF: 'Disabled',
      enableIPRestrictions: false,
      allowedIPAddresses: '',
      enableClientCertificateTF: false,
      clientCertModeTF: 'Optional',
      enableAlwaysEncrypted: true,
      keyVaultId: '',
      enableAppServiceAuth: false,
      enableDefenderAppService: true,
      
      // Container Instances Terraform
      enableVnetIntegrationCI: true,
      containerInstancesSubnetId: '',
      enableManagedIdentityCI: true,
      enablePrivateEndpointCI: false,
      enableImageScanning: true,
      enableContentTrust: true,
      enableDefenderCI: true,
      enableZoneRedundancy: false,
      enablePublicNetworkAccess: false,
      allowedIPRangesCI: '',
      enableEncryptionCMEK: false,
      keyVaultKeyId: '',
      enableLogAnalyticsCI: true,
      logAnalyticsWorkspaceIdCI: '',
      enableAzureMonitorCI: true,
      restartPolicy: 'OnFailure',
      tags: {}
    },
    compute: {
      instanceType: 'Standard_D2s_v3',
      minInstances: 1,
      maxInstances: 10,
      enableAutoScaling: true,
      cpuLimit: '2',
      memoryLimit: '4Gi',
      storageSize: '100Gi',
      enableSpotInstances: false,
      containerImage: '',
      nodePoolName: 'default'
    },
    network: {
      vnetName: '',
      addressSpace: '10.0.0.0/16',
      subnetName: 'default',
      subnetAddressPrefix: '10.0.1.0/24',
      enableServiceEndpoints: false,
      serviceEndpoints: [],
      enablePrivateEndpoint: false,
      nsgName: '',
      enableDdosProtection: false
    },
    deployment: {
      orchestrator: 'Kubernetes',
      cicdPlatform: 'GitHub Actions'
    },
    security: {
      authenticationProvider: 'Azure AD',
      secrets: []
    },
    observability: {
      logging: true,
      metrics: true,
      tracing: false
    }
  });

  const [newDatabase, setNewDatabase] = useState({
    name: '',
    type: 'PostgreSQL',
    location: 'Cloud',
    version: ''
  });

  // Load template data when editing
  useEffect(() => {
    if (isEditMode && id) {
      loadTemplateForEdit(id);
    }
  }, [isEditMode, id]);

  const loadTemplateForEdit = async (templateId: string) => {
    try {
      setLoading(true);
      const template = await adminApi.getTemplate(templateId);
      
      // Map template data back to form data
      // Note: This is a simplified mapping - you may need to adjust based on your template structure
      setFormData({
        templateName: template.name,
        serviceName: template.name, // Use name as serviceName fallback
        templateType: template.templateType,
        description: template.description || '',
        // Note: The template doesn't store the full form structure,
        // so we'll keep the defaults for application, infrastructure, etc.
        // In a real app, you'd want to store and retrieve these properly
        application: formData.application,
        databases: formData.databases,
        infrastructure: {
          format: template.format || 'Bicep',
          computePlatform: formData.infrastructure?.computePlatform || 'AKS',
          cloudProvider: formData.infrastructure?.cloudProvider || 'Azure',
          region: formData.infrastructure?.region || 'eastus', // Required field
          includeNetworking: formData.infrastructure?.includeNetworking ?? true,
          includeStorage: formData.infrastructure?.includeStorage ?? false,
          includeLoadBalancer: formData.infrastructure?.includeLoadBalancer ?? false
        },
        compute: formData.compute,
        network: formData.network,
        deployment: formData.deployment,
        security: formData.security,
        observability: formData.observability
      });
      
      setError(null);
    } catch (err: any) {
      console.error('Failed to load template:', err);
      setError(err.response?.data?.error || err.message || 'Failed to load template');
    } finally {
      setLoading(false);
    }
  };

  const handleInputChange = (field: string, value: any) => {
    setFormData(prev => ({ ...prev, [field]: value }));
  };

  const handleNestedChange = (parent: string, field: string, value: any) => {
    setFormData(prev => ({
      ...prev,
      [parent]: {
        ...(prev[parent as keyof CreateTemplateRequest] as any),
        [field]: value
      }
    }));
  };

  const handleLanguageChange = (language: string) => {
    const frameworkMap: { [key: string]: string } = {
      'Python': 'FastAPI',
      'NodeJS': 'Express',
      'Java': 'Spring Boot',
      'DotNet': 'ASP.NET Core',
      'Go': 'Gin',
      'Rust': 'Actix',
      'Ruby': 'Rails',
      'PHP': 'Laravel'
    };
    
    const defaultFramework = frameworkMap[language] || '';
    
    setFormData(prev => ({
      ...prev,
      application: {
        ...prev.application,
        language: language,
        framework: defaultFramework,
        type: prev.application?.type || '',
        port: prev.application?.port || 8080
      }
    }));
  };

  const addDatabase = () => {
    if (newDatabase.name && newDatabase.type) {
      setFormData(prev => ({
        ...prev,
        databases: [...(prev.databases || []), { ...newDatabase }]
      }));
      setNewDatabase({ name: '', type: 'PostgreSQL', location: 'Cloud', version: '' });
    }
  };

  const removeDatabase = (index: number) => {
    setFormData(prev => ({
      ...prev,
      databases: prev.databases?.filter((_, i) => i !== index) || []
    }));
  };

  // Tag management functions
  const addTag = () => {
    const existingTags = formData.infrastructure?.tags || {};
    const newKey = `tag${Object.keys(existingTags).length + 1}`;
    handleNestedChange('infrastructure', 'tags', { ...existingTags, [newKey]: '' });
  };

  const updateTagKey = (oldKey: string, newKey: string) => {
    const tags = { ...formData.infrastructure?.tags };
    if (!tags) return;
    const value = tags[oldKey];
    delete tags[oldKey];
    tags[newKey] = value;
    handleNestedChange('infrastructure', 'tags', tags);
  };

  const updateTagValue = (key: string, value: string) => {
    const tags = { ...formData.infrastructure?.tags, [key]: value };
    handleNestedChange('infrastructure', 'tags', tags);
  };

  const removeTag = (key: string) => {
    const tags = { ...formData.infrastructure?.tags };
    delete tags[key];
    handleNestedChange('infrastructure', 'tags', tags);
  };

  // Helper function to determine which sections to show based on template type
  const isInfrastructureTemplate = () => 
    formData.templateType === 'infrastructure';
  const isServerlessTemplate = () => formData.templateType === 'serverless';
  
  // Helper functions to determine compute platform type
  const getComputePlatform = () => formData.infrastructure?.computePlatform || 'AKS';
  const isKubernetesPlatform = () => ['AKS', 'EKS', 'GKE'].includes(getComputePlatform());
  const isContainerPlatform = () => ['ECS', 'Cloud Run'].includes(getComputePlatform());
  const isAppServicePlatform = () => getComputePlatform() === 'AppService';
  const isServerlessPlatform = () => getComputePlatform() === 'Lambda';
  
  const shouldShowApplicationConfig = () => {
    return !isInfrastructureTemplate();
  };

  const shouldShowDatabaseConfig = () => {
    return !isInfrastructureTemplate() && !isServerlessTemplate();
  };

  const shouldShowDeploymentConfig = () => {
    return !isInfrastructureTemplate();
  };

  const shouldShowSecurityConfig = () => {
    // Security is only relevant for application templates
    return !isInfrastructureTemplate();
  };

  const shouldShowObservabilityConfig = () => {
    return !isInfrastructureTemplate();
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!formData.templateName || !formData.serviceName) {
      setError('Template name and service name are required');
      return;
    }

    setLoading(true);
    setError(null);
    setSuccess(null);

    try {
      if (isEditMode && id) {
        // Update existing template
        const response = await adminApi.updateTemplate(id, formData);
        setSuccess(`✅ Template "${response.templateName}" updated successfully!`);
        
        // Navigate to template details after 1 second
        setTimeout(() => {
          navigate(`/templates/${id}`);
        }, 1000);
      } else {
        // Create new template
        const response = await adminApi.createTemplate(formData);
        setSuccess(`✅ Template "${response.templateName}" created successfully! Generated ${response.generatedFiles.length} files.`);
        
        // Navigate to template details after 2 seconds
        setTimeout(() => {
          navigate(`/templates/${response.templateId}`);
        }, 2000);
      }
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || `Failed to ${isEditMode ? 'update' : 'create'} template`);
    } finally {
      setLoading(false);
    }
  };

  const handleValidate = async () => {
    setLoading(true);
    setError(null);
    setSuccess(null);
    setShowValidationResults(false);

    try {
      const result = await adminApi.validateTemplate(formData);
      setValidationResult(result);
      setShowValidationResults(true);
      
      if (result.isValid) {
        setSuccess(`✅ Validation passed! Your ${result.platform || 'configuration'} configuration is ready to deploy.`);
      } else {
        setError(`❌ Validation found ${result.errors.length} error(s) that must be fixed before deployment.`);
      }
    } catch (err: any) {
      setError(err.response?.data?.message || err.message || 'Validation failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="create-template">
      <div className="create-template-header">
        <h2>{isEditMode ? '✏️ Edit Service Template' : '➕ Create New Service Template'}</h2>
        <button onClick={() => navigate('/templates')} className="btn-secondary">
          ← Back to Templates
        </button>
      </div>

      {error && (
        <div className="alert alert-error">
          ⚠️ {error}
        </div>
      )}

      {success && (
        <div className="alert alert-success">
          {success}
        </div>
      )}

      {showValidationResults && validationResult && (
        <ValidationResults 
          validationResult={validationResult}
          onDismiss={() => {
            setShowValidationResults(false);
            setValidationResult(null);
            setSuccess(null);
            setError(null);
          }}
        />
      )}

      <form onSubmit={handleSubmit} className="template-form">
        {/* Basic Information */}
        <section className="form-section">
          <h3>📝 Basic Information</h3>
          <div className="form-grid">
            <div className="form-group">
              <label>Template Name *</label>
              <input
                type="text"
                value={formData.templateName}
                onChange={(e) => handleInputChange('templateName', e.target.value)}
                placeholder="e.g., python-api-postgres"
                required
              />
            </div>
            <div className="form-group">
              <label>Service Name *</label>
              <input
                type="text"
                value={formData.serviceName}
                onChange={(e) => handleInputChange('serviceName', e.target.value)}
                placeholder="e.g., order-api"
                required
              />
            </div>
          </div>
          <div className="form-group">
            <label>Template Type</label>
            <select
              value={formData.templateType || 'microservice'}
              onChange={(e) => {
                const newType = e.target.value;
                handleInputChange('templateType', newType);
                // Auto-enable network and compute config for infrastructure templates
                if (newType === 'infrastructure') {
                  setShowNetworkConfig(true);
                  setShowComputeConfig(true);
                }
              }}
            >
              <option value="microservice">🔧 Microservice</option>
              <option value="web-app">🌐 Web Application</option>
              <option value="api">📡 API Service</option>
              <option value="infrastructure">🏗️ Infrastructure</option>
              <option value="data-pipeline">📊 Data Pipeline</option>
              <option value="ml-platform">🤖 ML Platform</option>
              <option value="serverless">⚡ Serverless Function</option>
            </select>
            {formData.templateType === 'infrastructure' && (
              <small className="field-hint" style={{ color: '#0066cc', fontWeight: 600 }}>
                💡 Infrastructure templates focus on networking, compute, and cloud resources
              </small>
            )}
          </div>
          <div className="form-group">
            <label>Description</label>
            <textarea
              value={formData.description}
              onChange={(e) => handleInputChange('description', e.target.value)}
              placeholder="Describe what this template is for..."
              rows={3}
            />
          </div>
        </section>

        {/* Application Configuration - Hidden for Infrastructure templates */}
        {shouldShowApplicationConfig() && (
        <section className="form-section">
          <h3>💻 Application Configuration</h3>
          <div className="form-grid">
            <div className="form-group">
              <label>Language</label>
              <select
                value={formData.application?.language}
                onChange={(e) => handleLanguageChange(e.target.value)}
              >
                <option value="Python">Python</option>
                <option value="NodeJS">Node.js</option>
                <option value="DotNet">.NET/C#</option>
                <option value="Java">Java</option>
                <option value="Go">Go</option>
                <option value="Rust">Rust</option>
                <option value="Ruby">Ruby</option>
                <option value="PHP">PHP</option>
              </select>
            </div>
            <div className="form-group">
              <label>Framework</label>
              <input
                type="text"
                value={formData.application?.framework}
                onChange={(e) => handleNestedChange('application', 'framework', e.target.value)}
                placeholder="e.g., FastAPI, Express, Spring Boot"
              />
            </div>
            <div className="form-group">
              <label>Type</label>
              <select
                value={formData.application?.type}
                onChange={(e) => handleNestedChange('application', 'type', e.target.value)}
              >
                <option value="WebAPI">Web API</option>
                <option value="WebApp">Web Application</option>
                <option value="BackgroundWorker">Background Worker</option>
                <option value="MessageConsumer">Message Consumer</option>
                <option value="Microservice">Microservice</option>
                <option value="Serverless">Serverless Function</option>
              </select>
            </div>
            <div className="form-group">
              <label>Port</label>
              <input
                type="number"
                value={formData.application?.port}
                onChange={(e) => handleNestedChange('application', 'port', parseInt(e.target.value))}
              />
            </div>
          </div>
        </section>
        )}

        {/* Database Configuration - Hidden for Infrastructure and Serverless templates */}
        {shouldShowDatabaseConfig() && (
        <section className="form-section">
          <h3>🗄️ Database Configuration</h3>
          
          {formData.databases && formData.databases.length > 0 && (
            <div className="database-list">
              {formData.databases.map((db, index) => (
                <div key={index} className="database-item">
                  <div className="database-info">
                    <strong>{db.name}</strong> - {db.type} ({db.location})
                    {db.version && <span className="db-version">v{db.version}</span>}
                  </div>
                  <button
                    type="button"
                    onClick={() => removeDatabase(index)}
                    className="btn-remove"
                  >
                    ✗ Remove
                  </button>
                </div>
              ))}
            </div>
          )}

          <div className="add-database-form">
            <h4>Add Database</h4>
            <div className="form-grid">
              <input
                type="text"
                placeholder="Database name"
                value={newDatabase.name}
                onChange={(e) => setNewDatabase(prev => ({ ...prev, name: e.target.value }))}
              />
              <select
                value={newDatabase.type}
                onChange={(e) => setNewDatabase(prev => ({ ...prev, type: e.target.value }))}
              >
                <option>PostgreSQL</option>
                <option>MySQL</option>
                <option>MongoDB</option>
                <option>Redis</option>
                <option>CosmosDB</option>
                <option>SQL Server</option>
              </select>
              <select
                value={newDatabase.location}
                onChange={(e) => setNewDatabase(prev => ({ ...prev, location: e.target.value }))}
              >
                <option>Cloud</option>
                <option>Container</option>
                <option>External</option>
              </select>
              <input
                type="text"
                placeholder="Version (optional)"
                value={newDatabase.version}
                onChange={(e) => setNewDatabase(prev => ({ ...prev, version: e.target.value }))}
              />
              <button type="button" onClick={addDatabase} className="btn-add">
                ➕ Add Database
              </button>
            </div>
          </div>
        </section>
        )}

        {/* Infrastructure Configuration - Always shown but emphasized for Infrastructure templates */}
        <section className={`form-section ${isInfrastructureTemplate() ? 'form-section-emphasized' : ''}`}>
          <h3>☁️ Infrastructure Configuration</h3>
          {isInfrastructureTemplate() && (
            <div className="section-info">
              <p>🏗️ <strong>Infrastructure Template Mode:</strong> Focus on defining cloud resources, networking, and compute infrastructure.</p>
            </div>
          )}
          <div className="form-grid">
            <div className="form-group">
              <label>Resource Type *</label>
              <select
                value={formData.infrastructure?.resourceType || 'Compute'}
                onChange={(e) => handleNestedChange('infrastructure', 'resourceType', e.target.value)}
              >
                <option value="Compute">💻 Compute (AKS, VMs, App Services)</option>
                <option value="Network">🌐 Network (VNet, Subnets, NSGs)</option>
              </select>
              {formData.infrastructure?.resourceType === 'Network' && (
                <small className="field-hint" style={{ color: '#0066cc', fontWeight: 600 }}>
                  💡 Network templates create foundational networking infrastructure
                </small>
              )}
            </div>
            <div className="form-group">
              <label>Format</label>
              <select
                value={formData.infrastructure?.format}
                onChange={(e) => handleNestedChange('infrastructure', 'format', e.target.value)}
              >
                <option>Bicep</option>
                <option>Terraform</option>
                <option>ARM</option>
                <option>CloudFormation</option>
              </select>
            </div>
            <div className="form-group">
              <label>Cloud Provider</label>
              <select
                value={formData.infrastructure?.cloudProvider}
                onChange={(e) => handleNestedChange('infrastructure', 'cloudProvider', e.target.value)}
              >
                <option>Azure</option>
                <option>AWS</option>
                <option>GCP</option>
              </select>
            </div>
          </div>
          
          {/* Compute Platform Selection - Only show when resourceType is Compute */}
          {formData.infrastructure?.resourceType !== 'Network' && (
            <div className="form-grid">
              <div className="form-group">
                <label>Compute Platform</label>
                <select
                  value={formData.infrastructure?.computePlatform}
                  onChange={(e) => handleNestedChange('infrastructure', 'computePlatform', e.target.value)}
                >
                  <option value="AKS">AKS (Azure Kubernetes)</option>
                  <option value="EKS">EKS (AWS Kubernetes)</option>
                  <option value="GKE">GKE (Google Kubernetes)</option>
                  <option value="ECS">ECS (AWS Containers)</option>
                  <option value="CloudRun">Cloud Run (Google)</option>
                  <option value="ContainerApps">Container Apps (Azure)</option>
                  <option value="AppService">App Service (Azure)</option>
                  <option value="Lambda">Lambda (AWS Serverless)</option>
                  <option value="VirtualMachine">Virtual Machine</option>
                </select>
              </div>
            </div>
          )}
        </section>

        {/* Compute Platform Configuration - Hidden when resourceType is Network */}
        {formData.infrastructure?.resourceType !== 'Network' && (
        <section className={`form-section ${isInfrastructureTemplate() ? 'form-section-emphasized' : ''}`}>
          {!isInfrastructureTemplate() && (
          <div className="section-toggle">
            <label className="toggle-label">
              <input
                type="checkbox"
                checked={showComputeConfig}
                onChange={(e) => setShowComputeConfig(e.target.checked)}
                className="toggle-checkbox"
              />
              <span className="toggle-text">💻 Configure Compute Resources (Optional)</span>
            </label>
          </div>
          )}

          {isInfrastructureTemplate() && (
            <div className="section-info">
              <p>💻 <strong>Compute Configuration:</strong> Define instance types, scaling policies, resource limits, and compute platform settings.</p>
            </div>
          )}

          {(showComputeConfig || isInfrastructureTemplate()) && (
            <div className="compute-config-content">
              <h3>💻 {getComputePlatform()} Configuration</h3>
              <div className="platform-badge">{getComputePlatform()}</div>
              
              {/* AKS (Azure Kubernetes Service) Configuration */}
              {getComputePlatform() === 'AKS' && (
                <>
                  <div className="config-subsection">
                    <h4>AKS Cluster Configuration</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>Node Virtual Machine Size</label>
                        <select
                          value={formData.compute?.instanceType || 'Standard_D2s_v3'}
                          onChange={(e) => handleNestedChange('compute', 'instanceType', e.target.value)}
                        >
                          <optgroup label="General Purpose">
                            <option value="Standard_B2s">Standard_B2s (2 vCPU, 4GB RAM)</option>
                            <option value="Standard_D2s_v3">Standard_D2s_v3 (2 vCPU, 8GB RAM) - Recommended</option>
                            <option value="Standard_D4s_v3">Standard_D4s_v3 (4 vCPU, 16GB RAM)</option>
                            <option value="Standard_D8s_v3">Standard_D8s_v3 (8 vCPU, 32GB RAM)</option>
                          </optgroup>
                          <optgroup label="Compute Optimized">
                            <option value="Standard_F2s_v2">Standard_F2s_v2 (2 vCPU, 4GB RAM)</option>
                            <option value="Standard_F4s_v2">Standard_F4s_v2 (4 vCPU, 8GB RAM)</option>
                            <option value="Standard_F8s_v2">Standard_F8s_v2 (8 vCPU, 16GB RAM)</option>
                          </optgroup>
                          <optgroup label="Memory Optimized">
                            <option value="Standard_E2s_v3">Standard_E2s_v3 (2 vCPU, 16GB RAM)</option>
                            <option value="Standard_E4s_v3">Standard_E4s_v3 (4 vCPU, 32GB RAM)</option>
                          </optgroup>
                        </select>
                      </div>
                      <div className="form-group">
                        <label>Node Pool Name</label>
                        <input
                          type="text"
                          value={formData.compute?.nodePoolName || 'default'}
                          onChange={(e) => handleNestedChange('compute', 'nodePoolName', e.target.value)}
                          placeholder="e.g., nodepool1"
                        />
                      </div>
                    </div>
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input
                          type="checkbox"
                          checked={formData.compute?.enableAutoScaling || false}
                          onChange={(e) => handleNestedChange('compute', 'enableAutoScaling', e.target.checked)}
                        />
                        ⚖️ Enable Node Pool Auto-Scaling
                      </label>
                    </div>
                    {formData.compute?.enableAutoScaling && (
                      <div className="form-grid">
                        <div className="form-group">
                          <label>Minimum Nodes</label>
                          <input type="number" min="1" value={formData.compute?.minInstances || 1}
                            onChange={(e) => handleNestedChange('compute', 'minInstances', parseInt(e.target.value))} />
                        </div>
                        <div className="form-group">
                          <label>Maximum Nodes</label>
                          <input type="number" min="1" value={formData.compute?.maxInstances || 10}
                            onChange={(e) => handleNestedChange('compute', 'maxInstances', parseInt(e.target.value))} />
                        </div>
                      </div>
                    )}
                  </div>
                  
                  <div className="config-subsection">
                    <h4>Kubernetes Resource Limits</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>CPU Limit</label>
                        <input type="text" value={formData.compute?.cpuLimit || '2'}
                          onChange={(e) => handleNestedChange('compute', 'cpuLimit', e.target.value)}
                          placeholder="e.g., 2, 4, 500m" />
                        <small className="field-hint">CPU cores or millicores (e.g., 2 or 2000m)</small>
                      </div>
                      <div className="form-group">
                        <label>Memory Limit</label>
                        <input type="text" value={formData.compute?.memoryLimit || '4Gi'}
                          onChange={(e) => handleNestedChange('compute', 'memoryLimit', e.target.value)}
                          placeholder="e.g., 4Gi, 512Mi" />
                        <small className="field-hint">Memory in Gi or Mi</small>
                      </div>
                      <div className="form-group">
                        <label>Storage Size (PVC)</label>
                        <input type="text" value={formData.compute?.storageSize || '100Gi'}
                          onChange={(e) => handleNestedChange('compute', 'storageSize', e.target.value)}
                          placeholder="e.g., 100Gi, 1Ti" />
                        <small className="field-hint">Persistent Volume Claim size</small>
                      </div>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>Container & Cost Options</h4>
                    <div className="form-group">
                      <label>Container Image</label>
                      <input type="text" value={formData.compute?.containerImage || ''}
                        onChange={(e) => handleNestedChange('compute', 'containerImage', e.target.value)}
                        placeholder="e.g., mcr.microsoft.com/dotnet/aspnet:8.0" />
                    </div>
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input type="checkbox" checked={formData.compute?.enableSpotInstances || false}
                          onChange={(e) => handleNestedChange('compute', 'enableSpotInstances', e.target.checked)} />
                        💰 Enable Spot Node Pools (Up to 90% cost savings)
                      </label>
                    </div>
                  </div>
                  
                  {/* Zero Trust Security Configuration */}
                  <div className="config-subsection">
                    <h4>🔒 Zero Trust Security Configuration</h4>
                    <p className="subsection-description">Configure comprehensive security controls for production-ready AKS clusters</p>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enablePrivateCluster || false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enablePrivateCluster', e.target.checked)} 
                        />
                        🔐 Enable Private Cluster (Recommended for Production)
                      </label>
                      <small className="field-hint">Restricts Kubernetes API server access to VNet only. Highest security by eliminating public endpoints.</small>
                    </div>
                    
                    {!formData.infrastructure?.enablePrivateCluster && (
                      <div className="form-group">
                        <label>Authorized IP Ranges (CIDR notation, comma-separated)</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.authorizedIPRanges || ''}
                          onChange={(e) => handleNestedChange('infrastructure', 'authorizedIPRanges', e.target.value)}
                          placeholder="203.0.113.0/24, 198.51.100.0/24" 
                        />
                        <small className="field-hint">IP ranges allowed to access the Kubernetes API server. Leave empty for unrestricted access (not recommended).</small>
                      </div>
                    )}
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableWorkloadIdentity !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableWorkloadIdentity', e.target.checked)} 
                        />
                        🎫 Enable Workload Identity (OIDC for Pods)
                      </label>
                      <small className="field-hint">Enables passwordless authentication for pods using Azure AD. Pods can access Azure resources without managing secrets.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.observability?.metrics !== false}
                          onChange={(e) => handleNestedChange('observability', 'metrics', e.target.checked)} 
                        />
                        📊 Enable Azure Monitor & Log Analytics
                      </label>
                      <small className="field-hint">Enables Container Insights, metrics collection, and comprehensive audit logging</small>
                    </div>
                    
                    {formData.observability?.metrics && (
                      <div className="form-group">
                        <label>Log Analytics Workspace Resource ID (Optional)</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.logAnalyticsWorkspaceId || ''}
                          onChange={(e) => handleNestedChange('infrastructure', 'logAnalyticsWorkspaceId', e.target.value)}
                          placeholder="/subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.OperationalInsights/workspaces/{name}" 
                        />
                        <small className="field-hint">Leave empty to create a new Log Analytics workspace automatically</small>
                      </div>
                    )}
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableAzurePolicy !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableAzurePolicy', e.target.checked)} 
                        />
                        ✅ Enable Azure Policy for AKS
                      </label>
                      <small className="field-hint">Enforces 7 critical security policies: allowed images (ACR/MCR), no privileged containers, HTTPS ingress, internal load balancers, AppArmor profiles, resource limits, read-only root filesystem</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableImageCleaner !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableImageCleaner', e.target.checked)} 
                        />
                        🔍 Enable Image Cleaner (Weekly Vulnerability Scanning)
                      </label>
                      <small className="field-hint">Automatically scans and removes vulnerable container images on a weekly schedule</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enablePrivateEndpointACR || false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enablePrivateEndpointACR', e.target.checked)} 
                        />
                        🔐 Enable Private Endpoint for ACR (Premium)
                      </label>
                      <small className="field-hint">Azure Container Registry accessible only via private network. Eliminates public access to container images for maximum security.</small>
                    </div>
                    
                    <div className="form-group">
                      <label>Disk Encryption Set ID (Optional - Customer-Managed Keys)</label>
                      <input 
                        type="text" 
                        value={formData.infrastructure?.diskEncryptionSetId || ''}
                        onChange={(e) => handleNestedChange('infrastructure', 'diskEncryptionSetId', e.target.value)}
                        placeholder="/subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Compute/diskEncryptionSets/{name}" 
                      />
                      <small className="field-hint">Azure Disk Encryption Set resource ID for customer-managed key encryption (CMK). Leave empty for platform-managed encryption. Required for compliance certifications (FedRAMP, HIPAA).</small>
                    </div>
                    
                    <div className="info-banner">
                      <strong>ℹ️ Zero Trust Architecture:</strong> These settings implement comprehensive Zero Trust security including private API server, Microsoft Defender for Containers, pod security admission (restricted), network policies (default-deny), and Azure Policy enforcement. All features are enabled by default for maximum security.
                    </div>
                  </div>

                  {/* Resource Tags */}
                  <div className="config-subsection">
                    <h4>🏷️ Resource Tags</h4>
                    <p className="subsection-description">Add custom tags to organize and track Azure resources (key-value pairs)</p>
                    
                    <div className="tag-list">
                      {Object.entries(formData.infrastructure?.tags || {}).map(([key, value]) => (
                        <div key={key} className="tag-row">
                          <div className="form-grid" style={{ gridTemplateColumns: '1fr 1fr auto' }}>
                            <div className="form-group">
                              <label>Key</label>
                              <input 
                                type="text" 
                                value={key}
                                onChange={(e) => updateTagKey(key, e.target.value)}
                                placeholder="e.g., Environment, Owner, CostCenter" 
                              />
                            </div>
                            <div className="form-group">
                              <label>Value</label>
                              <input 
                                type="text" 
                                value={value}
                                onChange={(e) => updateTagValue(key, e.target.value)}
                                placeholder="e.g., Production, Team-Alpha, CC-1234" 
                              />
                            </div>
                            <div className="form-group" style={{ display: 'flex', alignItems: 'flex-end' }}>
                              <button 
                                type="button" 
                                onClick={() => removeTag(key)}
                                className="btn-secondary"
                                style={{ marginBottom: '0' }}
                              >
                                ❌ Remove
                              </button>
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                    
                    <button 
                      type="button" 
                      onClick={addTag}
                      className="btn-secondary"
                      style={{ marginTop: '10px' }}
                    >
                      ➕ Add Tag
                    </button>
                    
                    <div className="info-banner" style={{ marginTop: '15px' }}>
                      <strong>💡 Common Tags:</strong> Environment (dev/staging/prod), Owner (team name), CostCenter (billing code), Project (project name), ManagedBy (terraform/bicep)
                    </div>
                  </div>
                </>
              )}

              {/* EKS (AWS Elastic Kubernetes Service) Configuration */}
              {getComputePlatform() === 'EKS' && (
                <>
                  <div className="config-subsection">
                    <h4>EKS Cluster Configuration</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>EC2 Instance Type</label>
                        <select value={formData.compute?.instanceType || 't3.medium'}
                          onChange={(e) => handleNestedChange('compute', 'instanceType', e.target.value)}>
                          <optgroup label="General Purpose">
                            <option value="t3.medium">t3.medium (2 vCPU, 4GB RAM)</option>
                            <option value="t3.large">t3.large (2 vCPU, 8GB RAM) - Recommended</option>
                            <option value="m5.large">m5.large (2 vCPU, 8GB RAM)</option>
                            <option value="m5.xlarge">m5.xlarge (4 vCPU, 16GB RAM)</option>
                            <option value="m5.2xlarge">m5.2xlarge (8 vCPU, 32GB RAM)</option>
                          </optgroup>
                          <optgroup label="Compute Optimized">
                            <option value="c5.large">c5.large (2 vCPU, 4GB RAM)</option>
                            <option value="c5.xlarge">c5.xlarge (4 vCPU, 8GB RAM)</option>
                            <option value="c5.2xlarge">c5.2xlarge (8 vCPU, 16GB RAM)</option>
                          </optgroup>
                          <optgroup label="Memory Optimized">
                            <option value="r5.large">r5.large (2 vCPU, 16GB RAM)</option>
                            <option value="r5.xlarge">r5.xlarge (4 vCPU, 32GB RAM)</option>
                          </optgroup>
                        </select>
                      </div>
                      <div className="form-group">
                        <label>Node Group Name</label>
                        <input type="text" value={formData.compute?.nodePoolName || 'ng-1'}
                          onChange={(e) => handleNestedChange('compute', 'nodePoolName', e.target.value)}
                          placeholder="e.g., ng-1, worker-nodes" />
                      </div>
                    </div>
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input type="checkbox" checked={formData.compute?.enableAutoScaling || false}
                          onChange={(e) => handleNestedChange('compute', 'enableAutoScaling', e.target.checked)} />
                        ⚖️ Enable Cluster Autoscaler
                      </label>
                    </div>
                    {formData.compute?.enableAutoScaling && (
                      <div className="form-grid">
                        <div className="form-group">
                          <label>Min Nodes</label>
                          <input type="number" min="1" value={formData.compute?.minInstances || 1}
                            onChange={(e) => handleNestedChange('compute', 'minInstances', parseInt(e.target.value))} />
                        </div>
                        <div className="form-group">
                          <label>Max Nodes</label>
                          <input type="number" min="1" value={formData.compute?.maxInstances || 10}
                            onChange={(e) => handleNestedChange('compute', 'maxInstances', parseInt(e.target.value))} />
                        </div>
                      </div>
                    )}
                  </div>
                  
                  <div className="config-subsection">
                    <h4>Pod Resource Limits</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>CPU Request/Limit</label>
                        <input type="text" value={formData.compute?.cpuLimit || '500m'}
                          onChange={(e) => handleNestedChange('compute', 'cpuLimit', e.target.value)}
                          placeholder="e.g., 500m, 1, 2" />
                        <small className="field-hint">CPU millicores or cores</small>
                      </div>
                      <div className="form-group">
                        <label>Memory Request/Limit</label>
                        <input type="text" value={formData.compute?.memoryLimit || '1Gi'}
                          onChange={(e) => handleNestedChange('compute', 'memoryLimit', e.target.value)}
                          placeholder="e.g., 1Gi, 2Gi, 512Mi" />
                      </div>
                      <div className="form-group">
                        <label>EBS Volume Size</label>
                        <input type="text" value={formData.compute?.storageSize || '20Gi'}
                          onChange={(e) => handleNestedChange('compute', 'storageSize', e.target.value)}
                          placeholder="e.g., 20Gi, 100Gi" />
                      </div>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>AWS-Specific Options</h4>
                    <div className="form-group">
                      <label>Container Image (ECR/Docker Hub)</label>
                      <input type="text" value={formData.compute?.containerImage || ''}
                        onChange={(e) => handleNestedChange('compute', 'containerImage', e.target.value)}
                        placeholder="e.g., 123456789.dkr.ecr.us-east-1.amazonaws.com/myapp:latest" />
                    </div>
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input type="checkbox" checked={formData.compute?.enableSpotInstances || false}
                          onChange={(e) => handleNestedChange('compute', 'enableSpotInstances', e.target.checked)} />
                        💰 Use EC2 Spot Instances (Up to 90% savings)
                      </label>
                    </div>
                  </div>
                  
                  {/* Zero Trust Security Configuration for EKS */}
                  <div className="config-subsection">
                    <h4>🔒 Zero Trust Security Configuration</h4>
                    <p className="subsection-description">Configure comprehensive security controls for production-ready EKS clusters</p>
                    
                    {/* API Server Security */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enablePrivateEndpointEKS !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enablePrivateEndpointEKS', e.target.checked)} 
                        />
                        🔐 Enable Private API Server Endpoint
                      </label>
                      <small className="field-hint">Restricts Kubernetes API server access to VPC only. Disables public endpoint for maximum security.</small>
                    </div>
                    
                    {!formData.infrastructure?.enablePrivateEndpointEKS && (
                      <div className="form-group">
                        <label>Authorized IP Ranges (CIDR, comma-separated)</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.allowedAPIBlocks || ''}
                          onChange={(e) => handleNestedChange('infrastructure', 'allowedAPIBlocks', e.target.value)}
                          placeholder="203.0.113.0/24, 198.51.100.0/24" 
                        />
                        <small className="field-hint">IP blocks allowed to access the public Kubernetes API. Leave empty for unrestricted (not recommended).</small>
                      </div>
                    )}
                    
                    {/* Identity & Access */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableIRSA !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableIRSA', e.target.checked)} 
                        />
                        🎫 Enable IRSA (IAM Roles for Service Accounts)
                      </label>
                      <small className="field-hint">OIDC provider for passwordless authentication. Pods can assume IAM roles without access keys.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enablePodIdentity !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enablePodIdentity', e.target.checked)} 
                        />
                        🆔 Enable EKS Pod Identity
                      </label>
                      <small className="field-hint">Simplified alternative to IRSA. Pods can authenticate to AWS services using Pod Identity Agent.</small>
                    </div>
                    
                    {/* Pod & Container Security */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enablePodSecurity !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enablePodSecurity', e.target.checked)} 
                        />
                        🛡️ Enable Pod Security Standards (Restricted)
                      </label>
                      <small className="field-hint">Enforces Kubernetes Pod Security Standards (baseline/restricted). Blocks privileged containers, host access, and capabilities.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableNetworkPolicies !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableNetworkPolicies', e.target.checked)} 
                        />
                        🌐 Enable Network Policies (VPC CNI)
                      </label>
                      <small className="field-hint">Enable Kubernetes NetworkPolicy enforcement for pod-to-pod traffic isolation (default-deny)</small>
                    </div>
                    
                    {/* Encryption */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableKMSEncryptionEKS !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableKMSEncryptionEKS', e.target.checked)} 
                        />
                        🔐 Enable KMS Encryption (Secrets Envelope Encryption)
                      </label>
                      <small className="field-hint">Encrypt Kubernetes secrets at rest using AWS KMS</small>
                    </div>
                    
                    {formData.infrastructure?.enableKMSEncryptionEKS && (
                      <div className="form-group">
                        <label>KMS Key ID (Optional)</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.eksKMSKeyId || ''}
                          onChange={(e) => handleNestedChange('infrastructure', 'eksKMSKeyId', e.target.value)}
                          placeholder="arn:aws:kms:region:account:key/key-id or alias/key-alias" 
                        />
                        <small className="field-hint">Leave empty to create a new KMS key for EKS secrets encryption</small>
                      </div>
                    )}
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableVPCCNIEncryption !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableVPCCNIEncryption', e.target.checked)} 
                        />
                        🔒 Enable VPC CNI Pod Traffic Encryption
                      </label>
                      <small className="field-hint">Encrypt pod-to-pod traffic using AWS VPC CNI network encryption plugin</small>
                    </div>
                    
                    {/* Compute Options */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableFargateProfiles || false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableFargateProfiles', e.target.checked)} 
                        />
                        ☁️ Enable Fargate Profiles (Serverless Pods)
                      </label>
                      <small className="field-hint">Run pods on AWS Fargate (serverless). No EC2 node management required.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableManagedNodeGroups !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableManagedNodeGroups', e.target.checked)} 
                        />
                        🖥️ Use EKS Managed Node Groups
                      </label>
                      <small className="field-hint">AWS-managed EC2 instances with automatic AMI updates, patches, and scaling</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableSpotInstancesEKS || false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableSpotInstancesEKS', e.target.checked)} 
                        />
                        💰 Enable EC2 Spot Instances for Node Groups
                      </label>
                      <small className="field-hint">Use Spot instances for non-critical workloads (up to 90% cost savings)</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableIMDSv2 !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableIMDSv2', e.target.checked)} 
                        />
                        🛡️ Require IMDSv2 (Instance Metadata Service v2)
                      </label>
                      <small className="field-hint">Enforces session-based IMDS to prevent SSRF attacks. Blocks IMDSv1.</small>
                    </div>
                    
                    {/* Add-ons & Controllers */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableClusterAutoscaler !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableClusterAutoscaler', e.target.checked)} 
                        />
                        ⚖️ Enable Cluster Autoscaler
                      </label>
                      <small className="field-hint">Automatically adjusts the number of nodes based on pod resource requests</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableALBController !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableALBController', e.target.checked)} 
                        />
                        🌐 Enable AWS Load Balancer Controller
                      </label>
                      <small className="field-hint">Manages Application Load Balancers (ALB) for Kubernetes Ingress resources</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableEBSCSI !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableEBSCSI', e.target.checked)} 
                        />
                        💾 Enable EBS CSI Driver
                      </label>
                      <small className="field-hint">Enables EBS volume provisioning for persistent storage (PersistentVolumeClaims)</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableEFSCSI || false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableEFSCSI', e.target.checked)} 
                        />
                        📁 Enable EFS CSI Driver
                      </label>
                      <small className="field-hint">Enables Amazon EFS for shared persistent storage across multiple pods</small>
                    </div>
                    
                    {/* Monitoring & Threat Detection */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableContainerInsights !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableContainerInsights', e.target.checked)} 
                        />
                        📊 Enable Container Insights (CloudWatch)
                      </label>
                      <small className="field-hint">Collects metrics and logs from EKS cluster, nodes, and pods. Performance monitoring and diagnostics.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableGuardDutyEKS !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableGuardDutyEKS', e.target.checked)} 
                        />
                        🛡️ Enable GuardDuty for EKS (Runtime Protection)
                      </label>
                      <small className="field-hint">Continuous threat detection for EKS workloads. Detects anomalous API calls, privilege escalation, and cryptocurrency mining.</small>
                    </div>
                    
                    {/* Private Networking */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableECRPrivateEndpoint !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableECRPrivateEndpoint', e.target.checked)} 
                        />
                        🔐 Enable Private Endpoint for ECR (PrivateLink)
                      </label>
                      <small className="field-hint">Access ECR via VPC endpoint. Eliminates internet egress for container image pulls.</small>
                    </div>
                    
                    <div className="info-banner">
                      <strong>ℹ️ Zero Trust Architecture:</strong> These settings implement comprehensive Zero Trust security including private API server, IRSA (OIDC), Pod Security Standards (restricted), Network Policies (VPC CNI), KMS encryption for secrets, GuardDuty runtime protection, Container Insights monitoring, IMDSv2, and ECR private endpoint. All critical features are enabled by default for maximum security.
                    </div>
                  </div>

                  {/* Resource Tags */}
                  <div className="config-subsection">
                    <h4>🏷️ Resource Tags</h4>
                    <p className="subsection-description">Add custom tags to organize and track AWS resources (key-value pairs)</p>
                    
                    <div className="tag-list">
                      {Object.entries(formData.infrastructure?.tags || {}).map(([key, value]) => (
                        <div key={key} className="tag-row">
                          <div className="form-grid" style={{ gridTemplateColumns: '1fr 1fr auto' }}>
                            <div className="form-group">
                              <label>Key</label>
                              <input 
                                type="text" 
                                value={key}
                                onChange={(e) => updateTagKey(key, e.target.value)}
                                placeholder="e.g., Environment, Owner, CostCenter" 
                              />
                            </div>
                            <div className="form-group">
                              <label>Value</label>
                              <input 
                                type="text" 
                                value={value}
                                onChange={(e) => updateTagValue(key, e.target.value)}
                                placeholder="e.g., Production, Team-Alpha, CC-1234" 
                              />
                            </div>
                            <div className="form-group" style={{ display: 'flex', alignItems: 'flex-end' }}>
                              <button 
                                type="button" 
                                onClick={() => removeTag(key)}
                                className="btn-secondary"
                                style={{ marginBottom: '0' }}
                              >
                                ❌ Remove
                              </button>
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                    
                    <button 
                      type="button" 
                      onClick={addTag}
                      className="btn-secondary"
                      style={{ marginTop: '10px' }}
                    >
                      ➕ Add Tag
                    </button>
                    
                    <div className="info-banner" style={{ marginTop: '15px' }}>
                      <strong>💡 Common Tags:</strong> Environment (dev/staging/prod), Owner (team name), CostCenter (billing code), Project (project name), ManagedBy (terraform)
                    </div>
                  </div>
                </>
              )}

              {/* ECS (AWS Elastic Container Service) Configuration */}
              {getComputePlatform() === 'ECS' && (
                <>
                  <div className="config-subsection">
                    <h4>ECS Task Configuration</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>Launch Type</label>
                        <select value={formData.compute?.instanceType || 'FARGATE'}
                          onChange={(e) => handleNestedChange('compute', 'instanceType', e.target.value)}>
                          <option value="FARGATE">Fargate (Serverless)</option>
                          <option value="EC2">EC2 (Self-managed)</option>
                          <option value="EXTERNAL">External (On-premises)</option>
                        </select>
                        <small className="field-hint">Fargate is recommended for most workloads</small>
                      </div>
                    </div>
                  </div>
                  
                  <div className="config-subsection">
                    <h4>Task Resources</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>Task CPU</label>
                        <select value={formData.compute?.cpuLimit || '512'}
                          onChange={(e) => handleNestedChange('compute', 'cpuLimit', e.target.value)}>
                          <option value="256">0.25 vCPU (256)</option>
                          <option value="512">0.5 vCPU (512)</option>
                          <option value="1024">1 vCPU (1024)</option>
                          <option value="2048">2 vCPU (2048)</option>
                          <option value="4096">4 vCPU (4096)</option>
                        </select>
                      </div>
                      <div className="form-group">
                        <label>Task Memory</label>
                        <select value={formData.compute?.memoryLimit || '1024'}
                          onChange={(e) => handleNestedChange('compute', 'memoryLimit', e.target.value)}>
                          <option value="512">512 MB</option>
                          <option value="1024">1 GB</option>
                          <option value="2048">2 GB</option>
                          <option value="4096">4 GB</option>
                          <option value="8192">8 GB</option>
                        </select>
                      </div>
                      <div className="form-group">
                        <label>Storage (Ephemeral)</label>
                        <input type="text" value={formData.compute?.storageSize || '20Gi'}
                          onChange={(e) => handleNestedChange('compute', 'storageSize', e.target.value)}
                          placeholder="e.g., 20Gi, 200Gi" />
                      </div>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>Service Auto Scaling</h4>
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input type="checkbox" checked={formData.compute?.enableAutoScaling || false}
                          onChange={(e) => handleNestedChange('compute', 'enableAutoScaling', e.target.checked)} />
                        ⚖️ Enable Service Auto Scaling
                      </label>
                    </div>
                    {formData.compute?.enableAutoScaling && (
                      <div className="form-grid">
                        <div className="form-group">
                          <label>Minimum Tasks</label>
                          <input type="number" min="1" value={formData.compute?.minInstances || 2}
                            onChange={(e) => handleNestedChange('compute', 'minInstances', parseInt(e.target.value))} />
                        </div>
                        <div className="form-group">
                          <label>Maximum Tasks</label>
                          <input type="number" min="1" value={formData.compute?.maxInstances || 10}
                            onChange={(e) => handleNestedChange('compute', 'maxInstances', parseInt(e.target.value))} />
                        </div>
                      </div>
                    )}
                    <div className="form-group">
                      <label>Container Image (ECR)</label>
                      <input type="text" value={formData.compute?.containerImage || ''}
                        onChange={(e) => handleNestedChange('compute', 'containerImage', e.target.value)}
                        placeholder="e.g., 123456789.dkr.ecr.us-east-1.amazonaws.com/app:latest" />
                    </div>
                  </div>
                  
                  {/* Zero Trust Security Configuration for ECS */}
                  <div className="config-subsection">
                    <h4>🔒 Zero Trust Security Configuration</h4>
                    <p className="subsection-description">Configure comprehensive security controls for production-ready ECS clusters</p>
                    
                    {/* Service Security */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableServiceConnect !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableServiceConnect', e.target.checked)} 
                        />
                        🔗 Enable Service Connect (Service Mesh for ECS)
                      </label>
                      <small className="field-hint">AWS Cloud Map service discovery and encrypted service-to-service communication</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableECSExec !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableECSExec', e.target.checked)} 
                        />
                        🖥️ Enable ECS Exec (Secure Task Access)
                      </label>
                      <small className="field-hint">KMS-encrypted session manager access for debugging. Requires IAM permissions and CloudWatch logging.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableSecretsManager !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableSecretsManager', e.target.checked)} 
                        />
                        🔐 Use AWS Secrets Manager for Environment Variables
                      </label>
                      <small className="field-hint">Store secrets in Secrets Manager instead of plaintext environment variables</small>
                    </div>
                    
                    {/* Container Security */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableReadOnlyRootFS !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableReadOnlyRootFS', e.target.checked)} 
                        />
                        🛡️ Enable Read-Only Root Filesystem
                      </label>
                      <small className="field-hint">Prevent container from writing to root filesystem (immutable infrastructure)</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableDropCapabilities !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableDropCapabilities', e.target.checked)} 
                        />
                        ⚠️ Drop All Linux Capabilities (Least Privilege)
                      </label>
                      <small className="field-hint">Remove all Linux kernel capabilities (CAP_ALL) for maximum security</small>
                    </div>
                    
                    {/* Load Balancer & Network Security */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.httpsListener !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'httpsListener', e.target.checked)} 
                        />
                        🔒 Enable HTTPS Listener (TLS 1.3)
                      </label>
                      <small className="field-hint">HTTP redirects to HTTPS. Requires SSL certificate ARN.</small>
                    </div>
                    
                    {formData.infrastructure?.httpsListener && (
                      <div className="form-group">
                        <label>SSL Certificate ARN (AWS Certificate Manager)</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.sslCertificateArn || ''}
                          onChange={(e) => handleNestedChange('infrastructure', 'sslCertificateArn', e.target.value)}
                          placeholder="arn:aws:acm:region:account:certificate/certificate-id" 
                        />
                        <small className="field-hint">TLS 1.3 policy: ELBSecurityPolicy-TLS13-1-2-2021-06</small>
                      </div>
                    )}
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableWAF !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableWAF', e.target.checked)} 
                        />
                        🛡️ Enable AWS WAF (Web Application Firewall)
                      </label>
                      <small className="field-hint">Protect against OWASP Top 10 vulnerabilities and DDoS attacks</small>
                    </div>
                    
                    {formData.infrastructure?.enableWAF && (
                      <div className="form-group">
                        <label>Web ACL ARN (Optional)</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.webAclArn || ''}
                          onChange={(e) => handleNestedChange('infrastructure', 'webAclArn', e.target.value)}
                          placeholder="arn:aws:wafv2:region:account:regional/webacl/name/id" 
                        />
                        <small className="field-hint">Leave empty to create a new WAF with managed rules (Core Rule Set, Known Bad Inputs, SQL Injection, IP Reputation)</small>
                      </div>
                    )}
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableNetworkIsolation !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableNetworkIsolation', e.target.checked)} 
                        />
                        🔒 Enable Network Isolation (Private Subnets Only)
                      </label>
                      <small className="field-hint">Tasks run in private subnets with no public IP addresses. Internet access via NAT Core.</small>
                    </div>
                    
                    <div className="form-group">
                      <label>Allowed CIDR Blocks (ALB Security Group)</label>
                      <input 
                        type="text" 
                        value={formData.infrastructure?.allowedCIDRBlocks || ''}
                        onChange={(e) => handleNestedChange('infrastructure', 'allowedCIDRBlocks', e.target.value)}
                        placeholder="0.0.0.0/0 (or restrict to specific IPs: 203.0.113.0/24, 198.51.100.0/24)" 
                      />
                      <small className="field-hint">CIDR blocks allowed to access the load balancer. For internal-only access, leave empty and use VPC CIDR.</small>
                    </div>
                    
                    {/* VPC & Private Networking */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableVPCEndpoints !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableVPCEndpoints', e.target.checked)} 
                        />
                        🌐 Enable VPC Endpoints (PrivateLink)
                      </label>
                      <small className="field-hint">Creates VPC endpoints for ECR (dkr/api), S3, CloudWatch Logs, and Secrets Manager. Eliminates internet egress.</small>
                    </div>
                    
                    {/* Container Image Security */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableECRScanning !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableECRScanning', e.target.checked)} 
                        />
                        🔍 Enable ECR Image Scanning (Vulnerability Detection)
                      </label>
                      <small className="field-hint">Automatically scan container images for CVEs on push (enhanced scanning with Amazon Inspector)</small>
                    </div>
                    
                    <div className="form-group">
                      <label>Allowed Container Registries (Comma-separated)</label>
                      <input 
                        type="text" 
                        value={formData.infrastructure?.allowedRegistries || ''}
                        onChange={(e) => handleNestedChange('infrastructure', 'allowedRegistries', e.target.value)}
                        placeholder="123456789.dkr.ecr.region.amazonaws.com, public.ecr.aws" 
                      />
                      <small className="field-hint">Restrict container images to trusted registries. Leave empty to allow all ECR registries.</small>
                    </div>
                    
                    {/* Encryption & Audit */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableKMSEncryption !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableKMSEncryption', e.target.checked)} 
                        />
                        🔐 Enable KMS Encryption (CloudWatch Logs & ECS Exec)
                      </label>
                      <small className="field-hint">Encrypt CloudWatch Logs and ECS Exec sessions with AWS KMS</small>
                    </div>
                    
                    {formData.infrastructure?.enableKMSEncryption && (
                      <div className="form-group">
                        <label>KMS Key ID (Optional)</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.kmsKeyId || ''}
                          onChange={(e) => handleNestedChange('infrastructure', 'kmsKeyId', e.target.value)}
                          placeholder="arn:aws:kms:region:account:key/key-id or alias/key-alias" 
                        />
                        <small className="field-hint">Leave empty to create a new KMS key automatically. Used for CloudWatch Logs, ECS Exec, and ECR encryption.</small>
                      </div>
                    )}
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableGuardDuty !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableGuardDuty', e.target.checked)} 
                        />
                        🛡️ Enable Amazon GuardDuty (Threat Detection)
                      </label>
                      <small className="field-hint">Continuous monitoring for malicious activity and unauthorized behavior (runtime protection, ECS Runtime Monitoring)</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableCloudTrail !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableCloudTrail', e.target.checked)} 
                        />
                        📝 Enable AWS CloudTrail (Audit Logging)
                      </label>
                      <small className="field-hint">Track all ECS API calls for compliance and security auditing. Logs stored in encrypted S3 bucket.</small>
                    </div>
                    
                    <div className="info-banner">
                      <strong>ℹ️ Zero Trust Architecture:</strong> These settings implement comprehensive Zero Trust security including VPC endpoints (PrivateLink), AWS WAF, GuardDuty threat detection, CloudTrail audit logging, ECR image scanning, Service Connect encrypted mesh, read-only root filesystem, and dropped Linux capabilities. All features are enabled by default for maximum security.
                    </div>
                  </div>
                </>
              )}

              {/* App Service (Azure Web App) Configuration */}
              {getComputePlatform() === 'AppService' && (
                <>
                  <div className="config-subsection">
                    <h4>App Service Plan Configuration</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>SKU / Pricing Tier</label>
                        <select value={formData.compute?.instanceType || 'B1'}
                          onChange={(e) => handleNestedChange('compute', 'instanceType', e.target.value)}>
                          <optgroup label="Free & Shared">
                            <option value="F1">F1 Free (60 min/day, 1GB RAM)</option>
                            <option value="D1">D1 Shared (240 min/day, 1GB RAM)</option>
                          </optgroup>
                          <optgroup label="Basic">
                            <option value="B1">B1 Basic (1 Core, 1.75GB RAM)</option>
                            <option value="B2">B2 Basic (2 Cores, 3.5GB RAM)</option>
                            <option value="B3">B3 Basic (4 Cores, 7GB RAM)</option>
                          </optgroup>
                          <optgroup label="Standard">
                            <option value="S1">S1 Standard (1 Core, 1.75GB RAM)</option>
                            <option value="S2">S2 Standard (2 Cores, 3.5GB RAM)</option>
                            <option value="S3">S3 Standard (4 Cores, 7GB RAM)</option>
                          </optgroup>
                          <optgroup label="Premium">
                            <option value="P1v2">P1v2 Premium (1 Core, 3.5GB RAM)</option>
                            <option value="P2v2">P2v2 Premium (2 Cores, 7GB RAM)</option>
                            <option value="P3v2">P3v2 Premium (4 Cores, 14GB RAM)</option>
                          </optgroup>
                        </select>
                      </div>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>Scaling Configuration</h4>
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input type="checkbox" checked={formData.compute?.enableAutoScaling || false}
                          onChange={(e) => handleNestedChange('compute', 'enableAutoScaling', e.target.checked)} />
                        ⚖️ Enable Auto Scale-Out
                      </label>
                    </div>
                    {formData.compute?.enableAutoScaling && (
                      <div className="form-grid">
                        <div className="form-group">
                          <label>Minimum Instances</label>
                          <input type="number" min="1" value={formData.compute?.minInstances || 1}
                            onChange={(e) => handleNestedChange('compute', 'minInstances', parseInt(e.target.value))} />
                        </div>
                        <div className="form-group">
                          <label>Maximum Instances</label>
                          <input type="number" min="1" max="30" value={formData.compute?.maxInstances || 5}
                            onChange={(e) => handleNestedChange('compute', 'maxInstances', parseInt(e.target.value))} />
                        </div>
                      </div>
                    )}
                  </div>

                  <div className="config-subsection">
                    <h4>Runtime & Deployment</h4>
                    <div className="form-group">
                      <label>Container Image (Optional)</label>
                      <input type="text" value={formData.compute?.containerImage || ''}
                        onChange={(e) => handleNestedChange('compute', 'containerImage', e.target.value)}
                        placeholder="e.g., mcr.microsoft.com/appsvc/node:18-lts or ACR image" />
                      <small className="field-hint">Leave empty for code deployment, specify for containers</small>
                    </div>
                  </div>

                  {/* Zero Trust Security Configuration for App Service */}
                  <div className="config-subsection">
                    <h4>🔒 Zero Trust Security Configuration</h4>
                    <p className="subsection-description">Configure comprehensive security controls for production-ready App Service</p>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.httpsOnly !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'httpsOnly', e.target.checked)} 
                        />
                        🔐 HTTPS Only (Recommended)
                      </label>
                      <small className="field-hint">Redirect all HTTP traffic to HTTPS for encrypted connections</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableVnetIntegration || false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableVnetIntegration', e.target.checked)} 
                        />
                        🌐 Enable VNet Integration (Premium Required)
                      </label>
                      <small className="field-hint">Deploy into Virtual Network - uses VNet/subnets configured in "Configure Network Settings" section below</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enablePrivateEndpoint || false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enablePrivateEndpoint', e.target.checked)} 
                        />
                        🔒 Enable Private Endpoint (Premium Required)
                      </label>
                      <small className="field-hint">Make app accessible only via private IP in VNet (Zero Trust ingress)</small>
                    </div>
                    
                    <div className="form-group">
                      <label>Minimum TLS Version</label>
                      <select 
                        value={formData.infrastructure?.minTlsVersion || '1.2'}
                        onChange={(e) => handleNestedChange('infrastructure', 'minTlsVersion', e.target.value)}
                      >
                        <option value="1.3">TLS 1.3 (Most Secure)</option>
                        <option value="1.2">TLS 1.2 (Recommended)</option>
                        <option value="1.1">TLS 1.1 (Legacy)</option>
                        <option value="1.0">TLS 1.0 (Not Recommended)</option>
                      </select>
                      <small className="field-hint">Minimum TLS version for HTTPS connections</small>
                    </div>
                    
                    <div className="form-group">
                      <label>FTP Deployment State</label>
                      <select 
                        value={formData.infrastructure?.ftpsState || 'FtpsOnly'}
                        onChange={(e) => handleNestedChange('infrastructure', 'ftpsState', e.target.value)}
                      >
                        <option value="Disabled">Disabled (Most Secure)</option>
                        <option value="FtpsOnly">FTPS Only (Recommended)</option>
                        <option value="AllAllowed">All Allowed (Not Recommended)</option>
                      </select>
                      <small className="field-hint">FTP/FTPS deployment access control</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableManagedIdentity !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableManagedIdentity', e.target.checked)} 
                        />
                        🎫 Enable Managed Identity (Recommended)
                      </label>
                      <small className="field-hint">System-assigned managed identity for passwordless Azure service access</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.observability?.applicationInsights || false}
                          onChange={(e) => handleNestedChange('observability', 'applicationInsights', e.target.checked)} 
                        />
                        📈 Enable Application Insights
                      </label>
                      <small className="field-hint">Application performance monitoring and analytics</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableClientCertificate || false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableClientCertificate', e.target.checked)} 
                        />
                        🔐 Require Client Certificates (Mutual TLS)
                      </label>
                      <small className="field-hint">Enforce client certificate authentication for Zero Trust access</small>
                    </div>
                    
                    {formData.infrastructure?.enableClientCertificate && (
                      <div className="form-group">
                        <label>Client Certificate Mode</label>
                        <select 
                          value={formData.infrastructure?.clientCertMode || 'Optional'}
                          onChange={(e) => handleNestedChange('infrastructure', 'clientCertMode', e.target.value)}
                        >
                          <option value="Required">Required (Enforce mTLS)</option>
                          <option value="Optional">Optional (Allow but not require)</option>
                          <option value="OptionalInteractiveUser">Optional Interactive User</option>
                        </select>
                        <small className="field-hint">Client certificate validation mode</small>
                      </div>
                    )}
                    
                    <div className="info-banner">
                      <strong>ℹ️ Zero Trust Architecture:</strong> These settings implement App Service security best practices including HTTPS enforcement, VNet integration, private endpoints, TLS 1.2+, managed identity, and optional client certificates. Premium SKU required for advanced networking features.
                    </div>
                  </div>
                </>
              )}

              {/* Virtual Machine (Azure Virtual Machines) Configuration */}
              {getComputePlatform() === 'VirtualMachine' && (
                <>
                  <div className="config-subsection">
                    <h4>Virtual Machine Configuration</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>Virtual Machine Size</label>
                        <select value={formData.compute?.instanceType || 'Standard_D2s_v3'}
                          onChange={(e) => handleNestedChange('compute', 'instanceType', e.target.value)}>
                          <optgroup label="General Purpose">
                            <option value="Standard_B2s">Standard_B2s (2 vCPU, 4GB RAM) - Burstable</option>
                            <option value="Standard_B2ms">Standard_B2ms (2 vCPU, 8GB RAM) - Burstable</option>
                            <option value="Standard_D2s_v3">Standard_D2s_v3 (2 vCPU, 8GB RAM) - Recommended</option>
                            <option value="Standard_D4s_v3">Standard_D4s_v3 (4 vCPU, 16GB RAM)</option>
                            <option value="Standard_D8s_v3">Standard_D8s_v3 (8 vCPU, 32GB RAM)</option>
                            <option value="Standard_D16s_v3">Standard_D16s_v3 (16 vCPU, 64GB RAM)</option>
                          </optgroup>
                          <optgroup label="Compute Optimized">
                            <option value="Standard_F2s_v2">Standard_F2s_v2 (2 vCPU, 4GB RAM)</option>
                            <option value="Standard_F4s_v2">Standard_F4s_v2 (4 vCPU, 8GB RAM)</option>
                            <option value="Standard_F8s_v2">Standard_F8s_v2 (8 vCPU, 16GB RAM)</option>
                            <option value="Standard_F16s_v2">Standard_F16s_v2 (16 vCPU, 32GB RAM)</option>
                          </optgroup>
                          <optgroup label="Memory Optimized">
                            <option value="Standard_E2s_v3">Standard_E2s_v3 (2 vCPU, 16GB RAM)</option>
                            <option value="Standard_E4s_v3">Standard_E4s_v3 (4 vCPU, 32GB RAM)</option>
                            <option value="Standard_E8s_v3">Standard_E8s_v3 (8 vCPU, 64GB RAM)</option>
                            <option value="Standard_E16s_v3">Standard_E16s_v3 (16 vCPU, 128GB RAM)</option>
                          </optgroup>
                        </select>
                      </div>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>Operating System</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>OS Type</label>
                        <select value={formData.compute?.cpuLimit || 'Linux'}
                          onChange={(e) => handleNestedChange('compute', 'cpuLimit', e.target.value)}>
                          <option value="Linux">Linux (Ubuntu, RHEL, Debian)</option>
                          <option value="Windows">Windows Server</option>
                        </select>
                      </div>
                      <div className="form-group">
                        <label>OS Disk Type</label>
                        <select value={formData.compute?.storageSize || 'Premium_LRS'}
                          onChange={(e) => handleNestedChange('compute', 'storageSize', e.target.value)}>
                          <option value="Standard_LRS">Standard HDD (Lowest cost)</option>
                          <option value="StandardSSD_LRS">Standard SSD</option>
                          <option value="Premium_LRS">Premium SSD (Recommended)</option>
                          <option value="Premium_ZRS">Premium SSD (Zone-redundant)</option>
                        </select>
                      </div>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>Availability & Scaling</h4>
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input type="checkbox" checked={formData.compute?.enableAutoScaling || false}
                          onChange={(e) => handleNestedChange('compute', 'enableAutoScaling', e.target.checked)} />
                        ⚖️ Enable Virtual Machine Scale Sets (Auto-scaling)
                      </label>
                    </div>
                    {formData.compute?.enableAutoScaling && (
                      <div className="form-grid">
                        <div className="form-group">
                          <label>Minimum Virtual Machine Instances</label>
                          <input type="number" min="1" value={formData.compute?.minInstances || 1}
                            onChange={(e) => handleNestedChange('compute', 'minInstances', parseInt(e.target.value))} />
                        </div>
                        <div className="form-group">
                          <label>Maximum Virtual Machine Instances</label>
                          <input type="number" min="1" max="100" value={formData.compute?.maxInstances || 10}
                            onChange={(e) => handleNestedChange('compute', 'maxInstances', parseInt(e.target.value))} />
                        </div>
                      </div>
                    )}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input type="checkbox" checked={formData.compute?.enableSpotInstances || false}
                          onChange={(e) => handleNestedChange('compute', 'enableSpotInstances', e.target.checked)} />
                        💰 Use Spot Virtual Machines (Up to 90% cost savings)
                      </label>
                      <small className="field-hint">Spot Virtual Machines can be evicted when Azure needs capacity</small>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>Data Disks (Optional)</h4>
                    <div className="form-group">
                      <label>Container Image / Custom Image</label>
                      <input type="text" value={formData.compute?.containerImage || ''}
                        onChange={(e) => handleNestedChange('compute', 'containerImage', e.target.value)}
                        placeholder="e.g., /subscriptions/.../images/myimage or marketplace image" />
                      <small className="field-hint">Leave empty to use marketplace images (Ubuntu, Windows Server)</small>
                    </div>
                  </div>
                </>
              )}

              {/* Container Apps (Azure) Configuration */}
              {getComputePlatform() === 'ContainerApps' && (
                <>
                  <div className="config-subsection">
                    <h4>Container Apps Configuration</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>CPU Allocation (vCPU)</label>
                        <select value={formData.compute?.cpuLimit || '0.5'}
                          onChange={(e) => handleNestedChange('compute', 'cpuLimit', e.target.value)}>
                          <option value="0.25">0.25 vCPU</option>
                          <option value="0.5">0.5 vCPU (Recommended)</option>
                          <option value="0.75">0.75 vCPU</option>
                          <option value="1">1 vCPU</option>
                          <option value="1.25">1.25 vCPU</option>
                          <option value="1.5">1.5 vCPU</option>
                          <option value="1.75">1.75 vCPU</option>
                          <option value="2">2 vCPU</option>
                          <option value="2.5">2.5 vCPU</option>
                          <option value="3">3 vCPU</option>
                          <option value="3.5">3.5 vCPU</option>
                          <option value="4">4 vCPU</option>
                        </select>
                      </div>
                      <div className="form-group">
                        <label>Memory</label>
                        <select value={formData.compute?.memoryLimit || '1Gi'}
                          onChange={(e) => handleNestedChange('compute', 'memoryLimit', e.target.value)}>
                          <option value="0.5Gi">0.5 GiB</option>
                          <option value="1Gi">1 GiB (Recommended)</option>
                          <option value="1.5Gi">1.5 GiB</option>
                          <option value="2Gi">2 GiB</option>
                          <option value="3Gi">3 GiB</option>
                          <option value="4Gi">4 GiB</option>
                          <option value="6Gi">6 GiB</option>
                          <option value="8Gi">8 GiB</option>
                        </select>
                      </div>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>Replica Scaling</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>Minimum Replicas</label>
                        <input type="number" min="0" max="30" value={formData.compute?.minInstances || 0}
                          onChange={(e) => handleNestedChange('compute', 'minInstances', parseInt(e.target.value))} />
                        <small className="field-hint">0 = scale to zero when idle (save costs)</small>
                      </div>
                      <div className="form-group">
                        <label>Maximum Replicas</label>
                        <input type="number" min="1" max="30" value={formData.compute?.maxInstances || 10}
                          onChange={(e) => handleNestedChange('compute', 'maxInstances', parseInt(e.target.value))} />
                        <small className="field-hint">Max 30 replicas per Container App</small>
                      </div>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>Container Configuration</h4>
                    <div className="form-group">
                      <label>Container Image (Required)</label>
                      <input type="text" value={formData.compute?.containerImage || ''}
                        onChange={(e) => handleNestedChange('compute', 'containerImage', e.target.value)}
                        placeholder="e.g., myregistry.azurecr.io/myapp:v1.0.0" />
                      <small className="field-hint">Use Azure Container Registry (ACR) for best integration</small>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>Managed Environment</h4>
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input type="checkbox" checked={formData.compute?.enableAutoScaling || false}
                          onChange={(e) => handleNestedChange('compute', 'enableAutoScaling', e.target.checked)} />
                        🌐 Enable VNet Integration
                      </label>
                      <small className="field-hint">Deploy Container Apps in your own VNet for private networking</small>
                    </div>
                  </div>

                  {/* Zero Trust Security Configuration for Container Apps */}
                  <div className="config-subsection">
                    <h4>🔒 Zero Trust Security Configuration</h4>
                    <p className="subsection-description">Configure security controls for production-ready Container Apps environments</p>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enablePrivateEndpointCA || false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enablePrivateEndpointCA', e.target.checked)} 
                        />
                        🔐 Enable Private Endpoint (Premium)
                      </label>
                      <small className="field-hint">Container Apps Environment accessible only via private network. Eliminates public endpoints for maximum security.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableManagedIdentityCA !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableManagedIdentityCA', e.target.checked)} 
                        />
                        🎫 Enable Managed Identity
                      </label>
                      <small className="field-hint">Enables passwordless authentication for Container Apps using Azure AD. Apps can access Azure resources without managing secrets.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableIPRestrictionsCA || false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableIPRestrictionsCA', e.target.checked)} 
                        />
                        🛡️ Enable IP Restrictions
                      </label>
                      <small className="field-hint">Restrict ingress access to specific IP ranges. Configure allowed IP ranges in environment settings.</small>
                    </div>
                  </div>
                </>
              )}

              {/* Cloud Run (GCP) Configuration */}
              {getComputePlatform() === 'Cloud Run' && (
                <>
                  <div className="config-subsection">
                    <h4>Cloud Run Service Configuration</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>CPU Allocation</label>
                        <select value={formData.compute?.cpuLimit || '1'}
                          onChange={(e) => handleNestedChange('compute', 'cpuLimit', e.target.value)}>
                          <option value="1">1 vCPU</option>
                          <option value="2">2 vCPU</option>
                          <option value="4">4 vCPU</option>
                          <option value="8">8 vCPU</option>
                        </select>
                      </div>
                      <div className="form-group">
                        <label>Memory</label>
                        <select value={formData.compute?.memoryLimit || '512Mi'}
                          onChange={(e) => handleNestedChange('compute', 'memoryLimit', e.target.value)}>
                          <option value="128Mi">128 MiB</option>
                          <option value="256Mi">256 MiB</option>
                          <option value="512Mi">512 MiB</option>
                          <option value="1Gi">1 GiB</option>
                          <option value="2Gi">2 GiB</option>
                          <option value="4Gi">4 GiB</option>
                          <option value="8Gi">8 GiB</option>
                        </select>
                      </div>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>Auto Scaling</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>Minimum Instances</label>
                        <input type="number" min="0" value={formData.compute?.minInstances || 0}
                          onChange={(e) => handleNestedChange('compute', 'minInstances', parseInt(e.target.value))} />
                        <small className="field-hint">0 = scale to zero when idle</small>
                      </div>
                      <div className="form-group">
                        <label>Maximum Instances</label>
                        <input type="number" min="1" value={formData.compute?.maxInstances || 100}
                          onChange={(e) => handleNestedChange('compute', 'maxInstances', parseInt(e.target.value))} />
                      </div>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>Container Configuration</h4>
                    <div className="form-group">
                      <label>Container Image (GCR/Artifact Registry)</label>
                      <input type="text" value={formData.compute?.containerImage || ''}
                        onChange={(e) => handleNestedChange('compute', 'containerImage', e.target.value)}
                        placeholder="e.g., gcr.io/project-id/image:tag" />
                    </div>
                  </div>
                  
                  {/* Zero Trust Security Configuration for Cloud Run */}
                  <div className="config-subsection">
                    <h4>🔒 Zero Trust Security Configuration</h4>
                    <p className="subsection-description">Configure comprehensive security controls for production-ready Cloud Run services</p>
                    
                    {/* Network Security */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableVPCConnector !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableVPCConnector', e.target.checked)} 
                        />
                        🌐 Enable VPC Connector (Private Networking)
                      </label>
                      <small className="field-hint">Connect Cloud Run to VPC for access to private resources (Cloud SQL, Memorystore, Compute Engine)</small>
                    </div>
                    
                    <div className="form-group">
                      <label>Ingress Settings</label>
                      <select 
                        value={formData.infrastructure?.ingressSettings || 'internal-and-cloud-load-balancing'}
                        onChange={(e) => handleNestedChange('infrastructure', 'ingressSettings', e.target.value)}
                      >
                        <option value="all">All traffic (public internet)</option>
                        <option value="internal">Internal only (VPC)</option>
                        <option value="internal-and-cloud-load-balancing">Internal + Cloud Load Balancing (Recommended)</option>
                      </select>
                      <small className="field-hint">Control who can reach your Cloud Run service</small>
                    </div>
                    
                    {formData.infrastructure?.enableVPCEgress && (
                      <div className="form-group">
                        <label>Egress Settings</label>
                        <select 
                          value={formData.infrastructure?.egressSettings || 'private-ranges-only'}
                          onChange={(e) => handleNestedChange('infrastructure', 'egressSettings', e.target.value)}
                        >
                          <option value="all-traffic">All traffic</option>
                          <option value="private-ranges-only">Private ranges only (Recommended)</option>
                        </select>
                        <small className="field-hint">Route outbound traffic through VPC (private IP ranges only recommended)</small>
                      </div>
                    )}
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableHTTPSOnly !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableHTTPSOnly', e.target.checked)} 
                        />
                        🔒 Enforce HTTPS Only (Redirect HTTP)
                      </label>
                      <small className="field-hint">Automatically redirect HTTP requests to HTTPS</small>
                    </div>
                    
                    <div className="form-group">
                      <label>Allowed Ingress Sources (comma-separated IPs/CIDR)</label>
                      <input 
                        type="text" 
                        value={formData.infrastructure?.allowedIngressSources || ''}
                        onChange={(e) => handleNestedChange('infrastructure', 'allowedIngressSources', e.target.value)}
                        placeholder="0.0.0.0/0 or restrict: 203.0.113.0/24, 198.51.100.0/24" 
                      />
                      <small className="field-hint">Leave empty to allow all traffic (if ingress=all). Use with Cloud Armor for granular control.</small>
                    </div>
                    
                    {/* Identity & Access */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableServiceIdentity !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableServiceIdentity', e.target.checked)} 
                        />
                        🆔 Enable Service Identity (Workload Identity Federation)
                      </label>
                      <small className="field-hint">Assign unique service account to Cloud Run service for accessing GCP resources without keys</small>
                    </div>
                    
                    {/* Container Security & Compliance */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableBinaryAuthorization !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableBinaryAuthorization', e.target.checked)} 
                        />
                        ✅ Enable Binary Authorization (Container Attestation)
                      </label>
                      <small className="field-hint">Deploy only signed and verified container images. Enforces attestation policies.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableExecutionEnvironmentV2 !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableExecutionEnvironmentV2', e.target.checked)} 
                        />
                        🚀 Enable Execution Environment V2 (gVisor Sandboxing)
                      </label>
                      <small className="field-hint">Enhanced security isolation using gVisor. Better syscall filtering and memory safety.</small>
                    </div>
                    
                    {/* Application Security */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableCloudArmor !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableCloudArmor', e.target.checked)} 
                        />
                        🛡️ Enable Cloud Armor (DDoS & WAF Protection)
                      </label>
                      <small className="field-hint">Google's DDoS protection and web application firewall (OWASP Top 10, rate limiting, geo-fencing)</small>
                    </div>
                    
                    <div className="form-group">
                      <label>Max Instance Request Concurrency</label>
                      <input 
                        type="number" 
                        min="1" 
                        max="1000"
                        value={formData.infrastructure?.maxInstanceConcurrency || 80}
                        onChange={(e) => handleNestedChange('infrastructure', 'maxInstanceConcurrency', parseInt(e.target.value))}
                        placeholder="80" 
                      />
                      <small className="field-hint">Maximum concurrent requests per instance (1-1000). Lower values = more instances, better isolation.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableSessionAffinity !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableSessionAffinity', e.target.checked)} 
                        />
                        🔗 Enable Session Affinity (Sticky Sessions)
                      </label>
                      <small className="field-hint">Route requests from the same client to the same instance</small>
                    </div>
                    
                    {/* Encryption */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableCMEK !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableCMEK', e.target.checked)} 
                        />
                        🔐 Enable CMEK (Customer-Managed Encryption Keys)
                      </label>
                      <small className="field-hint">Encrypt Cloud Run data with your own Cloud KMS keys (environment variables, secrets)</small>
                    </div>
                    
                    {formData.infrastructure?.enableCMEK && (
                      <div className="form-group">
                        <label>Cloud KMS Key ID</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.cloudRunKMSKeyId || ''}
                          onChange={(e) => handleNestedChange('infrastructure', 'cloudRunKMSKeyId', e.target.value)}
                          placeholder="projects/PROJECT_ID/locations/LOCATION/keyRings/KEYRING/cryptoKeys/KEY" 
                        />
                        <small className="field-hint">Leave empty to create a new Cloud KMS key for Cloud Run encryption</small>
                      </div>
                    )}
                    
                    {/* Monitoring & Audit */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableCloudAuditLogs !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableCloudAuditLogs', e.target.checked)} 
                        />
                        📝 Enable Cloud Audit Logs
                      </label>
                      <small className="field-hint">Track all Cloud Run API calls, configuration changes, and access logs</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableCloudMonitoring !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableCloudMonitoring', e.target.checked)} 
                        />
                        📊 Enable Cloud Monitoring (Metrics & Logs)
                      </label>
                      <small className="field-hint">Automatic metrics collection for request count, latency, errors, and resource usage</small>
                    </div>
                    
                    {/* Performance & Resource Management */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableCPUThrottling === false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableCPUThrottling', !e.target.checked)} 
                        />
                        ⚡ Disable CPU Throttling (CPU Always Allocated)
                      </label>
                      <small className="field-hint">Keep CPU allocated during request processing (better performance, higher cost)</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableStartupCPUBoost !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableStartupCPUBoost', e.target.checked)} 
                        />
                        🚀 Enable Startup CPU Boost
                      </label>
                      <small className="field-hint">Allocate extra CPU during container startup for faster cold starts</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableVPCEgress !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableVPCEgress', e.target.checked)} 
                        />
                        🌐 Enable VPC Egress (Route Outbound Traffic via VPC)
                      </label>
                      <small className="field-hint">Send all outbound traffic through VPC connector for private egress</small>
                    </div>
                    
                    <div className="info-banner">
                      <strong>ℹ️ Zero Trust Architecture:</strong> These settings implement comprehensive Zero Trust security including VPC Connector (private networking), Binary Authorization (container attestation), Cloud Armor (DDoS & WAF), CMEK encryption, Execution Environment V2 (gVisor sandboxing), Cloud Audit Logs, ingress restrictions (internal-only/Cloud LB), HTTPS enforcement, and Workload Identity Federation. All critical features are enabled by default for maximum security.
                    </div>
                  </div>
                </>
              )}

              {/* Lambda (AWS Serverless) Configuration */}
              {getComputePlatform() === 'Lambda' && (
                <>
                  <div className="config-subsection">
                    <h4>Lambda Function Configuration</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>Memory (MB)</label>
                        <select value={formData.compute?.memoryLimit || '512'}
                          onChange={(e) => handleNestedChange('compute', 'memoryLimit', e.target.value)}>
                          <option value="128">128 MB</option>
                          <option value="256">256 MB</option>
                          <option value="512">512 MB (Recommended)</option>
                          <option value="1024">1024 MB (1 GB)</option>
                          <option value="2048">2048 MB (2 GB)</option>
                          <option value="3008">3008 MB (3 GB)</option>
                          <option value="10240">10240 MB (10 GB)</option>
                        </select>
                        <small className="field-hint">CPU scales proportionally with memory</small>
                      </div>
                      <div className="form-group">
                        <label>Timeout (seconds)</label>
                        <input type="number" min="1" max="900" value={formData.compute?.cpuLimit || '30'}
                          onChange={(e) => handleNestedChange('compute', 'cpuLimit', e.target.value)}
                          placeholder="1-900" />
                        <small className="field-hint">Max 15 minutes (900 seconds)</small>
                      </div>
                      <div className="form-group">
                        <label>Ephemeral Storage (MB)</label>
                        <input type="number" min="512" max="10240" value={formData.compute?.storageSize || '512'}
                          onChange={(e) => handleNestedChange('compute', 'storageSize', e.target.value)} />
                        <small className="field-hint">/tmp directory size (512 MB - 10 GB)</small>
                      </div>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>Concurrency & Scaling</h4>
                    <div className="form-grid">
                      <div className="form-group">
                        <label>Reserved Concurrency</label>
                        <input type="number" min="0" value={formData.compute?.maxInstances || 0}
                          onChange={(e) => handleNestedChange('compute', 'maxInstances', parseInt(e.target.value))}
                          placeholder="0 = unreserved" />
                        <small className="field-hint">0 = no reservation, scales automatically</small>
                      </div>
                      <div className="form-group">
                        <label>Provisioned Concurrency</label>
                        <input type="number" min="0" value={formData.compute?.minInstances || 0}
                          onChange={(e) => handleNestedChange('compute', 'minInstances', parseInt(e.target.value))}
                          placeholder="0 = on-demand only" />
                        <small className="field-hint">Pre-warmed instances (incurs cost)</small>
                      </div>
                    </div>
                  </div>

                  <div className="config-subsection">
                    <h4>Container Image (Optional)</h4>
                    <div className="form-group">
                      <label>Container Image URI (ECR)</label>
                      <input type="text" value={formData.compute?.containerImage || ''}
                        onChange={(e) => handleNestedChange('compute', 'containerImage', e.target.value)}
                        placeholder="e.g., 123456789.dkr.ecr.region.amazonaws.com/lambda:tag" />
                      <small className="field-hint">Leave empty to use ZIP deployment</small>
                    </div>
                  </div>
                  
                  {/* Zero Trust Security Configuration for Lambda */}
                  <div className="config-subsection">
                    <h4>🔒 Zero Trust Security Configuration</h4>
                    <p className="subsection-description">Configure comprehensive security controls for production-ready Lambda functions</p>
                    
                    {/* Network Security */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableVPCConfig !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableVPCConfig', e.target.checked)} 
                        />
                        🌐 Enable VPC Configuration (Private Networking)
                      </label>
                      <small className="field-hint">Run Lambda in VPC for access to RDS, ElastiCache, and other private resources. Requires NAT Gateway for internet access.</small>
                    </div>
                    
                    {formData.infrastructure?.enableVPCConfig && (
                      <div className="form-group">
                        <label>VPC Subnet IDs (comma-separated)</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.lambdaVPCSubnetIds || ''}
                          onChange={(e) => handleNestedChange('infrastructure', 'lambdaVPCSubnetIds', e.target.value)}
                          placeholder="subnet-abc123, subnet-def456" 
                        />
                        <small className="field-hint">Use private subnets only. Lambda creates ENIs in these subnets.</small>
                      </div>
                    )}
                    
                    {/* Encryption */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableKMSEncryptionLambda !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableKMSEncryptionLambda', e.target.checked)} 
                        />
                        🔐 Enable KMS Encryption (Environment Variables)
                      </label>
                      <small className="field-hint">Encrypt environment variables at rest using AWS KMS (customer-managed keys)</small>
                    </div>
                    
                    {formData.infrastructure?.enableKMSEncryptionLambda && (
                      <div className="form-group">
                        <label>KMS Key ID (Optional)</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.lambdaKMSKeyId || ''}
                          onChange={(e) => handleNestedChange('infrastructure', 'lambdaKMSKeyId', e.target.value)}
                          placeholder="arn:aws:kms:region:account:key/key-id or alias/key-alias" 
                        />
                        <small className="field-hint">Leave empty to create a new KMS key for Lambda encryption</small>
                      </div>
                    )}
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableCloudWatchLogsEncryption !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableCloudWatchLogsEncryption', e.target.checked)} 
                        />
                        📝 Enable CloudWatch Logs Encryption (KMS)
                      </label>
                      <small className="field-hint">Encrypt Lambda execution logs in CloudWatch Logs using KMS</small>
                    </div>
                    
                    {/* Secrets Management */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableSecretsManagerLambda !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableSecretsManagerLambda', e.target.checked)} 
                        />
                        🔑 Use AWS Secrets Manager
                      </label>
                      <small className="field-hint">Retrieve secrets from Secrets Manager at runtime instead of environment variables. Automatic rotation support.</small>
                    </div>
                    
                    {/* Code Integrity */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableCodeSigning !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableCodeSigning', e.target.checked)} 
                        />
                        ✍️ Enable Code Signing (AWS Signer)
                      </label>
                      <small className="field-hint">Verify code integrity using AWS Signer. Only signed code can be deployed.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableLayerVersionValidation !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableLayerVersionValidation', e.target.checked)} 
                        />
                        📦 Enable Lambda Layer Version Validation
                      </label>
                      <small className="field-hint">Validate layer checksums and enforce specific layer versions</small>
                    </div>
                    
                    {/* API Gateway Security (if using Function URLs or API Gateway) */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableFunctionURLAuth !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableFunctionURLAuth', e.target.checked)} 
                        />
                        🔐 Enable Function URL Auth (IAM)
                      </label>
                      <small className="field-hint">Require IAM authentication for Lambda Function URLs. Blocks anonymous access.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enablePrivateAPI !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enablePrivateAPI', e.target.checked)} 
                        />
                        🔒 Use Private API Gateway (VPC Endpoint)
                      </label>
                      <small className="field-hint">API Gateway accessible only from VPC via VPC endpoint. Eliminates public internet access.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableWAFLambda !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableWAFLambda', e.target.checked)} 
                        />
                        🛡️ Enable AWS WAF for API Gateway
                      </label>
                      <small className="field-hint">Protect Lambda function with WAF rules (rate limiting, SQL injection, XSS protection)</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableAPIKeyRequired !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableAPIKeyRequired', e.target.checked)} 
                        />
                        🔑 Require API Key (API Gateway)
                      </label>
                      <small className="field-hint">Enforce API key authentication for all API Gateway requests</small>
                    </div>
                    
                    {/* Access Control */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableResourceBasedPolicy !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableResourceBasedPolicy', e.target.checked)} 
                        />
                        📜 Enable Resource-Based Policy (Restrict Invocations)
                      </label>
                      <small className="field-hint">Limit which AWS services/principals can invoke the Lambda function</small>
                    </div>
                    
                    {formData.infrastructure?.enableResourceBasedPolicy && (
                      <div className="form-group">
                        <label>Allowed Principals (comma-separated)</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.allowedPrincipals || ''}
                          onChange={(e) => handleNestedChange('infrastructure', 'allowedPrincipals', e.target.value)}
                          placeholder="apiCore.amazonaws.com, events.amazonaws.com" 
                        />
                        <small className="field-hint">AWS service principals or IAM ARNs allowed to invoke. Leave empty to allow all (not recommended).</small>
                      </div>
                    )}
                    
                    {/* Monitoring & Threat Detection */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableGuardDutyLambda !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableGuardDutyLambda', e.target.checked)} 
                        />
                        🛡️ Enable GuardDuty for Lambda (Runtime Protection)
                      </label>
                      <small className="field-hint">Detect suspicious API calls, crypto mining, and unauthorized network activity</small>
                    </div>
                    
                    <div className="info-banner">
                      <strong>ℹ️ Zero Trust Architecture:</strong> These settings implement comprehensive Zero Trust security including VPC isolation, KMS encryption (environment variables & logs), AWS Secrets Manager, Code Signing, Function URL IAM auth, Private API Gateway, AWS WAF protection, resource-based policies, GuardDuty runtime monitoring, and CloudWatch Logs encryption. All critical features are enabled by default for maximum security.
                    </div>
                  </div>
                </>
              )}

              {/* GKE (Google Kubernetes Engine) Configuration */}
              {getComputePlatform() === 'GKE' && (
                <>
                  <div className="config-subsection">
                    <h4>🔒 Zero Trust Security Configuration</h4>
                    <p className="subsection-description">Configure comprehensive security controls for production-ready GKE clusters</p>
                    
                    {/* Cluster Security */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enablePrivateClusterGKE !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enablePrivateClusterGKE', e.target.checked)} 
                        />
                        🔐 Enable Private Cluster (Private Control Plane)
                      </label>
                      <small className="field-hint">Master nodes use private IPs. Kubernetes API accessible only from VPC.</small>
                    </div>
                    
                    {formData.infrastructure?.enablePrivateClusterGKE && (
                      <div className="form-group">
                        <label>Master IPv4 CIDR Block</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.masterIPV4CIDRBlock || '172.16.0.0/28'}
                          onChange={(e) => handleNestedChange('infrastructure', 'masterIPV4CIDRBlock', e.target.value)}
                          placeholder="172.16.0.0/28" 
                        />
                        <small className="field-hint">/28 CIDR block for GKE master nodes (must not overlap with cluster CIDR)</small>
                      </div>
                    )}
                    
                    {!formData.infrastructure?.enablePrivateClusterGKE && (
                      <div className="form-group">
                        <label>Master Authorized Networks (CIDR, comma-separated)</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.masterAuthorizedNetworks || ''}
                          onChange={(e) => handleNestedChange('infrastructure', 'masterAuthorizedNetworks', e.target.value)}
                          placeholder="203.0.113.0/24, 198.51.100.0/24" 
                        />
                        <small className="field-hint">IP blocks allowed to access Kubernetes API. Leave empty for unrestricted (not recommended).</small>
                      </div>
                    )}
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enablePrivateEndpointGKE !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enablePrivateEndpointGKE', e.target.checked)} 
                        />
                        🔒 Disable Public Endpoint (Internal Access Only)
                      </label>
                      <small className="field-hint">Remove public IP from master. Access only via VPC/Cloud VPN/Cloud Interconnect.</small>
                    </div>
                    
                    {/* Identity & Access */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableWorkloadIdentityGKE !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableWorkloadIdentityGKE', e.target.checked)} 
                        />
                        🎫 Enable Workload Identity (Passwordless Authentication)
                      </label>
                      <small className="field-hint">Bind Kubernetes service accounts to Google Cloud service accounts for secure, keyless access to GCP APIs</small>
                    </div>
                    
                    {/* Container & Image Security */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableBinaryAuthorizationGKE !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableBinaryAuthorizationGKE', e.target.checked)} 
                        />
                        ✅ Enable Binary Authorization (Container Attestation)
                      </label>
                      <small className="field-hint">Deploy only cryptographically signed container images. Enforces image provenance and policy compliance.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableShieldedNodes !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableShieldedNodes', e.target.checked)} 
                        />
                        🛡️ Enable Shielded Nodes (Verified Boot)
                      </label>
                      <small className="field-hint">Protects against rootkits and bootkits. Verifies node integrity using Secure Boot.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableSecureBoot !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableSecureBoot', e.target.checked)} 
                        />
                        ✅ Enable Secure Boot (UEFI)
                      </label>
                      <small className="field-hint">Verify boot loader and kernel signatures. Prevents unsigned code execution during boot.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableIntegrityMonitoring !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableIntegrityMonitoring', e.target.checked)} 
                        />
                        🔍 Enable Integrity Monitoring (vTPM)
                      </label>
                      <small className="field-hint">Virtual Trusted Platform Module for boot integrity verification and remote attestation</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableVulnerabilityScanning !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableVulnerabilityScanning', e.target.checked)} 
                        />
                        🔍 Enable Workload Vulnerability Scanning
                      </label>
                      <small className="field-hint">Continuous vulnerability scanning for container images in Artifact Registry and running workloads</small>
                    </div>
                    
                    {/* Network Security */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableVPCNative !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableVPCNative', e.target.checked)} 
                        />
                        🌐 Enable VPC-Native Cluster (Alias IPs)
                      </label>
                      <small className="field-hint">Pods get IP addresses from VPC subnet. Better network performance and security.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableNetworkPoliciesGKE !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableNetworkPoliciesGKE', e.target.checked)} 
                        />
                        🛡️ Enable Network Policies (Dataplane V2)
                      </label>
                      <small className="field-hint">Kubernetes NetworkPolicy enforcement for pod-to-pod traffic isolation (default-deny recommended)</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableDataplaneV2 !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableDataplaneV2', e.target.checked)} 
                        />
                        🚀 Enable Dataplane V2 (eBPF Networking)
                      </label>
                      <small className="field-hint">eBPF-based dataplane for faster network performance, network policy enforcement, and observability</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableIntranodeVisibility !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableIntranodeVisibility', e.target.checked)} 
                        />
                        👁️ Enable Intranode Visibility
                      </label>
                      <small className="field-hint">VPC Flow Logs for pod-to-pod traffic within nodes. Enhanced network observability.</small>
                    </div>
                    
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableCloudArmorGKE !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableCloudArmorGKE', e.target.checked)} 
                        />
                        🛡️ Enable Cloud Armor (DDoS & WAF)
                      </label>
                      <small className="field-hint">Google Cloud Armor protection for GKE Ingress (DDoS mitigation, rate limiting, geo-blocking)</small>
                    </div>
                    
                    {/* Pod Security */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enablePodSecurityPolicy !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enablePodSecurityPolicy', e.target.checked)} 
                        />
                        🔐 Enable Pod Security Policy (PSP / Pod Security Admission)
                      </label>
                      <small className="field-hint">Enforce security policies on pods (no privileged containers, host access restrictions, capabilities drop)</small>
                    </div>
                    
                    {/* Encryption */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableKMSEncryptionGKE !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableKMSEncryptionGKE', e.target.checked)} 
                        />
                        🔐 Enable Application-Layer Secrets Encryption (CMEK)
                      </label>
                      <small className="field-hint">Encrypt Kubernetes secrets at application layer using Cloud KMS (customer-managed encryption keys)</small>
                    </div>
                    
                    {formData.infrastructure?.enableKMSEncryptionGKE && (
                      <div className="form-group">
                        <label>Cloud KMS Key ID</label>
                        <input 
                          type="text" 
                          value={formData.infrastructure?.gkeKMSKeyId || ''}
                          onChange={(e) => handleNestedChange('infrastructure', 'gkeKMSKeyId', e.target.value)}
                          placeholder="projects/PROJECT_ID/locations/LOCATION/keyRings/KEYRING/cryptoKeys/KEY" 
                        />
                        <small className="field-hint">Leave empty to create a new Cloud KMS key for GKE secrets encryption</small>
                      </div>
                    )}
                    
                    {/* Cluster Options */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableGKEAutopilot || false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableGKEAutopilot', e.target.checked)} 
                        />
                        ☁️ Enable GKE Autopilot Mode (Fully Managed)
                      </label>
                      <small className="field-hint">Google manages nodes, security patches, and scaling. Enforces best practices automatically.</small>
                    </div>
                    
                    {/* Security Posture & Monitoring */}
                    <div className="form-group">
                      <label className="checkbox-label">
                        <input 
                          type="checkbox" 
                          checked={formData.infrastructure?.enableSecurityPosture !== false}
                          onChange={(e) => handleNestedChange('infrastructure', 'enableSecurityPosture', e.target.checked)} 
                        />
                        📊 Enable Security Posture Dashboard
                      </label>
                      <small className="field-hint">Continuous security posture monitoring with actionable recommendations (GKE Security Posture)</small>
                    </div>
                    
                    <div className="info-banner">
                      <strong>ℹ️ Zero Trust Architecture:</strong> These settings implement comprehensive Zero Trust security including private cluster (master/nodes), Workload Identity (OIDC), Binary Authorization (container attestation), Shielded Nodes (Secure Boot + vTPM), Dataplane V2 (eBPF networking), Network Policies (default-deny), Cloud KMS encryption (secrets), Cloud Armor (DDoS/WAF), Workload Vulnerability Scanning, Security Posture dashboard, and VPC-native networking. All critical features are enabled by default for maximum security.
                    </div>
                  </div>
                </>
              )}

              {/* Compute Summary */}
              <div className="compute-summary">
                <h4>📋 {getComputePlatform()} Configuration Summary</h4>
                <div className="summary-grid">
                  <div className="summary-item">
                    <span className="summary-label">Platform:</span>
                    <span className="summary-value">{getComputePlatform()}</span>
                  </div>
                  {formData.compute?.instanceType && (
                  <div className="summary-item">
                    <span className="summary-label">Instance Type:</span>
                    <span className="summary-value">{formData.compute.instanceType}</span>
                  </div>
                  )}
                  {formData.compute?.enableAutoScaling !== undefined && (
                  <div className="summary-item">
                    <span className="summary-label">Auto-Scaling:</span>
                    <span className="summary-value">
                      {formData.compute.enableAutoScaling 
                        ? `✅ ${formData.compute.minInstances} - ${formData.compute.maxInstances}` 
                        : '❌ Disabled'}
                    </span>
                  </div>
                  )}
                  <div className="summary-item">
                    <span className="summary-label">CPU:</span>
                    <span className="summary-value">{formData.compute?.cpuLimit}</span>
                  </div>
                  <div className="summary-item">
                    <span className="summary-label">Memory:</span>
                    <span className="summary-value">{formData.compute?.memoryLimit}</span>
                  </div>
                  {formData.compute?.storageSize && (
                  <div className="summary-item">
                    <span className="summary-label">Storage:</span>
                    <span className="summary-value">{formData.compute.storageSize}</span>
                  </div>
                  )}
                  {formData.compute?.enableSpotInstances !== undefined && (
                  <div className="summary-item">
                    <span className="summary-label">Spot/Preemptible:</span>
                    <span className="summary-value">{formData.compute.enableSpotInstances ? '✅ Enabled' : '❌ Disabled'}</span>
                  </div>
                  )}
                </div>
              </div>
            </div>
          )}
        </section>
        )}

        {/* Network Configuration - Always shown and emphasized for Network infrastructure */}
        <section className={`form-section ${(isInfrastructureTemplate() || formData.infrastructure?.resourceType === 'Network') ? 'form-section-emphasized' : ''}`}>
          {!isInfrastructureTemplate() && (
          <div className="section-toggle">
            <label className="toggle-label">
              <input
                type="checkbox"
                checked={showNetworkConfig}
                onChange={(e) => setShowNetworkConfig(e.target.checked)}
                className="toggle-checkbox"
              />
              <span className="toggle-text">🌐 Configure Network Settings (Optional)</span>
            </label>
          </div>
          )}

          {isInfrastructureTemplate() && formData.infrastructure?.resourceType === 'Network' && (
            <div className="section-info">
              <p>🌐 <strong>Network Infrastructure Configuration:</strong> Define Virtual Networks (VNets), subnets, address spaces, service endpoints, and network security groups for your network infrastructure deployment.</p>
            </div>
          )}

          {isInfrastructureTemplate() && formData.infrastructure?.resourceType !== 'Network' && (
            <div className="section-info">
              <p>🌐 <strong>Network Configuration:</strong> Define VNets, subnets, service endpoints, and security settings for your infrastructure.</p>
            </div>
          )}

          {(showNetworkConfig || isInfrastructureTemplate()) && (
            <NetworkConfigurationForm
              value={formData.network || {}}
              onChange={(networkConfig) => setFormData(prev => ({ ...prev, network: networkConfig }))}
            />
          )}
        </section>

        {/* Deployment Configuration - Hidden for Infrastructure templates */}
        {shouldShowDeploymentConfig() && (
        <section className="form-section">
          <h3>🚀 Deployment Configuration</h3>
          <div className="form-grid">
            <div className="form-group">
              <label>Orchestrator</label>
              <select
                value={formData.deployment?.orchestrator}
                onChange={(e) => handleNestedChange('deployment', 'orchestrator', e.target.value)}
              >
                <option>Kubernetes</option>
                <option>Docker Compose</option>
                <option>ECS</option>
                <option>Nomad</option>
              </select>
            </div>
            <div className="form-group">
              <label>CI/CD Platform</label>
              <select
                value={formData.deployment?.cicdPlatform}
                onChange={(e) => handleNestedChange('deployment', 'cicdPlatform', e.target.value)}
              >
                <option>GitHub Actions</option>
                <option>Azure DevOps</option>
                <option>GitLab CI</option>
                <option>Jenkins</option>
                <option>CircleCI</option>
              </select>
            </div>
          </div>
        </section>
        )}

        {/* Security Configuration - Always shown */}
        {shouldShowSecurityConfig() && (
        <section className="form-section">
          <h3>🔒 Security Configuration</h3>
          <div className="form-grid">
            <div className="form-group">
              <label>Authentication Provider</label>
              <select
                value={formData.security?.authenticationProvider}
                onChange={(e) => handleNestedChange('security', 'authenticationProvider', e.target.value)}
              >
                <option>Azure AD</option>
                <option>Auth0</option>
                <option>Okta</option>
                <option>JWT</option>
                <option>None</option>
              </select>
            </div>
          </div>
        </section>
        )}

        {/* Observability Configuration - Hidden for Infrastructure templates */}
        {shouldShowObservabilityConfig() && (
        <section className="form-section">
          <h3>📊 Observability Configuration</h3>
          <div className="form-checkboxes">
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={formData.observability?.logging}
                onChange={(e) => handleNestedChange('observability', 'logging', e.target.checked)}
              />
              Enable Logging
            </label>
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={formData.observability?.metrics}
                onChange={(e) => handleNestedChange('observability', 'metrics', e.target.checked)}
              />
              Enable Metrics
            </label>
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={formData.observability?.tracing}
                onChange={(e) => handleNestedChange('observability', 'tracing', e.target.checked)}
              />
              Enable Tracing
            </label>
          </div>
        </section>
        )}

        {/* Form Actions */}
        <div className="form-actions">
          <button
            type="button"
            onClick={handleValidate}
            disabled={loading}
            className="btn-secondary"
          >
            {loading ? '⏳ Validating...' : '✓ Validate'}
          </button>
          <button
            type="submit"
            disabled={loading}
            className="btn-primary"
          >
            {loading ? `⏳ ${isEditMode ? 'Updating' : 'Creating'}...` : `${isEditMode ? '💾 Update Template' : '🚀 Create Template'}`}
          </button>
          <button
            type="button"
            onClick={() => navigate('/templates')}
            className="btn-cancel"
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
};

export default CreateTemplate;
