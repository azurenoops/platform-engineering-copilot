import * as vscode from 'vscode';
import { McpClient, McpChatResponse } from './services/mcpClient';
import { config } from './config';
import { showShareMenu } from './services/exportService';
import { WorkspaceService } from './services/workspaceService';

/**
 * Platform Chat Participant Handler
 * Integrates Platform Engineering Copilot with GitHub Copilot Chat
 */
export class PlatformChatParticipant implements vscode.Disposable {
    private participant: vscode.ChatParticipant;
    private conversationHistory: Map<string, string[]> = new Map();
    private chatSessionIds: Map<string, string> = new Map();
    private workspaceService: WorkspaceService;

    constructor(
        private context: vscode.ExtensionContext,
        private apiClient: McpClient
    ) {
        // Initialize workspace service
        this.workspaceService = new WorkspaceService();

        // Create chat participant
        this.participant = vscode.chat.createChatParticipant(
            'platform',
            this.handleChatRequest.bind(this)
        );

        // Set metadata
        this.participant.iconPath = vscode.Uri.joinPath(
            context.extensionUri,
            'media',
            'icon.png'
        );

        config.info('Platform chat participant initialized');
    }

    async handleChatRequest(
        request: vscode.ChatRequest,
        chatContext: vscode.ChatContext,
        stream: vscode.ChatResponseStream,
        token: vscode.CancellationToken
    ): Promise<void> {
        try {
            const userMessage = request.prompt.trim();
            const conversationId = this.getConversationId(chatContext);

            config.log(`Chat request: "${userMessage}"`);

            // Check if this is a code analysis request
            if (this.isCodeAnalysisRequest(userMessage)) {
                await this.handleCodeAnalysisRequest(userMessage, stream, token);
                return;
            }

            // Add thinking indicator
            stream.progress('Analyzing your request...');

            // Send to MCP server
            const response = await this.apiClient.sendChatMessage(
                userMessage,
                conversationId,
                {
                    source: 'github-copilot-chat',
                    vscodeVersion: vscode.version,
                    hasWorkspace: !!vscode.workspace.workspaceFolders
                }
            );

            // Handle response - pass conversationId for template lookup
            await this.handleSuccessResponse(response, stream, response.conversationId || conversationId);

            // Handle follow-up if needed
            if (response.requiresFollowUp && response.followUpPrompt) {
                await this.handleFollowUpResponse(response, stream);
            }

            // Store in conversation history
            this.updateConversationHistory(conversationId, userMessage, response.response);

        } catch (error) {
            config.error('Chat request error:', error);
            
            const errorMessage = error instanceof Error ? error.message : 'Unknown error';
            stream.markdown(`## ❌ Error\n\n${errorMessage}\n\n`);
            
            if (errorMessage.includes('Could not reach MCP server')) {
                stream.markdown('**Troubleshooting:**\n');
                stream.markdown('1. Ensure MCP server is running: `curl http://localhost:5100/health`\n');
                stream.markdown('2. Check the MCP server URL in settings: `platform-copilot.apiUrl`\n');
                stream.markdown('3. Run command: **Platform Copilot: Check Platform API Health**\n');
            }
        }
    }

