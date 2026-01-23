import axios, { AxiosInstance, AxiosError } from 'axios';
import { Config } from '../config';
import * as vscode from 'vscode';

/**
 * Response structure from MCP chat endpoint.
 */
export interface ChatResponse {
    content?: string;
    message?: string;
    agentUsed?: string;
    conversationId?: string;
    templates?: Array<{
        id: string;
        name: string;
        type: string;
        content?: string;
    }>;
    metadata?: Record<string, any>;
}

/**
 * Code analysis response structure.
 */
export interface AnalysisResponse {
    findings: Array<{
        controlId: string;
        severity: string;
        title: string;
        description: string;
        recommendation?: string;
        lineNumber?: number;
    }>;
    controlsChecked: number;
    summary: string;
}

/**
 * Repository analysis response structure.
 */
export interface RepositoryAnalysisResponse {
    filesAnalyzed: number;
    findings: Array<{
        file: string;
        controlId: string;
        severity: string;
        title: string;
        description: string;
    }>;
    summary: string;
}

/**
 * HTTP client for communicating with the Platform Engineering MCP server.
 * Supports the Agent FX multi-agent architecture.
 */
export class McpClient {
    private client: AxiosInstance;
    private config: Config;
    private outputChannel: vscode.OutputChannel;

    constructor(config: Config, outputChannel: vscode.OutputChannel) {
        this.config = config;
        this.outputChannel = outputChannel;

        this.client = axios.create({
            timeout: config.timeout,
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            }
        });

        // Request interceptor for logging and auth
        this.client.interceptors.request.use((request) => {
            const apiKey = this.config.apiKey;
            if (apiKey) {
                request.headers['Authorization'] = `Bearer ${apiKey}`;
            }
            this.log(`→ ${request.method?.toUpperCase()} ${request.url}`);
            return request;
        });

        // Response interceptor for logging
        this.client.interceptors.response.use(
            (response) => {
                this.log(`← ${response.status} ${response.statusText}`);
                return response;
            },
            (error: AxiosError) => {
                this.log(`← Error: ${error.message}`);
                throw error;
            }
        );
    }

    private get baseUrl(): string {
        return this.config.apiUrl;
    }

    private log(message: string): void {
        this.config.log(this.outputChannel, `[McpClient] ${message}`);
    }

    /**
     * Check MCP server health.
     */
    async healthCheck(): Promise<boolean> {
        try {
            const response = await this.client.get(`${this.baseUrl}/health`);
            return response.status === 200;
        } catch (error) {
            return false;
        }
    }

    /**
     * Send a chat message to the MCP server for processing by Agent FX.
     * 
     * @param message - User's message
     * @param history - Conversation history for context
     * @param targetAgent - Optional specific agent to route to
     */
    async sendChatMessage(
        message: string,
        history: Array<{ role: string; content: string }> = [],
        targetAgent?: string
    ): Promise<ChatResponse> {
        try {
            const payload: Record<string, any> = {
                message,
                history: history  // Changed from conversationHistory to match ChatRequest model
            };

            // Add target agent hint if specified (e.g., from /command)
            if (targetAgent) {
                payload.targetAgent = targetAgent;
                payload.metadata = {
                    routingHint: targetAgent
                };
            }

            const response = await this.client.post<ChatResponse>(
                `${this.baseUrl}/mcp/chat`,
                payload
            );

            return response.data;
        } catch (error: any) {
            this.handleError(error, 'sendChatMessage');
            throw error;
        }
    }

    /**
     * Analyze code for NIST 800-53 compliance.
     * Routes to the Compliance Agent.
     */
    async analyzeCode(
        content: string,
        fileName: string,
        language: string
    ): Promise<AnalysisResponse> {
        try {
            const response = await this.client.post<AnalysisResponse>(
                `${this.baseUrl}/mcp/analyze-code`,
                {
                    code: content,
                    fileName,
                    language,
                    framework: 'NIST-800-53'
                }
            );

            return response.data;
        } catch (error: any) {
            this.handleError(error, 'analyzeCode');
            throw error;
        }
    }

    /**
     * Analyze an entire repository for compliance.
     * Routes to the Compliance Agent for comprehensive scanning.
     */
    async analyzeRepository(repositoryPath: string): Promise<RepositoryAnalysisResponse> {
        try {
            const response = await this.client.post<RepositoryAnalysisResponse>(
                `${this.baseUrl}/mcp/analyze-repository`,
                {
                    path: repositoryPath,
                    framework: 'NIST-800-53',
                    includePatterns: ['*.bicep', '*.tf', '*.yaml', '*.yml', '*.json'],
                    excludePatterns: ['node_modules/**', '.git/**', 'out/**']
                }
            );

            return response.data;
        } catch (error: any) {
            this.handleError(error, 'analyzeRepository');
            throw error;
        }
    }

    /**
     * Get templates generated in a conversation.
     */
    async getTemplates(conversationId: string): Promise<Array<{ id: string; name: string; type: string; content: string }>> {
        try {
            const response = await this.client.get(
                `${this.baseUrl}/mcp/templates/${conversationId}`
            );

            return response.data.templates || [];
        } catch (error: any) {
            this.handleError(error, 'getTemplates');
            throw error;
        }
    }

    /**
     * Get a specific template by ID.
     */
    async getTemplate(templateId: string): Promise<{ name: string; type: string; content: string }> {
        try {
            const response = await this.client.get(
                `${this.baseUrl}/mcp/template/${templateId}`
            );

            return response.data;
        } catch (error: any) {
            this.handleError(error, 'getTemplate');
            throw error;
        }
    }

    /**
     * Handle errors with user-friendly messages.
     */
    private handleError(error: any, operation: string): void {
        if (axios.isAxiosError(error)) {
            const axiosError = error as AxiosError;
            
            if (axiosError.code === 'ECONNREFUSED') {
                this.log(`${operation}: Connection refused - MCP server not running`);
                throw new Error('Cannot connect to MCP server. Is it running?');
            }
            
            if (axiosError.code === 'ETIMEDOUT' || axiosError.code === 'TIMEOUT') {
                this.log(`${operation}: Request timeout`);
                throw new Error('Request timed out. Try increasing timeout in settings.');
            }

            if (axiosError.response) {
                const status = axiosError.response.status;
                const data = axiosError.response.data as any;
                const message = data?.message || data?.error || axiosError.message;
                
                this.log(`${operation}: HTTP ${status} - ${message}`);
                throw new Error(`Server error (${status}): ${message}`);
            }
        }
        
        this.log(`${operation}: ${error.message}`);
        throw error;
    }
}
