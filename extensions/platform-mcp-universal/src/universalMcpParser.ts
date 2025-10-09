/**
 * Universal MCP Result Parser
 * Handles all response formats from MCP servers with consistent parsing logic
 */

export interface UniversalMcpResult {
    success: boolean;
    message: string;
    resultType: string;
    data?: any;
    metadata: Record<string, any>;
    errors: string[];
    suggestions: string[];
    files: McpResultFile[];
    nextSteps: string[];
}

export interface McpResultFile {
    name: string;
    description: string;
    content?: string;
    path?: string;
    type: string;
}

export interface ServiceTemplateResult {
    serviceName: string;
    serviceType: string;
    framework: string;
    templateId: string;
    action: string;
    kubernetesManifest?: string;
    helmValues?: string;
    azureDevOpsPipeline?: string;
    dockerFile?: string;
    monitoringConfig?: string;
    securityConfig?: string;
    azure?: any;
}

export interface InfrastructureResult {
    resourceType: string;
    resourceName: string;
    resourceGroupName: string;
    location: string;
    resourceId?: string;
    status: string;
    properties: Record<string, any>;
}

export interface CostAnalysisResult {
    totalCost: number;
    currency: string;
    period: string;
    breakdown: Array<{
        category: string;
        cost: number;
        percentage: number;
    }>;
    trends?: any;
}

/**
 * Universal parser for MCP server responses
 * Handles multiple response formats and normalizes them to UniversalMcpResult
 */
export class UniversalMcpParser {
    
    /**
     * Parse any MCP response and return a standardized result
     */
    static parseResponse(response: any): UniversalMcpResult | null {
        try {
            console.log('🔍 UniversalMcpParser: Parsing response:', JSON.stringify(response, null, 2));

            // Handle different response formats
            let parsedData: any = null;

            // Format 1: Direct UniversalMcpResult object
            if (response && typeof response === 'object' && 'success' in response) {
                // Check if result field contains JSON string that needs parsing
                if (response.result && typeof response.result === 'string') {
                    try {
                        const parsedResult = JSON.parse(response.result);
                        parsedData = parsedResult;
                        console.log('🔧 UniversalMcpParser: Parsed double-encoded JSON result');
                    } catch (error) {
                        console.warn('⚠️ UniversalMcpParser: Failed to parse result field as JSON:', error);
                        parsedData = response;
                    }
                } else {
                    parsedData = response;
                }
            }
            // Format 2: Response with markdown field containing JSON
            else if (response && response.markdown) {
                parsedData = this.parseMarkdownResponse(response.markdown);
            }
            // Format 3: Response with content array
            else if (response && response.content && Array.isArray(response.content)) {
                parsedData = this.parseContentArrayResponse(response.content);
            }
            // Format 4: Response with text field
            else if (response && response.text) {
                parsedData = this.parseTextResponse(response.text);
            }
            // Format 5: Legacy format with direct properties
            else if (response && (response.serviceName || response.resourceName)) {
                parsedData = this.parseLegacyResponse(response);
            }

            if (!parsedData) {
                console.warn('🚨 UniversalMcpParser: Could not parse response format');
                return null;
            }

            // Normalize to UniversalMcpResult format
            const result = this.normalizeResult(parsedData);
            console.log('✅ UniversalMcpParser: Successfully parsed result:', JSON.stringify(result, null, 2));
            return result;

        } catch (error) {
            console.error('❌ UniversalMcpParser: Error parsing response:', error);
            return null;
        }
    }

