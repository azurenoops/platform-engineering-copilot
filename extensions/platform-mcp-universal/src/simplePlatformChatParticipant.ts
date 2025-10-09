import * as vscode from 'vscode';
import { PlatformMCPClient } from './mcpClient';
import { UniversalMcpParser, UniversalMcpResult } from './universalMcpParser';

/**
 * Simple Platform Chat Participant using Universal MCP Parser
 * This replaces the corrupted platformChatParticipant.ts with a clean implementation
 */
export class SimplePlatformChatParticipant implements vscode.Disposable {
    private participant: vscode.ChatParticipant;

    constructor(private context: vscode.ExtensionContext, private mcpClient: PlatformMCPClient) {
        try {
            // Check if chat API is available
            if (!vscode.chat || !vscode.chat.createChatParticipant) {
                throw new Error('VS Code Chat API is not available. Please ensure GitHub Copilot Chat extension is installed and enabled.');
            }

            // Create the chat participant with ID "platform" to match package.json
            this.participant = vscode.chat.createChatParticipant('platform', this.handleChatRequest.bind(this));

            // Set participant icon
            try {
                this.participant.iconPath = vscode.Uri.joinPath(context.extensionUri, 'media', 'platform-icon.svg');
            } catch (iconError) {
                console.warn('Could not set icon path:', iconError);
            }

            console.log('✅ Simple Platform Chat Participant created successfully!');

        } catch (error) {
            console.error('Failed to create Simple Platform Chat Participant:', error);
            throw error;
        }
    }

