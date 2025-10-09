// Application Insights module for monitoring and telemetry
@description('Name of the Application Insights instance')
param applicationInsightsName string

@description('Name of the Log Analytics Workspace')
param logAnalyticsWorkspaceName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, staging, prod)')
param environment string = 'dev'

@description('Application type')
@allowed([
  'web'
  'other'
])
param applicationType string = 'web'

@description('Retention in days')
@allowed([
  30
  60
  90
  120
  180
  270
  365
  550
  730
])
param retentionInDays int = environment == 'prod' ? 365 : 90

@description('Daily data cap in GB')
param dailyDataCapInGB int = environment == 'prod' ? 100 : 1

@description('Enable sampling')
param samplingPercentage int = environment == 'prod' ? 20 : 100

// Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
      disableLocalAuth: false
    }
    workspaceCapping: {
      dailyQuotaGb: dailyDataCapInGB
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
  tags: {
    Environment: environment
    Purpose: 'PlatformMonitoring'
  }
}

// Application Insights
resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: applicationInsightsName
  location: location
  kind: applicationType
  properties: {
    Application_Type: applicationType
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    Request_Source: 'rest'
    SamplingPercentage: samplingPercentage
    RetentionInDays: retentionInDays
  }
  tags: {
    Environment: environment
    Purpose: 'PlatformMonitoring'
  }
}

// Smart Detection Rules
resource slowPageLoadTimeRule 'Microsoft.AlertsManagement/smartDetectorAlertRules@2021-04-01' = {
  name: '${applicationInsightsName}-slow-page-load-time'
  properties: {
    description: 'Slow page load time'
    state: 'Enabled'
    severity: 'Sev3'
    frequency: 'PT1M'
    detector: {
      id: 'SlowPageLoadTimeDetector'
    }
    scope: [
      applicationInsights.id
    ]
    actionGroups: {
      groupIds: []
    }
  }
  tags: {
    Environment: environment
    Purpose: 'PlatformMonitoring'
  }
}

resource slowServerResponseTimeRule 'Microsoft.AlertsManagement/smartDetectorAlertRules@2021-04-01' = {
  name: '${applicationInsightsName}-slow-server-response-time'
  properties: {
    description: 'Slow server response time'
    state: 'Enabled'
    severity: 'Sev3'
    frequency: 'PT1M'
    detector: {
      id: 'SlowServerResponseTimeDetector'
    }
    scope: [
      applicationInsights.id
    ]
    actionGroups: {
      groupIds: []
    }
  }
  tags: {
    Environment: environment
    Purpose: 'PlatformMonitoring'
  }
}

// Availability Test for API endpoint
resource availabilityTest 'Microsoft.Insights/webtests@2022-06-15' = {
  name: '${applicationInsightsName}-availability-test'
  location: location
  kind: 'ping'
  properties: {
    SyntheticMonitorId: '${applicationInsightsName}-availability-test'
    Name: '${applicationInsightsName}-availability-test'
    Description: 'Platform API availability test'
    Enabled: true
    Frequency: 300 // 5 minutes
    Timeout: 30
    Kind: 'ping'
    RetryEnabled: true
    Locations: [
      {
        Id: 'us-ca-sjc-azr'
      }
      {
        Id: 'us-tx-sn1-azr'
      }
      {
        Id: 'us-il-ch1-azr'
      }
    ]
    Configuration: {
      WebTest: '<WebTest Name="${applicationInsightsName}-availability-test" Id="ABD48585-0831-40CB-9069-682EA6BB3583" Enabled="True" CssProjectStructure="" CssIteration="" Timeout="30" WorkItemIds="" xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010" Description="" CredentialUserName="" CredentialPassword="" PreAuthenticate="True" Proxy="default" StopOnError="False" RecordedResultFile="" ResultsLocale=""><Items><Request Method="GET" Guid="a5f10126-e4cd-570d-961c-cea43999a200" Version="1.1" Url="{{Url}}" ThinkTime="0" Timeout="30" ParseDependentRequests="False" FollowRedirects="True" RecordResult="True" Cache="False" ResponseTimeGoal="0" Encoding="utf-8" ExpectedHttpStatusCode="200" ExpectedResponseUrl="" ReportingName="" IgnoreHttpStatusCode="False" /></Items></WebTest>'
    }
  }
  tags: {
    Environment: environment
    Purpose: 'PlatformMonitoring'
    'hidden-link:${applicationInsights.id}': 'Resource'
  }
}

// Output values
output applicationInsightsId string = applicationInsights.id
output applicationInsightsName string = applicationInsights.name
output instrumentationKey string = applicationInsights.properties.InstrumentationKey
output connectionString string = applicationInsights.properties.ConnectionString
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id
output logAnalyticsWorkspaceName string = logAnalyticsWorkspace.name
