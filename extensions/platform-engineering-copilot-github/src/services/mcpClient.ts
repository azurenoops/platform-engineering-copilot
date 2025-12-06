import axios, { AxiosInstance, AxiosError } from 'axios';
import { config } from '../config';

/**
 * MCP Server Request and Response interfaces
 */
export interface McpChatRequest {
    message: string;
    conversationId?: string;
    context?: Record<string, any>;
}

export interface McpChatResponse {
    success: boolean;
    response: string;
    conversationId: string;
    intentType?: string;
    confidence?: number;
    toolExecuted?: string;
    executionTimeMs?: number;
    requiresFollowUp?: boolean;
    followUpPrompt?: string;
    metadata?: {
        resourceId?: string;
        complianceScore?: number;
        [key: string]: any;
    };
    error?: string;
}

export interface McpHealthResponse {
    status: string;
    mode: string;
    server: string;
    version: string;
}

export interface CodeComplianceResponse {
    success: boolean;
    analysisId: string;
    complianceReport: string;
    framework: string;
    fileName?: string;
    repositoryUrl?: string;
    requiresFollowUp: boolean;
    followUpPrompt?: string;
    findings: string[];
    recommendations: string[];
    riskLevel: string;
    errors: string[];
    analyzedAt: string;
}

export interface RepositoryComplianceResponse {
    success: boolean;
    analysisId: string;
    repositoryUrl: string;
    branch: string;
    complianceReport: string;
    framework: string;
    requiresFollowUp: boolean;
    followUpPrompt?: string;
    overallRiskLevel: string;
    errors: string[];
    analyzedAt: string;
}

export interface TemplateFile {
    fileName: string;
    content: string;
    fileType?: string;
}

export interface Template {
    id: string;
    name: string;
    description?: string;
    templateType?: string;
    createdAt: string;
    files: TemplateFile[];
}

export interface TemplateResponse {
    success: boolean;
    template?: Template;
    templates?: Template[];
    error?: string;
}

/**
 * MCP Client for VS Code Extension
 * Handles HTTP communication with the Platform Engineering Copilot MCP server
 */
export class McpClient {
    private client: AxiosInstance;
    private readonly baseUrl: string;

    constructor() {
        this.baseUrl = config.apiUrl;
        
        this.client = axios.create({
            baseURL: config.apiUrl,
            timeout: config.timeout,
            headers: {
                'Content-Type': 'application/json',
                'User-Agent': 'Platform-Copilot-VSCode-Extension/1.0.0'
            }
        });

        // Add API key if configured
        if (config.apiKey) {
            this.client.defaults.headers.common['X-API-Key'] = config.apiKey;
        }

        // Request interceptor
        this.client.interceptors.request.use(
            (requestConfig) => {
                config.log(`→ ${requestConfig.method?.toUpperCase()} ${requestConfig.url}`);
                if (requestConfig.data) {
                    config.log('  Request data:', requestConfig.data);
                }
                return requestConfig;
            },
            (error: any) => {
                config.error('Request error:', error);
                return Promise.reject(error);
            }
        );

        // Response interceptor
        this.client.interceptors.response.use(
            (response) => {
                config.log(`← ${response.status} ${response.config?.url}`);
                return response;
            },
            (error: AxiosError) => {
                if (error.response) {
                    config.error(`← ${error.response.status} ${error.config?.url}`, error.response.data);
                } else if (error.request) {
                    config.error('No response received:', error.message);
                } else {
                    config.error('Request setup error:', error.message);
                }
                return Promise.reject(error);
            }
        );
    }

    /**
     * Send a chat message to the MCP server
     */
    async sendChatMessage(
        message: string,
        conversationId?: string,
        context?: Record<string, any>
    ): Promise<McpChatResponse> {
        try {
            const request: McpChatRequest = {
                message,
                conversationId: conversationId || this.generateConversationId(),
                context: {
                    source: 'vscode-extension',
                    platform: 'VS Code',
                    ...context
                }
            };

            const response = await this.client.post<McpChatResponse>('/mcp/chat', request);
            
            if (!response.data.success) {
                throw new Error(response.data.error || 'MCP server returned unsuccessful response');
            }

            return response.data;
        } catch (error) {
            throw this.handleError(error, 'Failed to send chat message to MCP server');
        }
    }



