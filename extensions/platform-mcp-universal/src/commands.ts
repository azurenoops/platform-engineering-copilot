import * as vscode from 'vscode';
import { PlatformMCPClient } from './mcpClient';

export class PlatformCommands {
    constructor(
        private context: vscode.ExtensionContext,
        private mcpClient: PlatformMCPClient
    ) {}

    async provisionInfrastructure(): Promise<void> {
        try {
            const result = await vscode.window.showQuickPick([
                { label: '🚢 AKS Cluster', description: 'Azure Kubernetes Service cluster', value: 'aks' },
                { label: '� Storage Account', description: 'Azure Storage Account', value: 'storage' },
                { label: '🔐 Key Vault', description: 'Azure Key Vault', value: 'keyvault' },
                { label: '🌐 Virtual Network', description: 'Azure Virtual Network', value: 'vnet' }
            ], {
                placeHolder: 'Select infrastructure type to provision'
            });

            if (result) {
                const response = await this.mcpClient.sendSemanticQuery({
                    query: `provision ${result.value}`,
                    userRole: 'platform-engineer'
                });
                
                if (response.success && response.markdown) {
                    const panel = vscode.window.createWebviewPanel(
                        'infrastructureResult',
                        'Infrastructure Provisioning',
                        vscode.ViewColumn.One,
                        {}
                    );
                    panel.webview.html = this.getWebviewContent(response.markdown);
                }
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Infrastructure provisioning failed: ${error}`);
        }
    }

    async deployContainer(): Promise<void> {
        try {
            const imageName = await vscode.window.showInputBox({
                prompt: 'Enter container image name (e.g., nginx:latest)',
                placeHolder: 'nginx:latest'
            });

            if (imageName) {
                const response = await this.mcpClient.sendSemanticQuery({
                    query: `deploy container ${imageName}`,
                    userRole: 'platform-engineer'
                });
                
                if (response.success) {
                    vscode.window.showInformationMessage('Container deployment initiated');
                } else {
                    vscode.window.showErrorMessage(`Deployment failed: ${response.error}`);
                }
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Container deployment failed: ${error}`);
        }
    }

    async createMonitoringDashboard(): Promise<void> {
        try {
            const response = await this.mcpClient.sendSemanticQuery({
                query: 'create monitoring dashboard',
                userRole: 'platform-engineer'
            });
            
            if (response.success) {
                vscode.window.showInformationMessage('Monitoring dashboard creation initiated');
            } else {
                vscode.window.showErrorMessage(`Dashboard creation failed: ${response.error}`);
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Dashboard creation failed: ${error}`);
        }
    }

    async runSecurityScan(): Promise<void> {
        try {
            const response = await this.mcpClient.sendSemanticQuery({
                query: 'run security scan',
                userRole: 'platform-engineer'
            });
            
            if (response.success && response.markdown) {
                const panel = vscode.window.createWebviewPanel(
                    'securityScan',
                    'Security Scan Results',
                    vscode.ViewColumn.One,
                    {}
                );
                panel.webview.html = this.getWebviewContent(response.markdown);
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Security scan failed: ${error}`);
        }
    }

    async runAtoComplianceScan(): Promise<void> {
        try {
            const response = await this.mcpClient.sendSemanticQuery({
                query: 'run ato compliance scan',
                userRole: 'platform-engineer'
            });
            
            if (response.success && response.markdown) {
                const panel = vscode.window.createWebviewPanel(
                    'atoCompliance',
                    'ATO Compliance Results',
                    vscode.ViewColumn.One,
                    {}
                );
                panel.webview.html = this.getWebviewContent(response.markdown);
            }
        } catch (error) {
            vscode.window.showErrorMessage(`ATO compliance scan failed: ${error}`);
        }
    }

    // Authentication commands
    async authenticateAzure(): Promise<void> {
        try {
            vscode.window.showInformationMessage('Azure authentication will be handled by MCP server');
            const response = await this.mcpClient.sendSemanticQuery({
                query: 'authenticate azure',
                userRole: 'platform-engineer'
            });
            
            if (response.success) {
                vscode.window.showInformationMessage('Azure authentication initiated');
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Azure authentication failed: ${error}`);
        }
    }

    async authenticateGitHub(): Promise<void> {
        try {
            vscode.window.showInformationMessage('GitHub authentication will be handled by MCP server');
            const response = await this.mcpClient.sendSemanticQuery({
                query: 'authenticate github',
                userRole: 'platform-engineer'
            });
            
            if (response.success) {
                vscode.window.showInformationMessage('GitHub authentication initiated');
            }
        } catch (error) {
            vscode.window.showErrorMessage(`GitHub authentication failed: ${error}`);
        }
    }

    async checkAuthStatus(): Promise<void> {
        try {
            const response = await this.mcpClient.sendSemanticQuery({
                query: 'check authentication status',
                userRole: 'platform-engineer'
            });
            
            if (response.success && response.markdown) {
                vscode.window.showInformationMessage('Authentication status checked - see output');
                console.log(response.markdown);
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Auth status check failed: ${error}`);
        }
    }

