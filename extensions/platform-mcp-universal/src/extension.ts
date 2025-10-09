import * as vscode from 'vscode';
import { SimplePlatformChatParticipant } from './simplePlatformChatParticipant';
import { PlatformMCPClient } from './mcpClient';
import { PlatformCommands } from './commands';

let mcpClient: PlatformMCPClient;
let platformChatParticipant: SimplePlatformChatParticipant;
let platformCommands: PlatformCommands;

export function activate(context: vscode.ExtensionContext) {
    console.log('🚀 Platform Engineering Copilot extension is now active!');
    console.log('📍 Extension path:', context.extensionPath);
    console.log('📋 VS Code version:', vscode.version);
    
    // Show a notification to confirm extension is loading
    vscode.window.showInformationMessage('Platform Engineering extension activated! 🚀');

    try {
        // Initialize MCP client for platform engineering
        mcpClient = new PlatformMCPClient();
        console.log('✅ MCP Client initialized');
        
        // Initialize platform commands
        platformCommands = new PlatformCommands(context, mcpClient);
        console.log('✅ Platform Commands initialized');
        
        // Initialize proper chat participant with MCP integration
        try {
            console.log('🎯 Creating SimplePlatformChatParticipant with MCP integration...');
            platformChatParticipant = new SimplePlatformChatParticipant(context, mcpClient);
            console.log('✅ SimplePlatformChatParticipant created successfully!');
            
            // Show success notification
            vscode.window.showInformationMessage('✅ @platforms chat participant registered successfully!');
            
        } catch (chatError) {
            console.error('❌ Chat participant registration failed:', chatError);
            vscode.window.showWarningMessage(`Chat participant registration failed: ${chatError}. You may need to reload VS Code.`);
        }
        
    } catch (error) {
        console.error('❌ Error during extension activation:', error);
        vscode.window.showErrorMessage(`Platform Engineering extension activation failed: ${error}`);
    }

    // Validate MCP connection after initialization
    validateMCPConnection();

    // Register platform engineering commands
    const commands = [
        vscode.commands.registerCommand('platform.provisionInfrastructure', 
            () => platformCommands.provisionInfrastructure()),
        vscode.commands.registerCommand('platform.deployContainer', 
            () => platformCommands.deployContainer()),
        vscode.commands.registerCommand('platform.createMonitoringDashboard', 
            () => platformCommands.createMonitoringDashboard()),
        vscode.commands.registerCommand('platform.runSecurityScan', 
            () => platformCommands.runSecurityScan()),
        vscode.commands.registerCommand('platform.runAtoComplianceScan', 
            () => platformCommands.runAtoComplianceScan()),

        // Authentication commands
        vscode.commands.registerCommand('platform.authenticateAzure',
            () => platformCommands.authenticateAzure()),
        vscode.commands.registerCommand('platform.authenticateGitHub',
            () => platformCommands.authenticateGitHub()),
        vscode.commands.registerCommand('platform.checkAuthStatus',
            () => platformCommands.checkAuthStatus()),
        vscode.commands.registerCommand('platform.selectAzureCloud',
            () => platformCommands.selectAzureCloud()),
        vscode.commands.registerCommand('platform.signOutAll',
            () => platformCommands.signOutAll()),

        // Diagnostics and cache commands
        vscode.commands.registerCommand('platform.checkMCPConnections',
            () => platformCommands.checkMCPConnections()),
        vscode.commands.registerCommand('platform.reconnectMCPServers',
            () => platformCommands.reconnectMCPServers()),
        vscode.commands.registerCommand('platform.viewCacheStats',
            () => platformCommands.viewCacheStats()),
        vscode.commands.registerCommand('platform.clearCache',
            () => platformCommands.clearCache()),

        // Authentication troubleshooting commands
        vscode.commands.registerCommand('platform.resetAuthenticationState',
            () => platformCommands.resetAuthenticationState()),
        vscode.commands.registerCommand('platform.forceAzureReauth',
            () => platformCommands.forceAzureReauth()),
        vscode.commands.registerCommand('platform.configureAzureManually',
            () => platformCommands.configureAzureManually()),
        
        // Universal parser test command
        vscode.commands.registerCommand('platform.testUniversalParser',
            async () => {
                try {
                    const { testUniversalMcpParser } = await import('./universalMcpParserTest');
                    testUniversalMcpParser();
                    vscode.window.showInformationMessage('✅ Universal MCP Parser test completed! Check console for details.');
                } catch (error) {
                    vscode.window.showErrorMessage(`Universal parser test failed: ${error}`);
                }
            }),

        // Test the exact user scenario
        vscode.commands.registerCommand('platform.testUserScenario',
            async () => {
                try {
                    const { UniversalMcpParser } = await import('./universalMcpParser');
                    
                    // Simulate the exact response the user is seeing
                    const userResponse = {
                        content: [{
                            text: '{"success":true,"message":"Service template \'test-service\' created for api using dotnet (local mode)","templateId":"82c7c042-940d-483b-bb94-7ba31d17a7d1","action":"create","serviceName":"test-service","serviceType":"api","framework":"dotnet","files":[{"name":"deployment.yaml","description":"Kubernetes deployment manifest with container specifications"}]}'
                        }]
                    };
                    
                    console.log('🧪 Testing user scenario with response:', JSON.stringify(userResponse, null, 2));
                    
                    const result = UniversalMcpParser.parseResponse(userResponse);
                    console.log('🎯 Parsed result:', JSON.stringify(result, null, 2));
                    
                    if (result && result.success) {
                        vscode.window.showInformationMessage(`✅ Parser worked! Service: ${result.data?.serviceName}, Files: ${result.files?.length}`);
                    } else {
                        vscode.window.showErrorMessage(`❌ Parser failed. Result: ${JSON.stringify(result)}`);
                    }
                } catch (error) {
                    vscode.window.showErrorMessage(`User scenario test failed: ${error}`);
                }
            }),

        // Debug command to verify extension status
        vscode.commands.registerCommand('platform.debugStatus',
            async () => {
                try {
                    const mcpHealthy = await mcpClient.checkHealth();
                    const status = {
                        extensionActive: true,
                        mcpClientExists: !!mcpClient,
                        mcpClientHealthy: mcpHealthy.healthy,
                        chatParticipantExists: !!platformChatParticipant,
                        vsCodeChatApiAvailable: !!(vscode.chat && vscode.chat.createChatParticipant)
                    };
                    const message = `Platform Extension Status:\n${JSON.stringify(status, null, 2)}`;
                    vscode.window.showInformationMessage(message);
                    console.log('🔍 Platform Extension Debug Status:', status);
                } catch (error) {
                    vscode.window.showErrorMessage(`Debug status error: ${error}`);
                    console.error('❌ Debug status error:', error);
                }
            }),       
    ];

    context.subscriptions.push(...commands);

    // Add status bar item for MCP connection status
    const statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
    statusBarItem.text = '$(loading~spin) Platform MCP';
    statusBarItem.tooltip = 'Platform Engineering MCP Status - Checking connection...';
    statusBarItem.command = 'platform.checkMCPConnections';
    statusBarItem.show();
    context.subscriptions.push(statusBarItem);

    // Create a function to refresh status bar on demand
    const refreshStatusBar = (showLoading: boolean = false) => updateStatusBar(statusBarItem, showLoading);
    
    // Update status bar based on connection status (show loading on initial check)
    refreshStatusBar(true);
    
    // Check connection status every 30 seconds (no loading spinner for periodic updates)
    const statusInterval = setInterval(() => refreshStatusBar(false), 30000);
    context.subscriptions.push({ dispose: () => clearInterval(statusInterval) });
}

