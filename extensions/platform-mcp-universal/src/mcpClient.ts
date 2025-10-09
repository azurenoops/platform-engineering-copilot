import * as vscode from 'vscode';

// Types for MCP protocol
export interface MCPTool {
    name: string;
    description: string;
    inputSchema: any;
}

export interface MCPToolCall {
    name: string;
    arguments: Record<string, any>;
}

export interface MCPToolResult {
    content: Array<{ type: string; text: string }>;
    isError?: boolean;
}

export interface ToolRequest {
    shouldExecute: boolean;
    toolName: string;
    parameters: Record<string, any>;
    confidence: number;
}

export interface HealthStatus {
    healthy: boolean;
    toolCount: number;
    error?: string;
}

// Enhanced Semantic Kernel Types
export interface SemanticQueryRequest {
    query: string;
    context?: WorkspaceContext;
    userRole?: 'platform-engineer' | 'mission-owner' | 'developer';
    maxConfidence?: number;
}

export interface IntentInfo {
    category: string;
    action: string;
    confidence: number;
    description: string;
}

export interface SemanticQueryResponse {
    success: boolean;
    interpretation?: string;
    confidence?: number;
    intent?: IntentInfo | string;
    extractedParameters?: Record<string, any>;
    recommendedTools?: string[];
    recommendedActions?: SemanticAction[];
    executionPlan?: ExecutionStep[];
    alternatives?: string[];
    reasoning?: string;
    markdown?: string;
    result?: any;
    error?: string;
    // Regular chat response properties
    message?: string;
    semanticResponse?: string;
    suggestions?: string[];
    availableTools?: Array<{Name: string; Description: string}>;
    query?: string;
    entities?: any;
}

export interface SemanticAction {
    tool: string;
    parameters: Record<string, any>;
    description: string;
    order: number;
    requiresApproval?: boolean;
}

export interface ExecutionStep {
    order: number;
    tool: string;
    description: string;
    estimatedDuration?: string;
    dependencies?: number[];
}

export interface WorkspaceContext {
    hasDockerfile?: boolean;
    hasKubernetesManifests?: boolean;
    hasInfrastructureCode?: boolean;
    hasMonitoring?: boolean;
    programmingLanguages?: string[];
    frameworks?: string[];
    cloudResources?: string[];
    currentEnvironment?: string;
}

export class PlatformMCPClient {
    private serverUrl!: string;
    private timeout!: number;
    private enableLogging!: boolean;

    constructor() {
        this.updateConfig();
    }

    updateConfig(): void {
        const config = vscode.workspace.getConfiguration('platform-mcp-universal');
        this.serverUrl = config.get<string>('serverUrl', 'http://localhost:8080');
        this.timeout = config.get<number>('timeout', 30000);
        this.enableLogging = config.get<boolean>('enableLogging', true);
        
        if (this.enableLogging) {
            console.log(`MCP Client configured: ${this.serverUrl}`);
        }
    }

    async checkHealth(): Promise<HealthStatus> {
        try {
            const response = await this.makeRequest('GET', '/health');
            return {
                healthy: true,
                toolCount: response.toolCount || 0
            };
        } catch (error) {
            return {
                healthy: false,
                toolCount: 0,
                error: error instanceof Error ? error.message : 'Unknown error'
            };
        }
    }

    async getAvailableTools(): Promise<MCPTool[]> {
        try {
            const response = await this.makeRequest('GET', '/tools');
            return response.tools || [];
        } catch (error) {
            console.error('Failed to get available tools:', error);
            return [];
        }
    }

    async sendChatQuery(query: string): Promise<SemanticQueryResponse> {
        try {
            const requestBody = {
                query: query
            };
            
            const response = await this.makeRequest('POST', '/chat', requestBody);
            return response;
        } catch (error) {
            return {
                success: false,
                error: error instanceof Error ? error.message : 'Unknown error',
                markdown: `❌ **Error**: Failed to process chat query - ${error instanceof Error ? error.message : 'Unknown error'}`
            };
        }
    }

    async sendSemanticQuery(request: SemanticQueryRequest): Promise<SemanticQueryResponse> {
        try {
            const response = await this.makeRequest('POST', '/api/chat/semantic', request);
            return response;
        } catch (error) {
            return {
                success: false,
                error: error instanceof Error ? error.message : 'Unknown error',
                markdown: `❌ **Semantic Processing Error**: ${error instanceof Error ? error.message : 'Unknown error'}`
            };
        }
    }

    async extractParameters(query: string, toolName: string): Promise<any> {
        try {
            const requestBody = {
                query,
                toolName,
                context: await this.gatherWorkspaceContext()
            };
            
            const response = await this.makeRequest('POST', '/api/semantic/extract-parameters', requestBody);
            return response;
        } catch (error) {
            console.error('Failed to extract parameters:', error);
            return {
                parameters: {},
                confidence: 0,
                missingRequired: [],
                suggestions: []
            };
        }
    }

