const vscode = require('vscode');
const http = require('http');
const https = require('https');

let statusBarItem;
let outputChannel;
let historyItems = [];

function activate(context) {
    outputChannel = vscode.window.createOutputChannel('AI Agent');
    outputChannel.appendLine('AI Agent Platform extension activated');

    // Status bar
    statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
    statusBarItem.text = '$(hubot) AI Agent';
    statusBarItem.tooltip = 'AI Agent Platform — click to send request';
    statusBarItem.command = 'aiagent.sendRequest';
    statusBarItem.show();
    context.subscriptions.push(statusBarItem);

    // Commands
    context.subscriptions.push(
        vscode.commands.registerCommand('aiagent.sendRequest', handleSendRequest),
        vscode.commands.registerCommand('aiagent.applyChanges', handleApplyChanges),
        vscode.commands.registerCommand('aiagent.showHistory', handleShowHistory),
        vscode.commands.registerCommand('aiagent.setProject', handleSetProject)
    );
}

async function handleSendRequest() {
    const editor = vscode.window.activeTextEditor;
    if (!editor) {
        vscode.window.showWarningMessage('No active editor');
        return;
    }

    const selection = editor.selection;
    const selectedText = editor.document.getText(selection);
    const language = editor.document.languageId;

    let query;
    if (selectedText) {
        const action = await vscode.window.showQuickPick(
            ['Fix this code', 'Add validation', 'Refactor', 'Add documentation', 'Explain this code', 'Custom query...'],
            { placeHolder: 'What should the agent do with the selected code?' }
        );
        if (!action) return;

        if (action === 'Custom query...') {
            query = await vscode.window.showInputBox({ prompt: 'Enter your request' });
        } else {
            query = `${action}:\n\nFile: ${editor.document.fileName}\nLanguage: ${language}\n\n\`\`\`${language}\n${selectedText}\n\`\`\``;
        }
    } else {
        query = await vscode.window.showInputBox({ 
            prompt: 'Enter your request for the AI agent',
            placeHolder: 'Add email validation to UserService.CreateUser'
        });
    }

    if (!query) return;

    statusBarItem.text = '$(sync~spin) AI Agent: processing...';
    outputChannel.appendLine(`\n📝 Request: ${query.substring(0, 100)}...`);

    try {
        const response = await sendToAgent('/api/agent/request', { query });
        outputChannel.appendLine(`🤖 Response (${response.message?.length || 0} chars)`);
        
        // Store in history
        historyItems.unshift({
            timestamp: new Date().toISOString(),
            query: query,
            response: response.message
        });
        if (historyItems.length > 20) historyItems.pop();

        // Show result
        const action = await vscode.window.showInformationMessage(
            'Agent response received',
            'Show Details', 'Apply to File', 'Dismiss'
        );

        if (action === 'Show Details') {
            showResponseDetails(response.message);
        } else if (action === 'Apply to File') {
            applyToFile(response.message, editor);
        }

    } catch (error) {
        vscode.window.showErrorMessage(`Agent error: ${error.message}`);
        outputChannel.appendLine(`❌ Error: ${error.message}`);
    } finally {
        statusBarItem.text = '$(hubot) AI Agent';
    }
}

async function handleApplyChanges() {
    const editor = vscode.window.activeTextEditor;
    if (!editor) return;

    const lastResponse = historyItems[0]?.response;
    if (!lastResponse) {
        vscode.window.showWarningMessage('No recent agent response to apply');
        return;
    }

    applyToFile(lastResponse, editor);
}

function applyToFile(responseText, editor) {
    // Extract code from markdown blocks
    const codeMatch = responseText.match(/```(?:\w+)?\s*\n([\s\S]*?)\n```/);
    if (!codeMatch) {
        vscode.window.showWarningMessage('No code block found in agent response');
        return;
    }

    const code = codeMatch[1];
    editor.edit(editBuilder => {
        if (editor.selection.isEmpty) {
            // Replace entire file
            const fullRange = new vscode.Range(
                editor.document.positionAt(0),
                editor.document.positionAt(editor.document.getText().length)
            );
            editBuilder.replace(fullRange, code);
        } else {
            // Replace selection
            editBuilder.replace(editor.selection, code);
        }
    });
    vscode.window.showInformationMessage('Changes applied');
}

async function handleShowHistory() {
    if (historyItems.length === 0) {
        vscode.window.showInformationMessage('No history yet');
        return;
    }

    const items = historyItems.map(h => ({
        label: h.query.substring(0, 80),
        description: new Date(h.timestamp).toLocaleTimeString(),
        detail: h.response?.substring(0, 200)
    }));

    const selected = await vscode.window.showQuickPick(items, {
        placeHolder: 'Select a request to view details'
    });

    if (selected) {
        const item = historyItems.find(h => h.query.substring(0, 80) === selected.label);
        if (item) showResponseDetails(item.response);
    }
}

async function handleSetProject() {
    const folders = vscode.workspace.workspaceFolders;
    const projectPath = folders && folders.length > 0 
        ? folders[0].uri.fsPath 
        : await vscode.window.showInputBox({ prompt: 'Enter project path' });

    if (!projectPath) return;

    try {
        await sendToAgent('/api/agent/set-project', { path: projectPath });
        vscode.window.showInformationMessage(`Project set to: ${projectPath}`);
    } catch (error) {
        vscode.window.showErrorMessage(`Failed to set project: ${error.message}`);
    }
}

function showResponseDetails(message) {
    outputChannel.clear();
    outputChannel.appendLine(message);
    outputChannel.show(true);
}

function sendToAgent(endpoint, data) {
    return new Promise((resolve, reject) => {
        const config = vscode.workspace.getConfiguration('aiagent');
        const apiUrl = config.get('apiUrl', 'http://localhost:5000');
        const url = new URL(endpoint, apiUrl);
        const body = JSON.stringify(data);

        const options = {
            hostname: url.hostname,
            port: url.port || 80,
            path: url.pathname,
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Content-Length': Buffer.byteLength(body)
            }
        };

        const req = http.request(options, (res) => {
            let responseData = '';
            res.on('data', (chunk) => { responseData += chunk; });
            res.on('end', () => {
                try {
                    resolve(JSON.parse(responseData));
                } catch {
                    resolve({ message: responseData });
                }
            });
        });

        req.on('error', reject);
        req.write(body);
        req.end();
    });
}

function deactivate() {
    if (statusBarItem) statusBarItem.dispose();
    if (outputChannel) outputChannel.dispose();
}

module.exports = { activate, deactivate };