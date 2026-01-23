import * as vscode from 'vscode';
import { McpClient, ChatResponse } from './services/mcpClient';
import { WorkspaceService } from './services/workspaceService';

/**
 * Chat participant commands mapped to agents.
 * These correspond to the Microsoft Agent Framework specialized agents.
 */
const AGENT_COMMANDS: Record<string, string> = {
    'infrastructure': 'InfrastructureAgent',
    'compliance': 'ComplianceAgent',
    'cost': 'CostManagementAgent',
    'discover': 'DiscoveryAgent',
    'knowledge': 'KnowledgeBaseAgent',
    'config': 'ConfigurationAgent'
};

/**
 * Creates and registers the GitHub Copilot Chat participant.
 */
export function createChatParticipant(
    context: vscode.ExtensionContext,
    mcpClient: McpClient,
    outputChannel: vscode.OutputChannel
): vscode.ChatParticipant {
    
    const participant = vscode.chat.createChatParticipant('platform', async (request, context, response, token) => {
        return handleChatRequest(request, context, response, token, mcpClient, outputChannel);
    });

    participant.iconPath = vscode.Uri.joinPath(context.extensionUri, 'media', 'icon.png');
    
    return participant;
}

/**
 * Handle incoming chat requests from GitHub Copilot.
 */
async function handleChatRequest(
    request: vscode.ChatRequest,
    chatContext: vscode.ChatContext,
    response: vscode.ChatResponseStream,
    token: vscode.CancellationToken,
    mcpClient: McpClient,
    outputChannel: vscode.OutputChannel
): Promise<vscode.ChatResult> {
    
    const command = request.command;
    const userMessage = request.prompt;
    
    outputChannel.appendLine(`Chat request: command=${command || 'none'}, prompt="${userMessage.substring(0, 100)}..."`);

    // Build conversation history from context
    const conversationHistory = buildConversationHistory(chatContext);

    // Determine target agent from command
    const targetAgent = command ? AGENT_COMMANDS[command] : undefined;

    try {
        // Show progress
        response.progress('Sending request to Platform Engineering Copilot...');

        // Call MCP server
        const result = await mcpClient.sendChatMessage(userMessage, conversationHistory, targetAgent);

        if (token.isCancellationRequested) {
            return { metadata: { cancelled: true } };
        }

        // Stream the response
        await streamResponse(result, response, outputChannel);

        // Handle templates if present
        if (result.templates && result.templates.length > 0) {
            await handleTemplates(result.templates, response, outputChannel);
        }

        // Show agent routing info if available
        if (result.agentUsed) {
            response.markdown(`\n\n---\n*Processed by: ${result.agentUsed}*`);
        }

        return {
            metadata: {
                agentUsed: result.agentUsed,
                conversationId: result.conversationId,
                templateCount: result.templates?.length || 0
            }
        };

    } catch (error: any) {
        outputChannel.appendLine(`Chat error: ${error.message}`);
        response.markdown(`❌ **Error:** ${error.message}\n\nPlease check that the MCP server is running and configured correctly.`);
        
        // Show configure button
        response.button({
            command: 'platform.configure',
            title: 'Configure Connection'
        });

        return { 
            metadata: { error: error.message },
            errorDetails: {
                message: error.message
            }
        };
    }
}

/**
 * Build conversation history from chat context.
 */
function buildConversationHistory(chatContext: vscode.ChatContext): Array<{ role: string; content: string }> {
    const history: Array<{ role: string; content: string }> = [];
    
    for (const turn of chatContext.history) {
        if (turn instanceof vscode.ChatRequestTurn) {
            history.push({
                role: 'user',
                content: turn.prompt
            });
        } else if (turn instanceof vscode.ChatResponseTurn) {
            // Extract text from response parts
            let content = '';
            for (const part of turn.response) {
                if (part instanceof vscode.ChatResponseMarkdownPart) {
                    content += part.value.value;
                }
            }
            if (content) {
                history.push({
                    role: 'assistant',
                    content
                });
            }
        }
    }
    
    return history;
}

/**
 * Stream the response to the chat window.
 */
async function streamResponse(
    result: ChatResponse,
    response: vscode.ChatResponseStream,
    outputChannel: vscode.OutputChannel
): Promise<void> {
    const content = result.content || result.message || '';
    
    // Split into paragraphs for better streaming effect
    const paragraphs = content.split(/\n\n+/);
    
    for (const paragraph of paragraphs) {
        if (paragraph.trim()) {
            response.markdown(paragraph + '\n\n');
        }
    }
}

/**
 * Handle template artifacts from the response.
 */
async function handleTemplates(
    templates: Array<{ id: string; name: string; type: string; content?: string }>,
    response: vscode.ChatResponseStream,
    outputChannel: vscode.OutputChannel
): Promise<void> {
    
    response.markdown('\n\n### 📄 Generated Templates\n\n');
    
    for (const template of templates) {
        const icon = getTemplateIcon(template.type);
        response.markdown(`${icon} **${template.name}** (${template.type})\n\n`);
        
        if (template.content) {
            // Show code block with content
            const language = getLanguageForTemplateType(template.type);
            response.markdown(`\`\`\`${language}\n${template.content}\n\`\`\`\n\n`);
        }
        
        // Add buttons for template actions
        response.button({
            command: 'platform.saveTemplate',
            title: `💾 Save ${template.name}`,
            arguments: [template]
        });
    }

    // Add "Apply All" button if multiple templates
    if (templates.length > 1) {
        response.button({
            command: 'platform.applyAllTemplates',
            title: '📁 Save All Templates to Workspace',
            arguments: [templates]
        });
    }
}

function getTemplateIcon(type: string): string {
    const icons: Record<string, string> = {
        'bicep': '🔷',
        'terraform': '🟣',
        'kubernetes': '🎡',
        'arm': '📘',
        'dockerfile': '🐳',
        'yaml': '📋',
        'json': '📄'
    };
    return icons[type.toLowerCase()] || '📄';
}

function getLanguageForTemplateType(type: string): string {
    const languages: Record<string, string> = {
        'bicep': 'bicep',
        'terraform': 'hcl',
        'kubernetes': 'yaml',
        'arm': 'json',
        'dockerfile': 'dockerfile',
        'yaml': 'yaml',
        'json': 'json'
    };
    return languages[type.toLowerCase()] || 'text';
}