    async selectAzureCloud(): Promise<void> {
        try {
            const cloud = await vscode.window.showQuickPick([
                { label: 'Azure Commercial', value: 'commercial' },
                { label: 'Azure Government', value: 'government' }
            ], {
                placeHolder: 'Select Azure Cloud Environment'
            });

            if (cloud) {
                vscode.window.showInformationMessage(`Azure cloud set to: ${cloud.label}`);
                // Store the selection in workspace configuration
                await vscode.workspace.getConfiguration('platform-mcp-universal').update(
                    'azureCloud', 
                    cloud.value, 
                    vscode.ConfigurationTarget.Workspace
                );
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Cloud selection failed: ${error}`);
        }
    }

    async signOutAll(): Promise<void> {
        try {
            const response = await this.mcpClient.sendSemanticQuery({
                query: 'sign out all accounts',
                userRole: 'platform-engineer'
            });
            
            if (response.success) {
                vscode.window.showInformationMessage('All accounts signed out');
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Sign out failed: ${error}`);
        }
    }

    // Diagnostics and cache commands
    async checkMCPConnections(): Promise<void> {
        try {
            const statusMessage = vscode.window.setStatusBarMessage('$(loading~spin) Checking MCP connection...');
            
            const health = await this.mcpClient.checkHealth();
            statusMessage.dispose();
            
            if (health.healthy) {
                // Show detailed connection status in a webview panel
                const panel = vscode.window.createWebviewPanel(
                    'mcpConnectionStatus',
                    'Platform MCP Connection Status',
                    vscode.ViewColumn.Beside,
                    { enableScripts: true }
                );

                panel.webview.html = `
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset="UTF-8">
                        <meta name="viewport" content="width=device-width, initial-scale=1.0">
                        <title>MCP Connection Status</title>
                        <style>
                            body { 
                                font-family: var(--vscode-font-family); 
                                padding: 20px; 
                                color: var(--vscode-foreground);
                                background-color: var(--vscode-editor-background);
                            }
                            .status-card { 
                                background: var(--vscode-editor-background); 
                                border: 1px solid var(--vscode-panel-border);
                                border-radius: 8px; 
                                padding: 16px; 
                                margin-bottom: 16px; 
                            }
                            .status-header { 
                                display: flex; 
                                align-items: center; 
                                margin-bottom: 12px; 
                            }
                            .status-icon { 
                                margin-right: 8px; 
                                font-size: 18px; 
                            }
                            .connected { color: var(--vscode-testing-iconPassed); }
                            .disconnected { color: var(--vscode-testing-iconFailed); }
                            .details { color: var(--vscode-descriptionForeground); }
                            .metric { 
                                display: flex; 
                                justify-content: space-between; 
                                margin: 8px 0; 
                            }
                            .metric-label { font-weight: 500; }
                            .metric-value { color: var(--vscode-symbolIcon-numberForeground); }
                        </style>
                    </head>
                    <body>
                        <div class="status-card">
                            <div class="status-header">
                                <span class="status-icon connected">✓</span>
                                <h2>Platform MCP Server Connected</h2>
                            </div>
                            <div class="details">
                                <div class="metric">
                                    <span class="metric-label">Server Status:</span>
                                    <span class="metric-value connected">Healthy</span>
                                </div>
                                <div class="metric">
                                    <span class="metric-label">Available Tools:</span>
                                    <span class="metric-value">${health.toolCount || 'Unknown'}</span>
                                </div>
                                <div class="metric">
                                    <span class="metric-label">Connection Type:</span>
                                    <span class="metric-value">HTTP API</span>
                                </div>
                                <div class="metric">
                                    <span class="metric-label">Last Check:</span>
                                    <span class="metric-value">${new Date().toLocaleTimeString()}</span>
                                </div>
                            </div>
                        </div>
                        
                        <div class="status-card">
                            <h3>🔧 Available Features</h3>
                            <div class="details">
                                <p>✓ Semantic query processing</p>
                                <p>✓ Platform engineering commands</p>
                                <p>✓ Chat participant integration</p>
                                <p>✓ Infrastructure provisioning</p>
                                <p>✓ Security and compliance scans</p>
                            </div>
                        </div>
                    </body>
                    </html>
                `;

                // Also show a brief success message
                vscode.window.showInformationMessage(
                    `✅ Platform MCP Server: Connected (${health.toolCount || 'Unknown'} tools available)`
                );
            } else {
                vscode.window.showWarningMessage(
                    `⚠️ Platform MCP Server: Disconnected - ${health.error || 'Unknown error'}`,
                    'Retry Connection',
                    'View Logs'
                ).then(selection => {
                    if (selection === 'Retry Connection') {
                        this.reconnectMCPServers();
                    } else if (selection === 'View Logs') {
                        vscode.commands.executeCommand('workbench.action.toggleDevTools');
                    }
                });
            }
        } catch (error) {
            vscode.window.showErrorMessage(
                `❌ Connection check failed: ${error instanceof Error ? error.message : 'Unknown error'}`,
                'Troubleshoot'
            ).then(selection => {
                if (selection === 'Troubleshoot') {
                    vscode.env.openExternal(vscode.Uri.parse('https://github.com/jrspinella/platform-mcp-supervisor#troubleshooting'));
                }
            });
        }
    }

