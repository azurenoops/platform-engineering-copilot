import * as vscode from 'vscode';
import { createChatParticipant } from './chatParticipant';
import { Config } from './config';
import { McpClient } from './services/mcpClient';

let outputChannel: vscode.OutputChannel;

export function activate(context: vscode.ExtensionContext) {
    outputChannel = vscode.window.createOutputChannel('Platform Engineering Copilot');
    outputChannel.appendLine('Activating Platform Engineering Copilot extension...');

    const config = Config.getInstance();
    const mcpClient = new McpClient(config, outputChannel);

    // Register the chat participant
    const participant = createChatParticipant(context, mcpClient, outputChannel);
    context.subscriptions.push(participant);

    // Register commands
    registerCommands(context, mcpClient, config);

    // Health check on activation
    performHealthCheck(mcpClient);

    outputChannel.appendLine('Platform Engineering Copilot extension activated successfully');
}

function registerCommands(context: vscode.ExtensionContext, mcpClient: McpClient, config: Config) {
    // Check health command
    const checkHealthCmd = vscode.commands.registerCommand('platform.checkHealth', async () => {
        await performHealthCheck(mcpClient, true);
    });
    context.subscriptions.push(checkHealthCmd);

    // Configure API command
    const configureCmd = vscode.commands.registerCommand('platform.configure', async () => {
        const url = await vscode.window.showInputBox({
            prompt: 'Enter MCP Server URL',
            value: config.apiUrl,
            placeHolder: 'http://localhost:5100'
        });
        if (url) {
            await vscode.workspace.getConfiguration('platform-copilot').update('apiUrl', url, true);
            vscode.window.showInformationMessage(`Platform API URL updated to: ${url}`);
        }
    });
    context.subscriptions.push(configureCmd);

    // Analyze current file for compliance
    const analyzeFileCmd = vscode.commands.registerCommand('platform.analyzeCurrentFile', async () => {
        const editor = vscode.window.activeTextEditor;
        if (!editor) {
            vscode.window.showWarningMessage('No active file to analyze');
            return;
        }

        const document = editor.document;
        const content = document.getText();
        const fileName = document.fileName;
        const languageId = document.languageId;

        outputChannel.appendLine(`Analyzing file: ${fileName} (${languageId})`);

        await vscode.window.withProgress({
            location: vscode.ProgressLocation.Notification,
            title: 'Analyzing file for compliance...',
            cancellable: false
        }, async () => {
            try {
                const result = await mcpClient.analyzeCode(content, fileName, languageId);
                showAnalysisResults(result, fileName);
            } catch (error: any) {
                vscode.window.showErrorMessage(`Analysis failed: ${error.message}`);
                outputChannel.appendLine(`Analysis error: ${error.message}`);
            }
        });
    });
    context.subscriptions.push(analyzeFileCmd);

    // Analyze workspace for compliance
    const analyzeWorkspaceCmd = vscode.commands.registerCommand('platform.analyzeWorkspace', async () => {
        const workspaceFolders = vscode.workspace.workspaceFolders;
        if (!workspaceFolders) {
            vscode.window.showWarningMessage('No workspace folder open');
            return;
        }

        const workspacePath = workspaceFolders[0].uri.fsPath;
        outputChannel.appendLine(`Analyzing workspace: ${workspacePath}`);

        await vscode.window.withProgress({
            location: vscode.ProgressLocation.Notification,
            title: 'Analyzing workspace for compliance...',
            cancellable: false
        }, async () => {
            try {
                const result = await mcpClient.analyzeRepository(workspacePath);
                showWorkspaceAnalysisResults(result, workspacePath);
            } catch (error: any) {
                vscode.window.showErrorMessage(`Workspace analysis failed: ${error.message}`);
                outputChannel.appendLine(`Workspace analysis error: ${error.message}`);
            }
        });
    });
    context.subscriptions.push(analyzeWorkspaceCmd);
}

