import * as vscode from 'vscode';

/**
 * Service for exporting and sharing reports from compliance analysis
 * and infrastructure templates.
 */
export class ExportService {
    private outputChannel: vscode.OutputChannel;

    constructor(outputChannel: vscode.OutputChannel) {
        this.outputChannel = outputChannel;
    }

    /**
     * Export findings report to markdown.
     */
    async exportToMarkdown(data: {
        title: string;
        summary: string;
        findings: Array<{
            controlId: string;
            severity: string;
            title: string;
            description: string;
            recommendation?: string;
        }>;
        metadata?: Record<string, any>;
    }): Promise<string> {
        const lines: string[] = [];
        
        lines.push(`# ${data.title}`);
        lines.push('');
        lines.push(`**Generated:** ${new Date().toISOString()}`);
        lines.push('');
        lines.push('## Summary');
        lines.push('');
        lines.push(data.summary);
        lines.push('');
        
        if (data.findings.length > 0) {
            lines.push('## Findings');
            lines.push('');
            lines.push('| Severity | Control | Title |');
            lines.push('|----------|---------|-------|');
            
            for (const finding of data.findings) {
                lines.push(`| ${finding.severity} | ${finding.controlId} | ${finding.title} |`);
            }
            
            lines.push('');
            lines.push('### Details');
            lines.push('');
            
            for (const finding of data.findings) {
                lines.push(`#### ${finding.controlId}: ${finding.title}`);
                lines.push('');
                lines.push(`**Severity:** ${finding.severity}`);
                lines.push('');
                lines.push(finding.description);
                lines.push('');
                if (finding.recommendation) {
                    lines.push(`**Recommendation:** ${finding.recommendation}`);
                    lines.push('');
                }
            }
        } else {
            lines.push('## Findings');
            lines.push('');
            lines.push('✅ No compliance issues found.');
        }
        
        return lines.join('\n');
    }

    /**
     * Export findings report to JSON.
     */
    exportToJson(data: any): string {
        return JSON.stringify(data, null, 2);
    }

    /**
     * Export findings report to HTML.
     */
    exportToHtml(data: {
        title: string;
        summary: string;
        findings: Array<{
            controlId: string;
            severity: string;
            title: string;
            description: string;
            recommendation?: string;
        }>;
    }): string {
        const severityColors: Record<string, string> = {
            'high': '#f44336',
            'medium': '#ff9800',
            'low': '#4caf50',
            'info': '#2196f3'
        };

        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>${data.title}</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 900px; margin: 0 auto; padding: 20px; }
        h1 { color: #333; border-bottom: 2px solid #0078d4; padding-bottom: 10px; }
        .summary { background: #f5f5f5; padding: 15px; border-radius: 5px; margin: 20px 0; }
        .finding { border: 1px solid #ddd; padding: 15px; margin: 15px 0; border-radius: 5px; }
        .severity { display: inline-block; padding: 2px 8px; border-radius: 3px; color: white; font-weight: bold; text-transform: uppercase; font-size: 12px; }
        .severity.high { background: ${severityColors['high']}; }
        .severity.medium { background: ${severityColors['medium']}; }
        .severity.low { background: ${severityColors['low']}; }
        .severity.info { background: ${severityColors['info']}; }
        .control-id { font-family: monospace; background: #e3f2fd; padding: 2px 6px; border-radius: 3px; }
        .recommendation { background: #e8f5e9; padding: 10px; border-radius: 5px; margin-top: 10px; }
        .meta { color: #666; font-size: 14px; }
    </style>
</head>
<body>
    <h1>🛡️ ${data.title}</h1>
    <p class="meta">Generated: ${new Date().toISOString()}</p>
    
    <div class="summary">
        <h2>Summary</h2>
        <p>${data.summary}</p>
        <p><strong>Total Findings:</strong> ${data.findings.length}</p>
    </div>

    <h2>Findings</h2>
    ${data.findings.length === 0 ? '<p>✅ No compliance issues found!</p>' : data.findings.map(f => `
    <div class="finding">
        <span class="severity ${f.severity.toLowerCase()}">${f.severity}</span>
        <span class="control-id">${f.controlId}</span>
        <h3>${f.title}</h3>
        <p>${f.description}</p>
        ${f.recommendation ? `<div class="recommendation"><strong>Recommendation:</strong> ${f.recommendation}</div>` : ''}
    </div>
    `).join('')}
</body>
</html>`;
    }

    /**
     * Copy content to clipboard.
     */
    async copyToClipboard(content: string): Promise<void> {
        await vscode.env.clipboard.writeText(content);
        vscode.window.showInformationMessage('Copied to clipboard');
    }

    /**
     * Open content in new editor.
     */
    async openInEditor(content: string, language: string = 'markdown'): Promise<void> {
        const doc = await vscode.workspace.openTextDocument({
            content,
            language
        });
        await vscode.window.showTextDocument(doc);
    }

    /**
     * Share via email (opens default mail client).
     */
    async shareViaEmail(subject: string, body: string): Promise<void> {
        const mailto = `mailto:?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;
        await vscode.env.openExternal(vscode.Uri.parse(mailto));
    }

    /**
     * Export findings to file.
     */
    async exportToFile(
        data: any,
        format: 'markdown' | 'json' | 'html',
        defaultFileName: string
    ): Promise<vscode.Uri | undefined> {
        const extensions: Record<string, string> = {
            'markdown': 'md',
            'json': 'json',
            'html': 'html'
        };

        const uri = await vscode.window.showSaveDialog({
            defaultUri: vscode.Uri.file(defaultFileName + '.' + extensions[format]),
            filters: {
                [format.toUpperCase()]: [extensions[format]]
            }
        });

        if (!uri) {
            return undefined;
        }

        let content: string;
        switch (format) {
            case 'markdown':
                content = await this.exportToMarkdown(data);
                break;
            case 'html':
                content = this.exportToHtml(data);
                break;
            case 'json':
            default:
                content = this.exportToJson(data);
                break;
        }

        const edit = new vscode.WorkspaceEdit();
        edit.createFile(uri, { overwrite: true, contents: Buffer.from(content, 'utf8') });
        await vscode.workspace.applyEdit(edit);

        this.outputChannel.appendLine(`Exported report to: ${uri.fsPath}`);
        vscode.window.showInformationMessage(`Exported to: ${uri.fsPath}`);

        return uri;
    }
}