    /**
     * Send compliance scan request
     */
    async requestComplianceScan(
        subscriptionId: string,
        framework: string = 'nist-800-53',
        scope?: string
    ): Promise<McpChatResponse> {
        const message = `Run a ${framework} compliance scan on ${scope ? `${scope} in ` : ''}subscription ${subscriptionId}`;
        
        return this.sendChatMessage(message, undefined, {
            operation: 'compliance-scan',
            subscriptionId,
            framework,
            scope
        });
    }

    /**
     * Request remediation plan generation
     */
    async requestRemediationPlan(
        subscriptionId: string,
        findings?: string[]
    ): Promise<McpChatResponse> {
        let message = `Generate a comprehensive automated remediation plan for subscription ${subscriptionId}`;
        
        if (findings && findings.length > 0) {
            message += ` focusing on these findings: ${findings.join(', ')}`;
        }

        return this.sendChatMessage(message, undefined, {
            operation: 'remediation-plan',
            subscriptionId,
            findings
        });
    }

    /**
     * Request document analysis
     */
    async requestDocumentAnalysis(
        documentType: string,
        fileName?: string
    ): Promise<McpChatResponse> {
        let message = `Prepare to analyze a ${documentType} document for security and compliance requirements`;
        
        if (fileName) {
            message += `. The document is named "${fileName}"`;
        }

        return this.sendChatMessage(message, undefined, {
            operation: 'document-analysis',
            documentType,
            fileName
        });
    }

    /**
     * Execute automated remediation
     */
    async executeRemediation(
        subscriptionId: string,
        planId?: string,
        dryRun: boolean = false
    ): Promise<McpChatResponse> {
        let message = `Execute automated compliance remediation for subscription ${subscriptionId}`;
        
        if (planId) {
            message += ` using plan ${planId}`;
        }

        if (dryRun) {
            message += ' in dry-run mode (no actual changes)';
        }

        return this.sendChatMessage(message, undefined, {
            operation: 'execute-remediation',
            subscriptionId,
            planId,
            dryRun
        });
    }

    /**
     * Get remediation status and progress
     */
    async getRemediationStatus(
        subscriptionId: string,
        executionId?: string
    ): Promise<McpChatResponse> {
        let message = `Show remediation progress and status for subscription ${subscriptionId}`;
        
        if (executionId) {
            message += ` for execution ${executionId}`;
        }

        return this.sendChatMessage(message, undefined, {
            operation: 'remediation-status',
            subscriptionId,
            executionId
        });
    }

    /**
     * Check MCP server health
     */
    async healthCheck(): Promise<McpHealthResponse> {
        try {
            const response = await this.client.get<McpHealthResponse>('/health');
            return response.data;
        } catch (error) {
            throw this.handleError(error, 'MCP server health check failed');
        }
    }

