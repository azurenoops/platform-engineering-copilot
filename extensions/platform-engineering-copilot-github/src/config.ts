import * as vscode from 'vscode';

/**
 * Configuration singleton for Platform Engineering Copilot extension.
 * Reads settings from VS Code workspace configuration.
 */
export class Config {
    private static instance: Config;

    private constructor() {}

    static getInstance(): Config {
        if (!Config.instance) {
            Config.instance = new Config();
        }
        return Config.instance;
    }

    private get config(): vscode.WorkspaceConfiguration {
        return vscode.workspace.getConfiguration('platform-copilot');
    }

    /**
     * MCP Server HTTP endpoint URL.
     * Default: http://localhost:5100
     */
    get apiUrl(): string {
        return this.config.get<string>('apiUrl') || 'http://localhost:5100';
    }

    /**
     * Optional API key for authentication.
     */
    get apiKey(): string {
        return this.config.get<string>('apiKey') || '';
    }

    /**
     * Request timeout in milliseconds.
     * Default: 60000 (60 seconds)
     */
    get timeout(): number {
        return this.config.get<number>('timeout') || 60000;
    }

    /**
     * Whether debug logging is enabled.
     * Default: true
     */
    get enableLogging(): boolean {
        return this.config.get<boolean>('enableLogging') ?? true;
    }

    /**
     * Log a message if logging is enabled.
     */
    log(outputChannel: vscode.OutputChannel, message: string): void {
        if (this.enableLogging) {
            const timestamp = new Date().toISOString();
            outputChannel.appendLine(`[${timestamp}] ${message}`);
        }
    }
}