async function validateMCPConnection(): Promise<void> {
    try {
        const health = await mcpClient.checkHealth();
        if (health.healthy) {
            console.log('✅ Platform MCP server connection validated');
        } else {
            console.log('⚠️ Platform MCP server connection failed');
            vscode.window.showWarningMessage(
                'Platform MCP server is not connected. Some features may not work.',
                'Check Connection',
                'Reconnect'
            ).then(selection => {
                if (selection === 'Check Connection') {
                    vscode.commands.executeCommand('platform.checkMCPConnections');
                } else if (selection === 'Reconnect') {
                    vscode.commands.executeCommand('platform.reconnectMCPServers');
                }
            });
        }
    } catch (error) {
        console.error('Error validating MCP connection:', error);
    }
}

async function updateStatusBar(statusBarItem: vscode.StatusBarItem, showLoading: boolean = false): Promise<void> {
    try {
        // Only show loading state if explicitly requested (e.g., initial check)
        if (showLoading) {
            statusBarItem.text = '$(loading~spin) Platform MCP';
            statusBarItem.tooltip = 'Checking MCP connection...';
            statusBarItem.backgroundColor = undefined;
            statusBarItem.color = undefined;
        }

        const health = await mcpClient.checkHealth();
        
        if (health.healthy) {
            // Connected - show checkmark like in the attachment
            statusBarItem.text = '$(check) Platform MCP';
            statusBarItem.tooltip = 'Platform MCP Server: Connected ✓\nClick to view connection details';
            statusBarItem.backgroundColor = undefined;
            statusBarItem.color = new vscode.ThemeColor('statusBar.foreground');
        } else {
            // Disconnected - show warning
            statusBarItem.text = '$(warning) Platform MCP';
            statusBarItem.tooltip = 'Platform MCP Server: Disconnected ⚠️\nClick to reconnect';
            statusBarItem.backgroundColor = new vscode.ThemeColor('statusBarItem.warningBackground');
            statusBarItem.color = new vscode.ThemeColor('statusBarItem.warningForeground');
        }
    } catch (error) {
        // Error state - show error icon
        statusBarItem.text = '$(error) Platform MCP';
        statusBarItem.tooltip = `Platform MCP Server: Error\n${error instanceof Error ? error.message : 'Unknown error'}\nClick to troubleshoot`;
        statusBarItem.backgroundColor = new vscode.ThemeColor('statusBarItem.errorBackground');
        statusBarItem.color = new vscode.ThemeColor('statusBarItem.errorForeground');
        console.error('Status bar update error:', error);
    }
}

export function deactivate() {
    if (mcpClient) {
        // MCP client doesn't have a dispose method, but we can clean up if needed
        console.log('Platform MCP Extension deactivated');
    }
}