    /**
     * Generate a unique conversation ID
     */
    private generateConversationId(): string {
        return `vscode-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    }

    /**
     * Update configuration (called when VS Code settings change)
     */
    updateConfig(): void {
        this.client.defaults.baseURL = config.apiUrl;
        this.client.defaults.timeout = config.timeout;

        if (config.apiKey) {
            this.client.defaults.headers.common['X-API-Key'] = config.apiKey;
        } else {
            delete this.client.defaults.headers.common['X-API-Key'];
        }

        config.info('MCP client configuration updated');
    }

    /**
     * Update the base URL for the MCP server
     */
    updateBaseUrl(newBaseUrl: string): void {
        this.client.defaults.baseURL = newBaseUrl;
    }

    /**
     * Update request timeout
     */
    updateTimeout(timeoutMs: number): void {
        this.client.defaults.timeout = timeoutMs;
    }

    /**
     * Get current configuration
     */
    getConfig(): { baseUrl: string; timeout?: number } {
        return {
            baseUrl: this.client.defaults.baseURL || this.baseUrl,
            timeout: this.client.defaults.timeout
        };
    }

    /**
     * Handle and format errors
     */
    private handleError(error: any, contextMessage: string): Error {
        if (error instanceof AxiosError) {
            if (error.response) {
                const statusCode = error.response.status;
                const responseData = error.response.data;
                
                return new Error(
                    `${contextMessage}: HTTP ${statusCode} - ${
                        responseData?.error || responseData?.message || 'Unknown server error'
                    }`
                );
            } else if (error.request) {
                return new Error(
                    `${contextMessage}: Could not reach MCP server at ${this.baseUrl}. Please verify the server is running.`
                );
            } else {
                return new Error(`${contextMessage}: Request configuration error - ${error.message}`);
            }
        }

        return new Error(
            `${contextMessage}: ${error instanceof Error ? error.message : 'Unknown error'}`
        );
    }

    /**
     * Analyze code content for ATO compliance
     */
    async analyzeCodeForCompliance(
        codeContent: string,
        fileName?: string,
        repositoryUrl?: string,
        framework: string = 'NIST-800-53'
    ): Promise<CodeComplianceResponse> {
        try {
            config.log(`→ POST /mcp/analyze-code (${codeContent.length} chars)`);
            
            const response = await this.client.post('/mcp/analyze-code', {
                codeContent,
                fileName,
                repositoryUrl,
                framework,
                context: {
                    source: 'vscode-extension',
                    vscodeVersion: '1.0.0'
                }
            });

            const result = response.data as CodeComplianceResponse;
            config.log(`← Code compliance analysis complete: ${result.riskLevel} risk`);
            return result;

        } catch (error) {
            const errorMessage = this.handleError(error, 'Code compliance analysis failed');
            throw errorMessage;
        }
    }

    /**
     * Analyze current workspace/repository for compliance
     */
    async analyzeRepositoryForCompliance(
        repositoryUrl: string,
        branch: string = 'main',
        filePatterns?: string[],
        framework: string = 'NIST-800-53'
    ): Promise<RepositoryComplianceResponse> {
        try {
            config.log(`→ POST /mcp/analyze-repository ${repositoryUrl}`);
            
            const response = await this.client.post('/mcp/analyze-repository', {
                repositoryUrl,
                branch,
                filePatterns,
                framework,
                context: {
                    source: 'vscode-extension',
                    vscodeVersion: '1.0.0'
                }
            });

            const result = response.data as RepositoryComplianceResponse;
            config.log(`← Repository compliance analysis complete: ${result.overallRiskLevel} risk`);
            return result;

        } catch (error) {
            const errorMessage = this.handleError(error, 'Repository compliance analysis failed');
            throw errorMessage;
        }
    }

    /**
     * Get templates by conversation ID from the database
     */
    async getTemplatesByConversationId(conversationId: string): Promise<TemplateResponse> {
        try {
            config.log(`→ GET /mcp/templates/${conversationId}`);
            
            const response = await this.client.get<TemplateResponse>(`/mcp/templates/${conversationId}`);
            
            config.log(`← Found ${response.data.templates?.length || 0} template(s) for conversation`);
            return response.data;

        } catch (error) {
            if (error instanceof AxiosError && error.response?.status === 404) {
                return { success: false, templates: [], error: 'No templates found for this conversation' };
            }
            throw this.handleError(error, 'Failed to fetch templates');
        }
    }

    /**
     * Get the most recently generated template from the database
     */
    async getLatestTemplate(): Promise<TemplateResponse> {
        try {
            config.log(`→ GET /mcp/templates/latest`);
            
            const response = await this.client.get<TemplateResponse>('/mcp/templates/latest');
            
            if (response.data.template) {
                config.log(`← Found latest template: ${response.data.template.name}`);
            }
            return response.data;

        } catch (error) {
            if (error instanceof AxiosError && error.response?.status === 404) {
                return { success: false, error: 'No templates found' };
            }
            throw this.handleError(error, 'Failed to fetch latest template');
        }
    }
}