    private async handleSuccessResponse(
        response: McpChatResponse,
        stream: vscode.ChatResponseStream,
        conversationId?: string
    ): Promise<void> {
        // Determine icon based on intent type
        let icon = '✅';
        if (response.intentType === 'compliance') {
            icon = '🔒';
        }

        stream.markdown(`## ${icon} ${this.getIntentTitle(response.intentType)}\n\n`);
        stream.markdown(`${response.response}\n\n`);

        // Add metadata if available
        if (response.metadata) {
            await this.renderMetadata(response.metadata, stream);
        }

        // Add action buttons if applicable
        if (response.metadata?.resourceId) {
            const portalUrl = this.getAzurePortalUrl(response.metadata.resourceId);
            stream.button({
                command: 'vscode.open',
                title: 'View in Azure Portal',
                arguments: [vscode.Uri.parse(portalUrl)]
            });
        }

        // Add share/export buttons for compliance reports
        if (response.intentType === 'compliance') {
            stream.button({
                command: 'platform-copilot.shareReport',
                title: '📤 Share Report',
                arguments: [response.response]
            });
            
            stream.button({
                command: 'platform-copilot.exportReport',
                title: '💾 Export Report',
                arguments: [response.response]
            });
            
            stream.button({
                command: 'platform-copilot.copyToClipboard',
                title: '📋 Copy to Clipboard',
                arguments: [response.response]
            });
        }

        // Check for infrastructure templates - try to fetch from database first
        config.log(`Checking for infrastructure templates - intentType: ${response.intentType}`);
        const looksLikeInfraResponse = this.looksLikeInfrastructureResponse(response.response);
        config.log(`Looks like infrastructure response: ${looksLikeInfraResponse}`);
        
        if (response.intentType === 'infrastructure' || looksLikeInfraResponse) {
            // Try to fetch templates from database
            let templates: Map<string, string> = new Map();
            
            try {
                // First try by conversation ID
                if (conversationId) {
                    config.log(`Fetching templates from DB for conversation: ${conversationId}`);
                    const dbResponse = await this.apiClient.getTemplatesByConversationId(conversationId);
                    if (dbResponse.success && dbResponse.templates && dbResponse.templates.length > 0) {
                        for (const template of dbResponse.templates) {
                            for (const file of template.files) {
                                templates.set(file.fileName, file.content);
                            }
                        }
                        config.log(`Fetched ${templates.size} file(s) from DB by conversation ID`);
                    }
                }
                
                // If no templates found by conversation ID, try latest
                if (templates.size === 0) {
                    config.log(`Fetching latest template from DB`);
                    const latestResponse = await this.apiClient.getLatestTemplate();
                    if (latestResponse.success && latestResponse.template) {
                        for (const file of latestResponse.template.files) {
                            templates.set(file.fileName, file.content);
                        }
                        config.log(`Fetched ${templates.size} file(s) from latest template`);
                    }
                }
            } catch (error) {
                config.error('Failed to fetch templates from DB:', error);
                // Fall back to extracting from response
                templates = this.extractInfrastructureTemplates(response.response);
                config.log(`Fallback: Extracted ${templates.size} template(s) from response`);
            }
            
            config.log(`Total templates found: ${templates.size}`);
            for (const [filename, content] of templates.entries()) {
                config.log(`  - ${filename} (${content.length} bytes)`);
            }
            
            if (templates.size > 0) {
                stream.markdown('\n---\n\n');
                stream.markdown('### 💾 Save to Workspace\n\n');
                stream.markdown('Infrastructure templates are ready. Click below to save them to your workspace:\n\n');
                
                stream.button({
                    command: 'platform-copilot.createWorkspace',
                    title: '📁 Create Project in Workspace',
                    arguments: [templates, this.detectTemplateType(response.response)]
                });
                
                stream.button({
                    command: 'platform-copilot.saveTemplate',
                    title: '💾 Save Single File',
                    arguments: [templates]
                });
            }
        }
    }

    /**
     * Check if response looks like infrastructure template generation
     * (without requiring actual code blocks)
     */
    private looksLikeInfrastructureResponse(response: string): boolean {
        const indicators = [
            /Generated \d+ Files/i,
            /\.bicep/i,
            /\.tf\b/i,
            /main\.bicep/i,
            /main\.tf/i,
            /terraform/i,
            /Bicep template/i,
            /infrastructure template/i,
            /Create Project in Workspace/i,
            /📄 \*\*Generated/i
        ];
        
        return indicators.some(pattern => pattern.test(response));
    }

    private async handleFollowUpResponse(
        response: McpChatResponse,
        stream: vscode.ChatResponseStream
    ): Promise<void> {
        stream.markdown('---\n\n');
        stream.markdown(`## 🔄 Follow-up Needed\n\n`);
        stream.markdown(`${response.followUpPrompt}\n\n`);

        // Add follow-up action buttons based on the response type
        if (response.intentType === 'compliance') {
            stream.button({
                command: 'workbench.action.chat.newChat',
                title: 'Start New Compliance Chat',
                arguments: ['@platform Run detailed compliance analysis']
            });
        }

        // Generic retry button
        stream.button({
            command: 'workbench.action.chat.newChat',
            title: 'Ask Follow-up Question',
            arguments: ['@platform ']
        });
    }

    private async renderMetadata(
        metadata: Record<string, any>,
        stream: vscode.ChatResponseStream
    ): Promise<void> {
        // Render cost information
        if (metadata.cost !== undefined) {
            stream.markdown(`**Estimated Cost:** $${metadata.cost.toFixed(2)}/month\n\n`);
        }

        // Render compliance score
        if (metadata.complianceScore !== undefined) {
            const score = Math.round(metadata.complianceScore);
            const emoji = score >= 90 ? '🟢' : score >= 70 ? '🟡' : '🔴';
            stream.markdown(`**Compliance Score:** ${emoji} ${score}%\n\n`);
        }

        // Render resource details
        if (metadata.resourceId) {
            stream.markdown(`**Resource ID:**\n\`\`\`\n${metadata.resourceId}\n\`\`\`\n\n`);
        }

        if (metadata.location) {
            stream.markdown(`**Location:** ${metadata.location}\n\n`);
        }
    }