    /**
     * Parse JSON from markdown field (handles quoted JSON strings)
     */
    private static parseMarkdownResponse(markdown: string): any {
        try {
            // Handle quoted JSON string format: "{"success":true,...}"
            if (markdown.startsWith('"') && markdown.endsWith('"')) {
                const unquoted = markdown.slice(1, -1);
                // Unescape any escaped quotes
                const unescaped = unquoted.replace(/\\"/g, '"');
                return JSON.parse(unescaped);
            }

            // Handle JSON code blocks: ```json\n...\n```
            const jsonBlockMatch = markdown.match(/```json\s*\n([\s\S]*?)\n```/);
            if (jsonBlockMatch) {
                return JSON.parse(jsonBlockMatch[1]);
            }

            // Handle plain JSON blocks: ```\n...\n```
            const plainBlockMatch = markdown.match(/```\s*\n([\s\S]*?)\n```/);
            if (plainBlockMatch) {
                try {
                    return JSON.parse(plainBlockMatch[1]);
                } catch {
                    // Not JSON, fall through
                }
            }

            // Try parsing the entire markdown as JSON
            return JSON.parse(markdown);

        } catch (error) {
            console.warn('🚨 UniversalMcpParser: Could not parse markdown as JSON:', error);
            return null;
        }
    }

    /**
     * Parse content array responses
     */
    private static parseContentArrayResponse(content: any[]): any {
        for (const item of content) {
            if (item.text) {
                // First try to parse as quoted JSON string (the user's exact case)
                if (typeof item.text === 'string' && item.text.startsWith('"') && item.text.endsWith('"')) {
                    try {
                        const unquoted = item.text.slice(1, -1);
                        const unescaped = unquoted.replace(/\\"/g, '"').replace(/\\\\/g, '\\');
                        return JSON.parse(unescaped);
                    } catch (error) {
                        console.warn('🚨 Failed to parse quoted JSON from content array:', error);
                    }
                }
                
                // Fall back to regular text parsing
                const parsed = this.parseTextResponse(item.text);
                if (parsed) return parsed;
            }
        }
        return null;
    }

    /**
     * Parse text responses (try JSON parsing)
     */
    private static parseTextResponse(text: string): any {
        try {
            return JSON.parse(text);
        } catch {
            // Not JSON, check if it contains structured data
            if (text.includes('success') && text.includes('{')) {
                // Try to extract JSON from text
                const jsonMatch = text.match(/\{[\s\S]*\}/);
                if (jsonMatch) {
                    try {
                        return JSON.parse(jsonMatch[0]);
                    } catch {
                        // Still not valid JSON
                    }
                }
            }
            return null;
        }
    }

    /**
     * Parse legacy response formats
     */
    private static parseLegacyResponse(response: any): UniversalMcpResult {
        // Convert legacy service template format
        if (response.serviceName || response.files) {
            return {
                success: true,
                message: `Service template operation completed`,
                resultType: 'service_template',
                data: {
                    serviceName: response.serviceName,
                    serviceType: response.serviceType,
                    framework: response.framework,
                    templateId: response.templateId,
                    action: response.action,
                    kubernetesManifest: response.kubernetesManifest,
                    helmValues: response.helmValues,
                    azureDevOpsPipeline: response.azureDevOpsPipeline,
                    dockerFile: response.dockerFile,
                    monitoringConfig: response.monitoringConfig,
                    securityConfig: response.securityConfig,
                    azure: response.azure
                },
                metadata: {},
                errors: [],
                suggestions: [],
                files: response.files || [],
                nextSteps: []
            };
        }

        // Convert legacy infrastructure format
        if (response.resourceName) {
            return {
                success: response.success !== false,
                message: response.message || `Infrastructure operation completed`,
                resultType: 'infrastructure',
                data: {
                    resourceType: response.resourceType,
                    resourceName: response.resourceName,
                    resourceGroupName: response.resourceGroupName,
                    location: response.location,
                    resourceId: response.resourceId,
                    status: response.status,
                    properties: response.properties || {}
                },
                metadata: {},
                errors: [],
                suggestions: [],
                files: [],
                nextSteps: []
            };
        }

        return {
            success: false,
            message: 'Unknown legacy format',
            resultType: 'unknown',
            data: response,
            metadata: {},
            errors: ['Could not parse legacy response format'],
            suggestions: [],
            files: [],
            nextSteps: []
        };
    }

    /**
     * Normalize any parsed data to UniversalMcpResult format
     */
    private static normalizeResult(data: any): UniversalMcpResult {
        // If already in correct format, return as-is
        if (data && typeof data === 'object' && 
            'success' in data && 'message' in data && 'resultType' in data) {
            return {
                success: data.success || false,
                message: data.message || '',
                resultType: data.resultType || 'unknown',
                data: data.data,
                metadata: data.metadata || {},
                errors: data.errors || [],
                suggestions: data.suggestions || [],
                files: data.files || [],
                nextSteps: data.nextSteps || []
            };
        }

        // Convert other formats
        return this.parseLegacyResponse(data);
    }

    /**
     * Check if a result indicates a service template was created
     */
    static isServiceTemplateResult(result: UniversalMcpResult): boolean {
        return result.success && 
               result.resultType === 'service_template' && 
               result.files && result.files.length > 0;
    }

    /**
     * Check if a result indicates infrastructure was created
     */
    static isInfrastructureResult(result: UniversalMcpResult): boolean {
        return result.success && 
               result.resultType === 'infrastructure' && 
               result.data && (result.data as InfrastructureResult).resourceName;
    }

    /**
     * Check if a result contains cost analysis data
     */
    static isCostAnalysisResult(result: UniversalMcpResult): boolean {
        return result.success && 
               result.resultType === 'cost_analysis' && 
               result.data && (result.data as CostAnalysisResult).totalCost !== undefined;
    }

    /**
     * Extract service template data from result
     */
    static getServiceTemplateData(result: UniversalMcpResult): ServiceTemplateResult | null {
        if (this.isServiceTemplateResult(result)) {
            return result.data as ServiceTemplateResult;
        }
        return null;
    }

    /**
     * Extract infrastructure data from result
     */
    static getInfrastructureData(result: UniversalMcpResult): InfrastructureResult | null {
        if (this.isInfrastructureResult(result)) {
            return result.data as InfrastructureResult;
        }
        return null;
    }

    /**
     * Extract cost analysis data from result
     */
    static getCostAnalysisData(result: UniversalMcpResult): CostAnalysisResult | null {
        if (this.isCostAnalysisResult(result)) {
            return result.data as CostAnalysisResult;
        }
        return null;
    }
}