    async reconnectMCPServers(): Promise<void> {
        try {
            // Update client configuration to force reconnection
            this.mcpClient.updateConfig();
            
            // Test the connection
            const health = await this.mcpClient.checkHealth();
            if (health.healthy) {
                vscode.window.showInformationMessage('✅ MCP Server reconnected successfully');
            } else {
                vscode.window.showWarningMessage('⚠️ Reconnection attempt failed');
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Reconnection failed: ${error}`);
        }
    }

    async viewCacheStats(): Promise<void> {
        try {
            const response = await this.mcpClient.sendSemanticQuery({
                query: 'show cache statistics',
                userRole: 'platform-engineer'
            });
            
            if (response.success && response.markdown) {
                const panel = vscode.window.createWebviewPanel(
                    'cacheStats',
                    'Cache Statistics',
                    vscode.ViewColumn.One,
                    {}
                );
                panel.webview.html = this.getWebviewContent(response.markdown);
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Cache stats failed: ${error}`);
        }
    }

    async clearCache(): Promise<void> {
        try {
            const confirm = await vscode.window.showWarningMessage(
                'Are you sure you want to clear the cache?',
                'Yes', 'No'
            );

            if (confirm === 'Yes') {
                const response = await this.mcpClient.sendSemanticQuery({
                    query: 'clear cache',
                    userRole: 'platform-engineer'
                });
                
                if (response.success) {
                    vscode.window.showInformationMessage('Cache cleared successfully');
                }
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Cache clear failed: ${error}`);
        }
    }

    // Authentication troubleshooting commands
    async resetAuthenticationState(): Promise<void> {
        try {
            const confirm = await vscode.window.showWarningMessage(
                'This will reset all authentication state. Continue?',
                'Yes', 'No'
            );

            if (confirm === 'Yes') {
                const response = await this.mcpClient.sendSemanticQuery({
                    query: 'reset authentication state',
                    userRole: 'platform-engineer'
                });
                
                if (response.success) {
                    vscode.window.showInformationMessage('Authentication state reset');
                }
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Auth reset failed: ${error}`);
        }
    }

    async forceAzureReauth(): Promise<void> {
        try {
            const response = await this.mcpClient.sendSemanticQuery({
                query: 'force azure reauthentication',
                userRole: 'platform-engineer'
            });
            
            if (response.success) {
                vscode.window.showInformationMessage('Azure reauthentication initiated');
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Azure reauth failed: ${error}`);
        }
    }

    async configureAzureManually(): Promise<void> {
        try {
            const tenantId = await vscode.window.showInputBox({
                prompt: 'Enter Azure Tenant ID',
                placeHolder: 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
            });

            if (tenantId) {
                const subscriptionId = await vscode.window.showInputBox({
                    prompt: 'Enter Azure Subscription ID',
                    placeHolder: 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx'
                });

                if (subscriptionId) {
                    // Store configuration
                    const config = vscode.workspace.getConfiguration('platform-mcp-universal');
                    await config.update('azureTenantId', tenantId, vscode.ConfigurationTarget.Workspace);
                    await config.update('azureSubscriptionId', subscriptionId, vscode.ConfigurationTarget.Workspace);
                    
                    vscode.window.showInformationMessage('Azure configuration saved');
                }
            }
        } catch (error) {
            vscode.window.showErrorMessage(`Manual config failed: ${error}`);
        }
    }

    private getWebviewContent(markdown: string): string {
        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Platform Results</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            line-height: 1.6;
            color: var(--vscode-editor-foreground);
            background-color: var(--vscode-editor-background);
            padding: 20px;
        }
        pre {
            background-color: var(--vscode-textBlockQuote-background);
            padding: 10px;
            border-radius: 4px;
            overflow-x: auto;
        }
        code {
            background-color: var(--vscode-textBlockQuote-background);
            padding: 2px 4px;
            border-radius: 2px;
        }
        .success { color: var(--vscode-gitDecoration-addedResourceForeground); }
        .error { color: var(--vscode-gitDecoration-deletedResourceForeground); }
        .warning { color: var(--vscode-gitDecoration-modifiedResourceForeground); }
    </style>
</head>
<body>
    <div id="content">${this.markdownToHtml(markdown)}</div>
</body>
</html>`;
    }

    private markdownToHtml(markdown: string): string {
        // Basic markdown to HTML conversion
        return markdown
            .replace(/^### (.*$)/gim, '<h3>$1</h3>')
            .replace(/^## (.*$)/gim, '<h2>$1</h2>')
            .replace(/^# (.*$)/gim, '<h1>$1</h1>')
            .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
            .replace(/\*(.*?)\*/g, '<em>$1</em>')
            .replace(/`(.*?)`/g, '<code>$1</code>')
            .replace(/```([\s\S]*?)```/g, '<pre><code>$1</code></pre>')
            .replace(/\n/g, '<br>');
    }
}