    private getIntentTitle(intentType?: string): string {
        const titles: Record<string, string> = {
            'compliance': 'Compliance Assessment'            
        };

        return titles[intentType || ''] || 'Platform Engineering Copilot Response';
    }

    private getAzurePortalUrl(resourceId: string): string {
        // Determine if MAG or commercial Azure
        const isMag = resourceId.includes('.us') || config.apiUrl.includes('.us');
        const portalDomain = isMag ? 'portal.azure.us' : 'portal.azure.com';
        return `https://${portalDomain}/#resource${resourceId}`;
    }

    private getConversationId(chatContext: vscode.ChatContext): string {
        // Use a stable conversation ID for the entire chat session
        // Create a unique key based on the chat history to identify the same session
        // When a user starts a new chat, the history will be empty, creating a new session
        
        // Create a session key from the first message in the history (if any)
        const sessionKey = chatContext.history.length > 0 
            ? `session-${chatContext.history[0].participant}-${chatContext.history.length}`
            : `new-session-${Date.now()}`;
        
        // For existing sessions, use a simplified key based on participant
        const stableKey = chatContext.history.length > 0
            ? `session-${chatContext.history[0].participant}`
            : sessionKey;
        
        // Check if we already have an ID for this chat session
        if (!this.chatSessionIds.has(stableKey)) {
            // Generate a new stable ID for this chat session
            const sessionId = `github-copilot-${Date.now()}-${Math.random().toString(36).substring(7)}`;
            this.chatSessionIds.set(stableKey, sessionId);
            config.log(`📌 New chat session created: ${sessionId} (key: ${stableKey})`);
        }
        
        return this.chatSessionIds.get(stableKey)!;
    }

    private updateConversationHistory(
        conversationId: string,
        userMessage: string,
        response: string
    ): void {
        const history = this.conversationHistory.get(conversationId) || [];
        history.push(`User: ${userMessage}`);
        history.push(`Assistant: ${response}`);
        this.conversationHistory.set(conversationId, history);

        // Clean up old conversations (keep last 10)
        if (this.conversationHistory.size > 10) {
            const oldestKey = this.conversationHistory.keys().next().value;
            if (oldestKey) {
                this.conversationHistory.delete(oldestKey);
            }
        }
    }

    private isCodeAnalysisRequest(message: string): boolean {
        const lowerMessage = message.toLowerCase();
        return lowerMessage.includes('analyze code') ||
               lowerMessage.includes('scan code') ||
               lowerMessage.includes('code compliance') ||
               lowerMessage.includes('security scan') ||
               lowerMessage.includes('analyze this file') ||
               lowerMessage.includes('check this code') ||
               (lowerMessage.includes('analyze') && (lowerMessage.includes('file') || lowerMessage.includes('current')));
    }