async function performHealthCheck(mcpClient: McpClient, showSuccess: boolean = false) {
    try {
        const isHealthy = await mcpClient.healthCheck();
        if (isHealthy) {
            outputChannel.appendLine('✓ MCP Server is healthy');
            if (showSuccess) {
                vscode.window.showInformationMessage('✓ Platform MCP Server is healthy and responding');
            }
        } else {
            outputChannel.appendLine('✗ MCP Server health check failed');
            vscode.window.showWarningMessage('Platform MCP Server is not responding. Check configuration.');
        }
    } catch (error: any) {
        outputChannel.appendLine(`✗ Health check error: ${error.message}`);
        if (showSuccess) {
            vscode.window.showErrorMessage(`Cannot connect to Platform MCP Server: ${error.message}`);
        }
    }
}

function showAnalysisResults(result: any, fileName: string) {
    const panel = vscode.window.createWebviewPanel(
        'complianceResults',
        `Compliance Analysis: ${fileName.split('/').pop()}`,
        vscode.ViewColumn.Two,
        { enableScripts: true }
    );

    const findings = result.findings || [];
    const controlsChecked = result.controlsChecked || 0;
    const summary = result.summary || 'No summary available';

    panel.webview.html = `
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Compliance Analysis Results</title>
    <style>
        body { font-family: var(--vscode-font-family); padding: 20px; color: var(--vscode-foreground); }
        h1 { color: var(--vscode-textLink-foreground); }
        .summary { background: var(--vscode-textBlockQuote-background); padding: 15px; border-radius: 5px; margin-bottom: 20px; }
        .finding { border: 1px solid var(--vscode-panel-border); padding: 10px; margin: 10px 0; border-radius: 5px; }
        .finding.high { border-left: 4px solid #f44336; }
        .finding.medium { border-left: 4px solid #ff9800; }
        .finding.low { border-left: 4px solid #4caf50; }
        .severity { font-weight: bold; text-transform: uppercase; }
        .control-id { color: var(--vscode-textLink-foreground); }
    </style>
</head>
<body>
    <h1>🛡️ Compliance Analysis Results</h1>
    <div class="summary">
        <strong>Controls Checked:</strong> ${controlsChecked}<br>
        <strong>Findings:</strong> ${findings.length}<br>
        <strong>Summary:</strong> ${summary}
    </div>
    ${findings.length === 0 ? '<p>✅ No compliance issues found!</p>' : findings.map((f: any) => `
        <div class="finding ${f.severity?.toLowerCase() || 'medium'}">
            <span class="severity">${f.severity || 'Medium'}</span> - 
            <span class="control-id">${f.controlId || 'Unknown'}</span><br>
            <strong>${f.title || 'Finding'}</strong><br>
            <p>${f.description || 'No description'}</p>
            ${f.recommendation ? `<p><strong>Recommendation:</strong> ${f.recommendation}</p>` : ''}
        </div>
    `).join('')}
</body>
</html>`;
}

function showWorkspaceAnalysisResults(result: any, workspacePath: string) {
    const panel = vscode.window.createWebviewPanel(
        'workspaceAnalysis',
        'Workspace Compliance Analysis',
        vscode.ViewColumn.Two,
        { enableScripts: true }
    );

    const files = result.filesAnalyzed || 0;
    const findings = result.findings || [];
    const summary = result.summary || 'Analysis complete';

    panel.webview.html = `
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Workspace Analysis</title>
    <style>
        body { font-family: var(--vscode-font-family); padding: 20px; color: var(--vscode-foreground); }
        h1 { color: var(--vscode-textLink-foreground); }
        .stats { display: flex; gap: 20px; margin-bottom: 20px; }
        .stat { background: var(--vscode-textBlockQuote-background); padding: 15px; border-radius: 5px; text-align: center; }
        .stat-value { font-size: 2em; font-weight: bold; color: var(--vscode-textLink-foreground); }
    </style>
</head>
<body>
    <h1>📁 Workspace Compliance Analysis</h1>
    <p><strong>Path:</strong> ${workspacePath}</p>
    <div class="stats">
        <div class="stat"><div class="stat-value">${files}</div>Files Analyzed</div>
        <div class="stat"><div class="stat-value">${findings.length}</div>Findings</div>
    </div>
    <div class="summary">${summary}</div>
</body>
</html>`;
}

export function deactivate() {
    outputChannel?.appendLine('Platform Engineering Copilot extension deactivated');
}