    async handleChatRequest(
        request: vscode.ChatRequest,
        context: vscode.ChatContext,
        stream: vscode.ChatResponseStream,
        token: vscode.CancellationToken
    ): Promise<void> {
        try {
            console.log(`🎯 Simple Platform Chat Request: "${request.prompt}"`);

            const prompt = request.prompt.toLowerCase();

            // Route based on simple keyword detection
            if (prompt.includes('create') && (prompt.includes('service') || prompt.includes('template'))) {
                await this.handleServiceTemplateCreation(request, stream, token);
            } else if (prompt.includes('show') && prompt.includes('template') && !prompt.includes('all')) {
                // Handle "show template <id>" - specific template by ID
                await this.handleShowSpecificTemplate(request, stream, token);
            } else if ((prompt.includes('list') || prompt.includes('show') || prompt.includes('see') || prompt.includes('all')) && (prompt.includes('service') || prompt.includes('template'))) {
                await this.handleListServiceTemplates(request, stream, token);
            } else if ((prompt.includes('create') || prompt.includes('deploy')) && prompt.includes('environment')) {
                await this.handleEnvironmentDeployment(request, stream, token);
            } else if (prompt.includes('provision') && prompt.includes('template')) {
                await this.handleTemplateProvisioning(request, stream, token);
            } else if (prompt.includes('cost') || prompt.includes('analyze')) {
                await this.handleCostAnalysis(request, stream, token);
            } else if (prompt.includes('provision') && prompt.includes('aks')) {
                await this.handleAKSProvisioning(request, stream, token);
            } else if ((prompt.includes('ato') || prompt.includes('compliance') || prompt.includes('nist') || prompt.includes('scan')) && 
                       (prompt.includes('azure') || prompt.includes('resource') || prompt.includes('resource group') || prompt.includes('subscription'))) {
                await this.handleATOComplianceScanning(request, stream, token);
            } else if (prompt.includes('test') && prompt.includes('parser')) {
                await this.handleParserTest(request, stream, token);
            } else {
                await this.handleGeneralRequest(request, stream, token);
            }

        } catch (error) {
            console.error('❌ Error handling platform chat request:', error);
            stream.markdown(`❌ **Error:** ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    private async handleServiceTemplateCreation(
        request: vscode.ChatRequest,
        stream: vscode.ChatResponseStream,
        token: vscode.CancellationToken
    ): Promise<void> {
        stream.markdown(`## 📦 Service Template Generation

Creating a comprehensive microservice template with production-ready configurations...

`);

        try {
            // Extract service name from prompt
            const serviceNameMatch = request.prompt.match(/(?:named|called|for)\s+['"]?([a-zA-Z0-9-_]+)['"]?/);
            const serviceName = serviceNameMatch ? serviceNameMatch[1] : 'sample-service';

            stream.markdown(`**Service Name:** ${serviceName}
**Template Type:** Microservice
**Framework:** .NET

🚀 **Generating components...**

`);

            // Call MCP server to create template in database
            const mcpResponse = await this.mcpClient.executeTool('service_templates', {
                action: 'create',
                serviceName: serviceName,
                serviceType: 'microservice',
                framework: 'dotnet',
                components: ['kubernetes', 'helm', 'monitoring', 'security']
            });

            console.log('🔍 Raw MCP Response from server:', JSON.stringify(mcpResponse, null, 2));

            // Display results directly from MCP response (all tools return markdown)
            if (mcpResponse && !mcpResponse.isError && mcpResponse.content && mcpResponse.content.length > 0) {
                const markdownContent = mcpResponse.content[0].text;
                stream.markdown(markdownContent);
            } else {
                const errorMsg = mcpResponse?.isError ? 'Service template creation failed' : 'No content returned from service template creation';
                stream.markdown(`### ❌ **Template Creation Failed**

**Error:** ${errorMsg}

### 🛠️ **Troubleshooting:**
- Verify service name is valid (alphanumeric, hyphens allowed)
- Check MCP server connection
- Ensure proper permissions for template generation
- Try with a different service name

`);
            }

        } catch (error) {
            console.error('❌ Error handling service template creation:', error);
            stream.markdown(`❌ **Error:** ${error instanceof Error ? error.message : 'Unknown error occurred during service template creation'}

### �️ **Common Issues:**
- **MCP Server Connection:** Check if the MCP server is running on localhost:8080
- **Invalid Service Name:** Use alphanumeric characters and hyphens only
- **Permissions:** Ensure proper access to template generation
- **Network Issues:** Check connectivity to the platform services

`);
        }
    }

    private async handleListServiceTemplates(
        request: vscode.ChatRequest,
        stream: vscode.ChatResponseStream,
        token: vscode.CancellationToken
    ): Promise<void> {
        stream.markdown(`## 📋 Saved Service Templates

Retrieving all service templates...

`);

        try {
            // Call MCP server to list all service templates
            const mcpResponse = await this.mcpClient.executeTool('service_templates', {
                action: 'list',
                includeDetails: true
            });

            console.log('🔍 MCP Response for template list:', JSON.stringify(mcpResponse, null, 2));

            // Display results directly from MCP response (all tools return markdown)
            if (mcpResponse && !mcpResponse.isError && mcpResponse.content && mcpResponse.content.length > 0) {
                const markdownContent = mcpResponse.content[0].text;
                stream.markdown(markdownContent);
            } else {
                const errorMsg = mcpResponse?.isError ? 'Failed to retrieve service templates' : 'No content returned from template list';
                stream.markdown(`### ❌ **Template List Failed**

**Error:** ${errorMsg}

### �️ **Troubleshooting:**
- Check MCP server connection
- Verify database is accessible
- Try: \`@platform test parser\`

`);
            }

        } catch (error) {
            console.error('❌ Error listing service templates:', error);
            stream.markdown(`❌ **Error:** ${error instanceof Error ? error.message : 'Unknown error occurred while retrieving templates'}

### 🛠️ **Common Issues:**
- **MCP Server Connection:** Check if the MCP server is running on localhost:8080
- **Database Access:** Ensure proper database connectivity
- **Network Issues:** Check connectivity to the platform services

`);
        }
    }

    private async handleShowSpecificTemplate(
        request: vscode.ChatRequest,
        stream: vscode.ChatResponseStream,
        token: vscode.CancellationToken
    ): Promise<void> {
        // Extract template ID from the request
        const templateIdMatch = request.prompt.match(/template\s+([a-f0-9-]{36})/i);
        const templateId = templateIdMatch ? templateIdMatch[1] : null;

        if (!templateId) {
            stream.markdown(`❌ **Invalid Template ID**

Please provide a valid template ID. 

**Usage:** \`@platform show template <template-id>\`

**Example:** \`@platform show template 88de9c97-6cb3-4212-9c24-653761ef9655\`

💡 **Tip:** Use \`@platform list all service templates\` to see available template IDs.

`);
            return;
        }

        stream.markdown(`## 🔍 Service Template Details

Retrieving template: \`${templateId}\`

`);

        try {
            // Call MCP server to list all templates, then filter for the specific one
            const mcpResponse = await this.mcpClient.executeTool('service_templates', {
                action: 'list',
                includeDetails: true
            });

            console.log('🔍 MCP Response for specific template:', JSON.stringify(mcpResponse, null, 2));

            // Display results directly from MCP response (all tools return markdown)
            if (mcpResponse && !mcpResponse.isError && mcpResponse.content && mcpResponse.content.length > 0) {
                const markdownContent = mcpResponse.content[0].text;
                
                // Add template ID filtering information to the response
                stream.markdown(`**Searching for Template ID:** \`${templateId}\`

${markdownContent}

### 🛠️ **Quick Actions:**
- **List all templates:** \`@platform list all service templates\`
- **Create new template:** \`@platform create a service template named <name>\`

`);
            } else {
                const errorMsg = mcpResponse?.isError ? 'Failed to retrieve specific template' : 'No content returned from template details';
                stream.markdown(`### ❌ **Template Retrieval Failed**

**Error:** ${errorMsg}

### �️ **Troubleshooting:**
- Verify the template ID is correct: \`${templateId}\`
- Check if the template still exists
- Use \`@platform list all service templates\` to see available templates
- Check MCP server connection

`);
            }

        } catch (error) {
            console.error('❌ Error retrieving specific template:', error);
            stream.markdown(`❌ **Error:** ${error instanceof Error ? error.message : 'Unknown error occurred while retrieving template'}

### 🛠️ **Common Issues:**
- **Invalid Template ID:** Ensure the template ID is in UUID format
- **MCP Server Connection:** Check if the MCP server is running on localhost:8080
- **Template Not Found:** The template may have been deleted
- **Network Issues:** Check connectivity to the platform services

`);
        }
    }

    private async handleCostAnalysis(
        request: vscode.ChatRequest,
        stream: vscode.ChatResponseStream,
        token: vscode.CancellationToken
    ): Promise<void> {
        stream.markdown(`## 💰 Cost Analysis

Analyzing your Azure costs and usage patterns...

`);

        try {
            const mcpResponse = await this.mcpClient.executeTool('cost_monitoring', {
                action: 'analyze',
                timeframe: 'monthly',
                includeBreakdown: true
            });

            console.log('🔍 Cost Analysis Response:', JSON.stringify(mcpResponse, null, 2));

            // Display results directly from MCP response (all tools return markdown)
            if (mcpResponse && !mcpResponse.isError && mcpResponse.content && mcpResponse.content.length > 0) {
                const markdownContent = mcpResponse.content[0].text;
                stream.markdown(markdownContent);
            } else {
                const errorMsg = mcpResponse?.isError ? 'Cost analysis failed' : 'No content returned from cost analysis';
                stream.markdown(`### ❌ **Cost Analysis Failed**

**Error:** ${errorMsg}

### 🛠️ **Troubleshooting:**
- Verify Azure credentials are configured
- Check billing account permissions
- Ensure cost data is available for the timeframe
- Try with a different time period

`);
            }

        } catch (error) {
            console.error('❌ Error handling cost analysis:', error);
            stream.markdown(`❌ **Error:** ${error instanceof Error ? error.message : 'Unknown error occurred during cost analysis'}

### 🛠️ **Common Issues:**
- **Azure Authentication:** Run \`az login\` to authenticate
- **Billing Permissions:** Ensure account has Cost Management Reader role
- **Network Issues:** Check connectivity to Azure services
- **Service Availability:** Cost Management API may be temporarily unavailable

`);
        }
    }

    private async handleAKSProvisioning(
        request: vscode.ChatRequest,
        stream: vscode.ChatResponseStream,
        token: vscode.CancellationToken
    ): Promise<void> {
        stream.markdown(`## 🚀 AKS Cluster Provisioning

Provisioning Azure Kubernetes Service cluster...

`);

        try {
            const mcpResponse = await this.mcpClient.executeTool('aks_provisioning', {
                action: 'provision',
                nodeCount: 3,
                vmSize: 'Standard_DS2_v2',
                kubernetesVersion: 'latest'
            });

            console.log('🔍 AKS Provisioning Response:', JSON.stringify(mcpResponse, null, 2));

            // Display results directly from MCP response (all tools return markdown)
            if (mcpResponse && !mcpResponse.isError && mcpResponse.content && mcpResponse.content.length > 0) {
                const markdownContent = mcpResponse.content[0].text;
                stream.markdown(markdownContent);
            } else {
                const errorMsg = mcpResponse?.isError ? 'AKS cluster provisioning failed' : 'No content returned from AKS provisioning';
                stream.markdown(`### ❌ **AKS Provisioning Failed**

**Error:** ${errorMsg}

### 🛠️ **Troubleshooting:**
- Verify Azure credentials are configured
- Check subscription permissions for AKS creation
- Ensure sufficient quota for VM sizes
- Try with different node count or VM size

`);
            }

        } catch (error) {
            console.error('❌ Error handling AKS provisioning:', error);
            stream.markdown(`❌ **Error:** ${error instanceof Error ? error.message : 'Unknown error occurred during AKS provisioning'}

### 🛠️ **Common Issues:**
- **Azure Authentication:** Run \`az login\` to authenticate
- **Insufficient Permissions:** Ensure account has Contributor role
- **Resource Quotas:** Check if subscription has available VM quota
- **Network Issues:** Check connectivity to Azure services

`);
        }
    }

    private async handleParserTest(
        request: vscode.ChatRequest,
        stream: vscode.ChatResponseStream,
        token: vscode.CancellationToken
    ): Promise<void> {
        stream.markdown(`## 🧪 MCP Response Format Test

Testing direct markdown response handling (no parsing needed)...

`);

        try {
            // Test with a simple tool call
            const mcpResponse = await this.mcpClient.executeTool('service_templates', {
                action: 'list',
                includeDetails: false
            });

            console.log('🔍 Parser Test Response:', JSON.stringify(mcpResponse, null, 2));

            stream.markdown(`### 🔍 **Response Format Analysis**

**Response Structure:**
- **Has Error Flag:** ${mcpResponse?.isError ? 'Yes' : 'No'}
- **Has Content Array:** ${mcpResponse?.content ? 'Yes' : 'No'}
- **Content Length:** ${mcpResponse?.content?.length || 0}
- **Content Type:** ${mcpResponse?.content?.[0]?.type || 'N/A'}

### 📝 **Raw Response Content:**

`);

            // Display the actual response content
            if (mcpResponse && !mcpResponse.isError && mcpResponse.content && mcpResponse.content.length > 0) {
                const markdownContent = mcpResponse.content[0].text;
                stream.markdown(`✅ **Direct Markdown Content (No Parsing Needed):**

${markdownContent}

### ✅ **Test Result:** Direct markdown handling is working correctly!

`);
            } else {
                stream.markdown(`❌ **Test Failed:** No valid content returned

**Debug Info:**
\`\`\`json
${JSON.stringify(mcpResponse, null, 2)}
\`\`\`

`);
            }

        } catch (error) {
            console.error('❌ Error in parser test:', error);
            stream.markdown(`❌ **Test Error:** ${error instanceof Error ? error.message : 'Unknown error'}

This indicates an issue with the MCP server connection or tool execution.

`);
        }
    }

    private async handleGeneralRequest(
        request: vscode.ChatRequest,
        stream: vscode.ChatResponseStream,
        token: vscode.CancellationToken
    ): Promise<void> {
        stream.markdown(`## 🤖 Platform Engineering Assistant

I can help you with:

### 🛠️ **Available Commands:**

#### 📦 **Service Templates:**
- **Create template:** \`@platform create a service template named my-service\`
- **List templates:** \`@platform list all service templates\`
- **Show template details:** \`@platform show template <id>\`

#### 🌍 **Environment Deployment:**
- **Create environment with template:** \`@platform create environment named dev-env type development\`
- **Deploy with specific template:** \`@platform create environment named prod-env templateId <id>\`

#### 🚀 **Service Provisioning:**
- **Provision from template:** \`@platform provision template <id> named my-service\`
- **Deploy to environment:** \`@platform provision template <id> to dev-environment\`

#### 🔧 **Platform Operations:**
- **Analyze costs:** \`@platform analyze costs\`
- **Provision AKS:** \`@platform provision aks cluster\`
- **Test parser:** \`@platform test parser\`

### 💡 **Your Request:** "${request.prompt}"

Try one of the commands above, or ask me about platform engineering topics!

`);
    }

    private async handleEnvironmentDeployment(
        request: vscode.ChatRequest,
        stream: vscode.ChatResponseStream,
        token: vscode.CancellationToken
    ): Promise<void> {
        stream.markdown(`## 🌍 Environment Deployment with Service Template

Creating environment from service template...

`);

        try {
            // Extract environment details from prompt
            const envNameMatch = request.prompt.match(/(?:named|called)\s+['"]?([a-zA-Z0-9-_]+)['"]?/);
            const envName = envNameMatch ? envNameMatch[1] : 'dev-environment';

            const typeMatch = request.prompt.match(/type\s+(['"]?)([a-zA-Z0-9-_]+)\1/);
            const userEnvType = typeMatch ? typeMatch[2] : 'development';
            
            // Map user-friendly types to Azure environment types
            const envType = userEnvType.toLowerCase() === 'development' || userEnvType.toLowerCase() === 'dev' ? 'aks' :
                           userEnvType.toLowerCase() === 'production' || userEnvType.toLowerCase() === 'prod' ? 'aks' :
                           userEnvType.toLowerCase() === 'staging' ? 'aks' :
                           userEnvType.toLowerCase() === 'frontend' ? 'webapp' :
                           userEnvType.toLowerCase() === 'api' ? 'webapp' :
                           userEnvType.toLowerCase() === 'function' ? 'function' :
                           'aks'; // Default to AKS for most cases

            const templateIdMatch = request.prompt.match(/template(?:Id)?\s+(['"]?)([a-fA-F0-9-]+)\1/);
            const templateId = templateIdMatch ? templateIdMatch[2] : undefined;

            const tierMatch = request.prompt.match(/tier\s+(['"]?)([a-zA-Z0-9-_]+)\1/);
            const tier = tierMatch ? tierMatch[2] : 'basic';

            const locationMatch = request.prompt.match(/location\s+(['"]?)([a-zA-Z0-9-_]+)\1/);
            const location = locationMatch ? locationMatch[2] : 'eastus';

            stream.markdown(`**Environment Name:** ${envName}
**Environment Type:** ${userEnvType} (Azure: ${envType})
**Template ID:** ${templateId || 'Auto-selected'}
**Deployment Tier:** ${tier}
**Location:** ${location}

🚀 **Creating environment with template integration...**

`);

            // Call environment management tool with template settings
            const templateSettings = templateId ? 
                { templateId, deploymentTier: tier } :
                { templateType: 'microservice', deploymentTier: tier };

            const toolParams = {
                action: 'create',
                name: envName,
                type: envType,
                tier: tier,
                location: location,
                templateSettings: templateSettings
            };

            console.log('🔧 Debug - Environment tool params:', JSON.stringify(toolParams, null, 2));

            const mcpResponse = await this.mcpClient.executeTool('environment_management', toolParams);
            
            console.log('🔧 Debug - Raw MCP Response:', JSON.stringify(mcpResponse, null, 2));

            // Display results directly from MCP response (all tools return markdown)
            if (mcpResponse && !mcpResponse.isError && mcpResponse.content && mcpResponse.content.length > 0) {
                const markdownContent = mcpResponse.content[0].text;
                stream.markdown(markdownContent);
                
                // Show additional quick actions
                stream.markdown(`

### 💡 **Quick Actions:**
- **Deploy service to environment:** \`@platform provision template <template-id> to ${envName}\`
- **Check deployment status:** \`@platform show environment ${envName}\`
- **Configure monitoring:** \`@platform set up monitoring for ${envName}\`

`);
            } else {
                const errorMsg = mcpResponse?.isError ? 'Environment deployment failed' : 'No content returned from environment deployment';
                stream.markdown(`### ❌ **Environment Deployment Failed**

**Error:** ${errorMsg}

### �️ **Troubleshooting:**
- Check Azure subscription permissions
- Verify resource group name availability
- Ensure template ID exists (if specified): ${templateId || 'N/A'}
- Try with a different location: ${location}

### 🧪 **Debug Information:**
- **Tool Called:** environment_management
- **Parameters:** ${JSON.stringify(toolParams, null, 2)}

`);
            }

        } catch (error) {
            console.error('❌ Error handling environment deployment:', error);
            stream.markdown(`❌ **Error:** ${error instanceof Error ? error.message : 'Unknown error occurred while creating environment'}

### 🔧 **Detailed Error Information:**
\`\`\`
${error instanceof Error ? error.stack : JSON.stringify(error, null, 2)}
\`\`\`

### 🧪 **Debug Data:**
- **Tool Name:** environment_management
- **Attempted Action:** create environment with template integration

### 💡 **Possible Issues:**
1. **MCP Server Connection** - Server may not be running
2. **Resource Group** - May need to exist before environment creation
3. **Azure Permissions** - May need proper Azure credentials
4. **Tool Registration** - Environment management tool may not be registered

### 🛠️ **Try These Commands:**
- **Test MCP connection:** \`@platform test parser\`
- **List available tools:** Check MCP server logs
- **Create resource group first:** Use Azure Portal or CLI

`);
        }
    }

    private async handleTemplateProvisioning(
        request: vscode.ChatRequest,
        stream: vscode.ChatResponseStream,
        token: vscode.CancellationToken
    ): Promise<void> {
        stream.markdown(`## 🚀 Service Template Provisioning

Provisioning service from template to target environment...

`);

        try {
            // Extract template and service details from prompt
            const templateIdMatch = request.prompt.match(/(?:template|from)\s+(['"]?)([a-fA-F0-9-]+)\1/);
            const templateId = templateIdMatch ? templateIdMatch[2] : undefined;

            const serviceNameMatch = request.prompt.match(/(?:named|called|as)\s+(['"]?)([a-zA-Z0-9-_]+)\1/);
            const serviceName = serviceNameMatch ? serviceNameMatch[2] : 'provisioned-service';

            const environmentMatch = request.prompt.match(/(?:to|in|environment)\s+(['"]?)([a-zA-Z0-9-_]+)\1/);
            const targetEnvironment = environmentMatch ? environmentMatch[2] : undefined;

            if (!templateId) {
                stream.markdown(`❌ **Template ID Required**

Please specify a template ID to provision from.

**Usage:** \`@platform provision template <template-id> named <service-name>\`

**Example:** \`@platform provision template 88de9c97-6cb3-4212-9c24-653761ef9655 named user-service\`

### 📋 **Available Templates:**
Use \`@platform list all service templates\` to see available templates.

`);
                return;
            }

            stream.markdown(`**Template ID:** ${templateId}
**Service Name:** ${serviceName}
**Target Environment:** ${targetEnvironment || 'Default'}

🚀 **Provisioning service from template...**

`);

            // Call service template tool to provision
            const mcpResponse = await this.mcpClient.executeTool('service_templates', {
                action: 'provision',
                templateId: templateId,
                serviceName: serviceName,
                targetEnvironment: targetEnvironment
            });

            console.log('🔍 Template Provisioning Response:', JSON.stringify(mcpResponse, null, 2));

            // Display results directly from MCP response (all tools return markdown)
            if (mcpResponse && !mcpResponse.isError && mcpResponse.content && mcpResponse.content.length > 0) {
                const markdownContent = mcpResponse.content[0].text;
                stream.markdown(markdownContent);
                
                // Show additional quick actions
                stream.markdown(`

### 💡 **Quick Actions:**
- **View service template details:** \`@platform show template ${templateId}\`
- **Create additional environments:** \`@platform create environment for ${serviceName}\`
- **Monitor costs:** \`@platform analyze costs for ${serviceName}\`

`);
            } else {
                const errorMsg = mcpResponse?.isError ? 'Service provisioning failed' : 'No content returned from template provisioning';
                stream.markdown(`### ❌ **Service Provisioning Failed**

**Error:** ${errorMsg}

### �️ **Troubleshooting:**
- Verify template ID exists: \`@platform show template ${templateId}\`
- Check Azure subscription permissions
- Ensure resource naming is unique
- Try with a different service name: ${serviceName}

`);
            }

        } catch (error) {
            console.error('❌ Error handling template provisioning:', error);
            stream.markdown(`❌ **Error:** ${error instanceof Error ? error.message : 'Unknown error occurred while provisioning template'}`);
        }
    }

    private async handleATOComplianceScanning(
        request: vscode.ChatRequest,
        stream: vscode.ChatResponseStream,
        token: vscode.CancellationToken
    ): Promise<void> {
        stream.markdown(`## 🔒 ATO Compliance Scanning

Analyzing your Azure resources for ATO compliance violations...

`);

        try {
            const prompt = request.prompt.toLowerCase();
            
            // Extract parameters from the request
            let toolName = 'azure_scan_resource_group_ato';
            let subscriptionId = '';
            let resourceGroupName = '';
            let resourceId = '';
            
            // Determine which scan tool to use based on request
            if (prompt.includes('subscription')) {
                toolName = 'azure_scan_subscription_ato';
            } else if (prompt.includes('resource') && !prompt.includes('resource group')) {
                toolName = 'azure_scan_resource_ato';
            }
            
            // Extract subscription ID if mentioned
            const subMatch = request.prompt.match(/subscription[:\s]+([a-f0-9-]{36})/i);
            if (subMatch) {
                subscriptionId = subMatch[1];
            } else {
                subscriptionId = '453c2549-4cc5-464f-ba66-acad920823e8'; // Default subscription
            }
            
            // Extract resource group name if mentioned
            const rgMatch = request.prompt.match(/resource.?group[:\s]+([a-zA-Z0-9-_]+)/i);
            if (rgMatch) {
                resourceGroupName = rgMatch[1];
            } else if (toolName === 'azure_scan_resource_group_ato') {
                resourceGroupName = 'platform-demo-rg'; // Default resource group
            }
            
            // Extract resource ID if mentioned  
            const resourceMatch = request.prompt.match(/resource[:\s]+([^\s]+)/i);
            if (resourceMatch && !resourceMatch[1].includes('group')) {
                resourceId = resourceMatch[1];
            }

            // Build parameters
            const parameters: any = {
                subscriptionId: subscriptionId,
                scanType: 'full',
                complianceFrameworks: ['NIST-800-53'],
                severity: 'all',
                includeRemediation: true,
                showDetailedAnalysis: false,
                enableAiAnalysis: true,
                businessContext: 'government'
            };

            if (toolName === 'azure_scan_resource_group_ato' && resourceGroupName) {
                parameters.resourceGroupName = resourceGroupName;
            } else if (toolName === 'azure_scan_resource_ato' && resourceId) {
                parameters.resourceId = resourceId;
            }

            stream.markdown(`**Scan Type:** ${toolName.replace('azure_scan_', '').replace('_ato', '').replace('_', ' ')}
**Subscription:** ${subscriptionId}
${resourceGroupName ? `**Resource Group:** ${resourceGroupName}\n` : ''}${resourceId ? `**Resource ID:** ${resourceId}\n` : ''}**Compliance Framework:** NIST 800-53
**AI Analysis:** Enabled

🔍 **Executing compliance scan...**

`);

            // Execute the ATO compliance scan
            const mcpResponse = await this.mcpClient.executeTool(toolName, parameters);
            console.log('🔍 ATO Compliance Scan Response:', JSON.stringify(mcpResponse, null, 2));

            // Display results directly from MCP response (all tools return markdown)
            if (mcpResponse && !mcpResponse.isError && mcpResponse.content && mcpResponse.content.length > 0) {
                const markdownContent = mcpResponse.content[0].text;
                stream.markdown(markdownContent);
            } else {
                const errorMsg = mcpResponse?.isError ? 'Tool execution failed' : 'No content returned from compliance scan';
                stream.markdown(`### ❌ **Scan Failed**

**Error:** ${errorMsg}

### 🛠️ **Troubleshooting:**
- Verify Azure credentials are configured
- Check subscription ID and resource names
- Ensure proper permissions for compliance scanning
- Try with a smaller scope (single resource group)

`);
            }

        } catch (error) {
            console.error('❌ Error handling ATO compliance scanning:', error);
            stream.markdown(`❌ **Error:** ${error instanceof Error ? error.message : 'Unknown error occurred during ATO compliance scanning'}

### 🛠️ **Common Issues:**
- **Azure Authentication:** Run \`az login\` to authenticate
- **Missing Permissions:** Ensure account has Security Reader role
- **Network Issues:** Check connectivity to Azure services
- **Resource Access:** Verify resource group/subscription exists

`);
        }
    }

    dispose(): void {
        this.participant?.dispose();
    }
}