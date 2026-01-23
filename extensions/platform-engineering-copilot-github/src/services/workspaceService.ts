import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

/**
 * Service for creating files and workspaces from generated templates.
 */
export class WorkspaceService {
    private outputChannel: vscode.OutputChannel;

    constructor(outputChannel: vscode.OutputChannel) {
        this.outputChannel = outputChannel;
    }

    /**
     * Create a single file in the workspace.
     */
    async createFile(fileName: string, content: string, subFolder?: string): Promise<vscode.Uri | undefined> {
        const workspaceFolders = vscode.workspace.workspaceFolders;
        
        if (!workspaceFolders) {
            vscode.window.showErrorMessage('No workspace folder open');
            return undefined;
        }

        const targetFolder = subFolder 
            ? path.join(workspaceFolders[0].uri.fsPath, subFolder)
            : workspaceFolders[0].uri.fsPath;

        // Ensure folder exists
        if (!fs.existsSync(targetFolder)) {
            fs.mkdirSync(targetFolder, { recursive: true });
        }

        const filePath = path.join(targetFolder, fileName);
        const fileUri = vscode.Uri.file(filePath);

        // Check if file exists
        if (fs.existsSync(filePath)) {
            const overwrite = await vscode.window.showWarningMessage(
                `File ${fileName} already exists. Overwrite?`,
                'Yes', 'No', 'Save As New'
            );
            
            if (overwrite === 'No') {
                return undefined;
            }
            
            if (overwrite === 'Save As New') {
                const newName = await vscode.window.showInputBox({
                    prompt: 'Enter new file name',
                    value: `${path.basename(fileName, path.extname(fileName))}_new${path.extname(fileName)}`
                });
                if (!newName) { return undefined; }
                return this.createFile(newName, content, subFolder);
            }
        }

        // Write file
        const edit = new vscode.WorkspaceEdit();
        edit.createFile(fileUri, { overwrite: true, contents: Buffer.from(content, 'utf8') });
        
        const success = await vscode.workspace.applyEdit(edit);
        
        if (success) {
            this.outputChannel.appendLine(`Created file: ${filePath}`);
            vscode.window.showInformationMessage(`Created: ${fileName}`);
            
            // Open the file
            const doc = await vscode.workspace.openTextDocument(fileUri);
            await vscode.window.showTextDocument(doc);
            
            return fileUri;
        } else {
            vscode.window.showErrorMessage(`Failed to create ${fileName}`);
            return undefined;
        }
    }

    /**
     * Create multiple files as a workspace/project structure.
     */
    async createWorkspace(
        projectName: string,
        files: Array<{ name: string; content: string; path?: string }>
    ): Promise<boolean> {
        const workspaceFolders = vscode.workspace.workspaceFolders;
        
        if (!workspaceFolders) {
            vscode.window.showErrorMessage('No workspace folder open');
            return false;
        }

        const projectPath = path.join(workspaceFolders[0].uri.fsPath, projectName);

        // Create project folder
        if (!fs.existsSync(projectPath)) {
            fs.mkdirSync(projectPath, { recursive: true });
        }

        const edit = new vscode.WorkspaceEdit();
        const createdFiles: vscode.Uri[] = [];

        for (const file of files) {
            const relativePath = file.path ? path.join(file.path, file.name) : file.name;
            const fullPath = path.join(projectPath, relativePath);
            const fileDir = path.dirname(fullPath);

            // Ensure directory exists
            if (!fs.existsSync(fileDir)) {
                fs.mkdirSync(fileDir, { recursive: true });
            }

            const fileUri = vscode.Uri.file(fullPath);
            edit.createFile(fileUri, { overwrite: true, contents: Buffer.from(file.content, 'utf8') });
            createdFiles.push(fileUri);
        }

        const success = await vscode.workspace.applyEdit(edit);
        
        if (success) {
            this.outputChannel.appendLine(`Created project: ${projectName} with ${files.length} files`);
            vscode.window.showInformationMessage(`Created project: ${projectName}`);
            
            // Open first file
            if (createdFiles.length > 0) {
                const doc = await vscode.workspace.openTextDocument(createdFiles[0]);
                await vscode.window.showTextDocument(doc);
            }
            
            return true;
        }
        
        return false;
    }

    /**
     * Create infrastructure templates in a standard structure.
     */
    async createInfrastructureProject(
        templates: Array<{ name: string; type: string; content: string }>
    ): Promise<boolean> {
        const projectName = await vscode.window.showInputBox({
            prompt: 'Enter project name for infrastructure templates',
            value: 'infrastructure'
        });

        if (!projectName) {
            return false;
        }

        const files = templates.map(template => {
            const folder = this.getFolderForTemplateType(template.type);
            return {
                name: template.name,
                content: template.content,
                path: folder
            };
        });

        return this.createWorkspace(projectName, files);
    }

    /**
     * Get appropriate folder for template type.
     */
    private getFolderForTemplateType(type: string): string {
        const folders: Record<string, string> = {
            'bicep': 'bicep',
            'terraform': 'terraform',
            'kubernetes': 'kubernetes',
            'arm': 'arm-templates',
            'dockerfile': 'docker',
            'yaml': 'manifests',
            'json': 'config'
        };
        return folders[type.toLowerCase()] || 'templates';
    }

    /**
     * Save template from chat response.
     */
    async saveTemplate(template: { name: string; type: string; content: string }): Promise<vscode.Uri | undefined> {
        const folder = this.getFolderForTemplateType(template.type);
        return this.createFile(template.name, template.content, folder);
    }

    /**
     * Save all templates from a chat response.
     */
    async saveAllTemplates(templates: Array<{ name: string; type: string; content: string }>): Promise<boolean> {
        return this.createInfrastructureProject(templates);
    }
}