    private async handleCodeAnalysisRequest(
        message: string,
        stream: vscode.ChatResponseStream,
        token: vscode.CancellationToken
    ): Promise<void> {
        const editor = vscode.window.activeTextEditor;
        
        if (!editor) {
            stream.markdown('## 🔍 Code Analysis\n\n');
            stream.markdown('❌ No file is currently open for analysis.\n\n');
            stream.markdown('Please open a file and try again, or use:\n');
            stream.button({
                command: 'platform.analyzeWorkspace',
                title: 'Analyze Entire Workspace',
                arguments: []
            });
            return;
        }

        const document = editor.document;
        const codeContent = document.getText();
        const fileName = document.fileName;
        
        if (codeContent.trim().length === 0) {
            stream.markdown('## 🔍 Code Analysis\n\n');
            stream.markdown('❌ Current file is empty.\n\n');
            return;
        }

        stream.progress('Analyzing code for compliance and security...');
        stream.markdown('## 🔍 Code Compliance Analysis\n\n');
        stream.markdown(`**Analyzing**: ${fileName}\n\n`);

        try {
            const result = await this.apiClient.analyzeCodeForCompliance(
                codeContent,
                fileName,
                vscode.workspace.workspaceFolders?.[0]?.uri.toString()
            );

            if (result.success) {
                // Display risk level with appropriate emoji
                const riskEmoji = this.getRiskEmoji(result.riskLevel);
                stream.markdown(`**Risk Level**: ${riskEmoji} ${result.riskLevel}\n\n`);
                
                // Show compliance report
                stream.markdown('### Compliance Report\n\n');
                stream.markdown(`${result.complianceReport}\n\n`);

                // Show findings if any
                if (result.findings.length > 0) {
                    stream.markdown('### Key Findings\n\n');
                    result.findings.forEach((finding, index) => {
                        stream.markdown(`${index + 1}. ${finding}\n`);
                    });
                    stream.markdown('\n');
                }

                // Show recommendations if any
                if (result.recommendations.length > 0) {
                    stream.markdown('### Recommendations\n\n');
                    result.recommendations.forEach((rec, index) => {
                        stream.markdown(`${index + 1}. ${rec}\n`);
                    });
                    stream.markdown('\n');
                }

                // Add action buttons
                stream.button({
                    command: 'platform.analyzeWorkspace',
                    title: 'Analyze Entire Workspace',
                    arguments: []
                });

                // Handle follow-up if needed
                if (result.requiresFollowUp && result.followUpPrompt) {
                    stream.markdown('### Follow-up Required\n\n');
                    stream.markdown(`${result.followUpPrompt}\n\n`);
                }

            } else {
                stream.markdown(`❌ Analysis failed: ${result.errors.join(', ')}\n\n`);
            }

        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : 'Unknown error';
            stream.markdown(`❌ Analysis failed: ${errorMessage}\n\n`);
            
            if (errorMessage.includes('Could not reach MCP server')) {
                stream.markdown('**Troubleshooting:**\n');
                stream.markdown('1. Ensure MCP server is running\n');
                stream.markdown('2. Check Platform Copilot settings\n');
                stream.button({
                    command: 'platform.checkHealth',
                    title: 'Check API Health',
                    arguments: []
                });
            }
        }
    }

    private getRiskEmoji(riskLevel: string): string {
        switch (riskLevel.toLowerCase()) {
            case 'critical': return '🔴';
            case 'high': return '🟠';
            case 'medium': return '🟡';
            case 'low': return '🟢';
            default: return '⚪';
        }
    }

    /**
     * Check if response contains infrastructure templates (Bicep, Terraform, Kubernetes)
     */
    private containsInfrastructureTemplates(response: string): boolean {
        const templatePatterns = [
            /```bicep/i,
            /```terraform/i,
            /```hcl/i,
            /```yaml[\s\S]*?apiVersion:/i,
            /```yml[\s\S]*?apiVersion:/i,
            /```json[\s\S]*?"type":\s*"Microsoft\./i, // ARM templates
        ];

        return templatePatterns.some(pattern => pattern.test(response));
    }