    async getRecommendedTools(intent: string): Promise<any> {
        try {
            const requestBody = { intent };
            const response = await this.makeRequest('POST', '/api/semantic/recommend-tools', requestBody);
            return response.recommendations || [];
        } catch (error) {
            console.error('Failed to get recommended tools:', error);
            return [];
        }
    }

    private async gatherWorkspaceContext(): Promise<WorkspaceContext> {
        const context: WorkspaceContext = {};
        
        try {
            const workspaceRoot = vscode.workspace.workspaceFolders?.[0];
            if (!workspaceRoot) {
                return context;
            }

            // Check for Dockerfile
            try {
                await vscode.workspace.fs.stat(vscode.Uri.joinPath(workspaceRoot.uri, 'Dockerfile'));
                context.hasDockerfile = true;
            } catch {}

            // Check for Kubernetes manifests
            try {
                const files = await vscode.workspace.findFiles('**/*.{yaml,yml}', '**/node_modules/**', 10);
                for (const file of files) {
                    const content = await vscode.workspace.fs.readFile(file);
                    const text = Buffer.from(content).toString('utf8');
                    if (text.includes('apiVersion:') && text.includes('kind:')) {
                        context.hasKubernetesManifests = true;
                        break;
                    }
                }
            } catch {}

            // Check for Infrastructure as Code
            try {
                const bicepFiles = await vscode.workspace.findFiles('**/*.bicep', '**/node_modules/**', 1);
                const terraformFiles = await vscode.workspace.findFiles('**/*.tf', '**/node_modules/**', 1);
                context.hasInfrastructureCode = bicepFiles.length > 0 || terraformFiles.length > 0;
            } catch {}

            // Detect programming languages
            try {
                const languages = new Set<string>();
                const extensions = ['js', 'ts', 'py', 'java', 'cs', 'go', 'rs', 'php'];
                
                for (const ext of extensions) {
                    const files = await vscode.workspace.findFiles(`**/*.${ext}`, '**/node_modules/**', 1);
                    if (files.length > 0) {
                        languages.add(ext);
                    }
                }
                
                context.programmingLanguages = Array.from(languages);
            } catch {}

        } catch (error) {
            console.warn('Failed to gather workspace context:', error);
        }

        return context;
    }

    // Legacy method - keeping for backwards compatibility but not used in new flow
    async analyzeRequest(message: string): Promise<ToolRequest> {
        // This method is now deprecated in favor of server-side analysis
        // Keeping it for backwards compatibility but it won't be used
        return {
            shouldExecute: false,
            toolName: '',
            parameters: {},
            confidence: 0.0
        };
    }

    async executeTool(toolName: string, parameters: Record<string, any>): Promise<MCPToolResult> {
        try {
            const requestBody = {
                tool: toolName,
                arguments: parameters
            };
            
            const response = await this.makeRequest('POST', '/tools', requestBody);
            
            // Check if the response indicates success
            if (response.success === false) {
                return {
                    content: [{ type: 'text', text: response.error || response.message || 'Tool execution failed' }],
                    isError: true
                };
            }
            
            return {
                content: Array.isArray(response.result) ? response.result : [{ type: 'text', text: typeof response.result === 'string' ? response.result : JSON.stringify(response.result || response) }],
                isError: false
            };
        } catch (error) {
            return {
                content: [{ type: 'text', text: error instanceof Error ? error.message : 'Unknown error' }],
                isError: true
            };
        }
    }

    private async makeRequest(method: 'GET' | 'POST', path: string, body?: any): Promise<any> {
        const url = `${this.serverUrl}${path}`;
        
        const options: RequestInit = {
            method,
            headers: {
                'Content-Type': 'application/json',
                'User-Agent': 'Platform-MCP-Extension/1.0'
            },
            signal: AbortSignal.timeout(this.timeout)
        };

        if (body && method === 'POST') {
            options.body = JSON.stringify(body);
        }
        
        if (this.enableLogging) {
            console.log(`MCP Request: ${method} ${url}`, body);
        }

        const response = await fetch(url, options);
        
        if (!response.ok) {
            const errorText = await response.text();
            if (this.enableLogging) {
                console.error(`MCP Error: ${response.status} ${response.statusText}`, errorText);
            }
            throw new Error(`MCP Server error: ${response.status} ${response.statusText}`);
        }

        const result = await response.json();
        
        if (this.enableLogging) {
            console.log(`MCP Response:`, result);
        }
        
        return result;
    }

    // All natural language processing and parameter extraction is now handled server-side
    // This makes the extension much simpler and allows the MCP server to use advanced AI capabilities
}