    /**
     * Detect the primary template type from response
     */
    private detectTemplateType(response: string): 'bicep' | 'terraform' | 'kubernetes' | 'arm' | null {
        if (/```bicep/i.test(response)) {
            return 'bicep';
        }
        if (/```terraform/i.test(response) || /```hcl/i.test(response)) {
            return 'terraform';
        }
        if (/```(yaml|yml)[\s\S]*?apiVersion:/i.test(response)) {
            return 'kubernetes';
        }
        if (/```json[\s\S]*?"type":\s*"Microsoft\./i.test(response)) {
            return 'arm';
        }
        return null;
    }

    /**
     * Extract infrastructure templates from markdown code blocks
     * Returns a map of filename -> content
     */
    private extractInfrastructureTemplates(response: string): Map<string, string> {
        const templates = new Map<string, string>();
        
        // First try to extract from <details> sections with filenames
        const detailsRegex = /<details>\s*<summary>📁\s*<strong>([^<]+)<\/strong>/g;
        const codeBlockRegex = /```(\w+)\n([\s\S]*?)```/g;
        
        // Build map of positions to filenames from details/summary tags
        const filenamePositions = new Map<number, string>();
        let detailsMatch;
        while ((detailsMatch = detailsRegex.exec(response)) !== null) {
            const filename = detailsMatch[1].trim();
            const position = detailsMatch.index;
            filenamePositions.set(position, filename);
        }
        
        // Also check for markdown headers (### filename.bicep or ### 📁 filename.bicep)
        const headerRegex = /###\s*(?:📁\s*)?([^\n]+)/g;
        let headerMatch;
        while ((headerMatch = headerRegex.exec(response)) !== null) {
            const filename = headerMatch[1].trim();
            const position = headerMatch.index;
            // Only add if it looks like a filename
            if (filename.match(/\.(bicep|tf|yaml|yml|json)$/)) {
                filenamePositions.set(position, filename);
            }
        }
        
        // Extract code blocks and match with nearest preceding filename
        let match;
        let bicepCount = 0;
        let terraformCount = 0;
        let k8sCount = 0;
        let jsonCount = 0;

        while ((match = codeBlockRegex.exec(response)) !== null) {
            const lang = match[1].toLowerCase();
            const content = match[2].trim();
            const codeBlockPosition = match.index;

            if (!content) {
                continue;
            }

            // Find the nearest filename before this code block
            let nearestFilename: string | undefined;
            let nearestDistance = Infinity;
            
            for (const [pos, filename] of filenamePositions.entries()) {
                if (pos < codeBlockPosition) {
                    const distance = codeBlockPosition - pos;
                    if (distance < nearestDistance) {
                        nearestDistance = distance;
                        nearestFilename = filename;
                    }
                }
            }

            // Extract Bicep templates
            if (lang === 'bicep') {
                const filename = nearestFilename || (bicepCount === 0 ? 'main.bicep' : `module${bicepCount}.bicep`);
                templates.set(filename, content);
                bicepCount++;
                // Remove used filename so it's not reused
                if (nearestFilename) {
                    for (const [pos, fname] of filenamePositions.entries()) {
                        if (fname === nearestFilename) {
                            filenamePositions.delete(pos);
                            break;
                        }
                    }
                }
            }
            // Extract Terraform templates
            else if (lang === 'terraform' || lang === 'hcl') {
                const filename = nearestFilename || (terraformCount === 0 ? 'main.tf' : `${this.inferTerraformFilename(content, terraformCount)}.tf`);
                templates.set(filename, content);
                terraformCount++;
                if (nearestFilename) {
                    for (const [pos, fname] of filenamePositions.entries()) {
                        if (fname === nearestFilename) {
                            filenamePositions.delete(pos);
                            break;
                        }
                    }
                }
            }
            // Extract Kubernetes manifests
            else if ((lang === 'yaml' || lang === 'yml') && content.includes('apiVersion:')) {
                const resourceKind = this.extractK8sKind(content) || 'resource';
                const filename = nearestFilename || (k8sCount === 0 
                    ? `${resourceKind.toLowerCase()}.yaml` 
                    : `${resourceKind.toLowerCase()}-${k8sCount}.yaml`);
                templates.set(filename, content);
                k8sCount++;
                if (nearestFilename) {
                    for (const [pos, fname] of filenamePositions.entries()) {
                        if (fname === nearestFilename) {
                            filenamePositions.delete(pos);
                            break;
                        }
                    }
                }
            }
            // Extract ARM templates (JSON)
            else if (lang === 'json' && content.includes('"type": "Microsoft.')) {
                const filename = nearestFilename || (jsonCount === 0 ? 'azuredeploy.json' : `template${jsonCount}.json`);
                templates.set(filename, content);
                jsonCount++;
                if (nearestFilename) {
                    for (const [pos, fname] of filenamePositions.entries()) {
                        if (fname === nearestFilename) {
                            filenamePositions.delete(pos);
                            break;
                        }
                    }
                }
            }
            // Extract parameter files
            else if (lang === 'json' && (content.includes('"parameters":') || content.includes('"$schema":') && content.includes('deploymentParameters'))) {
                const filename = nearestFilename || 'parameters.json';
                templates.set(filename, content);
                jsonCount++;
                if (nearestFilename) {
                    for (const [pos, fname] of filenamePositions.entries()) {
                        if (fname === nearestFilename) {
                            filenamePositions.delete(pos);
                            break;
                        }
                    }
                }
            }
        }

        return templates;
    }

    /**
     * Infer Terraform filename from content (variables.tf, outputs.tf, providers.tf, etc.)
     */
    private inferTerraformFilename(content: string, index: number): string {
        if (content.includes('variable "')) {
            return 'variables';
        }
        if (content.includes('output "')) {
            return 'outputs';
        }
        if (content.includes('provider "')) {
            return 'providers';
        }
        if (content.includes('terraform {')) {
            return 'versions';
        }
        return `main${index > 0 ? index : ''}`;
    }

    /**
     * Extract Kubernetes resource kind from YAML content
     */
    private extractK8sKind(content: string): string | null {
        const kindMatch = content.match(/kind:\s*(\w+)/);
        return kindMatch ? kindMatch[1] : null;
    }

    dispose(): void {
        this.participant.dispose();
        this.conversationHistory.clear();
        config.info('Platform chat participant disposed');
    }
}
