// Chat types and interfaces
export interface Conversation {
  id: string;
  title: string;
  description?: string;
  userId: string;
  createdAt: string;
  updatedAt: string;
  isArchived: boolean;
  messages: ChatMessage[];
  messageCount?: number;
  context?: ConversationContext;
}

export interface ChatMessage {
  id: string;
  conversationId: string;
  content: string;
  role: MessageRole;
  timestamp: string;
  status: MessageStatus;
  attachments: MessageAttachment[];
  parentMessageId?: string;
  tools: string[];
  toolResult?: ToolExecutionResult;
  metadata?: MessageMetadata;
}

export interface ConversationContext {
  id: string;
  conversationId: string;
  type: string;
  title: string;
  summary: string;
  data: Record<string, any>;
  createdAt: string;
  lastAccessedAt: string;
  tags: string[];
}

export interface MessageAttachment {
  id: string;
  messageId: string;
  fileName: string;
  contentType: string;
  size: number;
  storageUrl: string;
  type: AttachmentType;
  uploadedAt: string;
}

export interface ToolExecutionResult {
  toolName: string;
  success: boolean;
  result?: any;
  error?: string;
  parameters: Record<string, any>;
  executedAt: string;
  duration: string;
}

export enum MessageRole {
  User = 'User',
  Assistant = 'Platform Engineering Copilot',
  System = 'System',
  Tool = 'Tool'
}

export enum MessageStatus {
  Sending = 'Sending',
  Sent = 'Sent',
  Processing = 'Processing',
  Completed = 'Completed',
  Error = 'Error',
  Retry = 'Retry'
}

export enum AttachmentType {
  Document = 'Document',
  Image = 'Image',
  Code = 'Code',
  Configuration = 'Configuration',
  Log = 'Log'
}

export interface ChatRequest {
  conversationId: string;
  message: string;
  attachmentIds?: string[];
  context?: Record<string, any>;
}

export interface ChatResponse {
  messageId: string;
  content: string;
  success: boolean;
  error?: string;
  suggestedActions: string[];
  recommendedTools: ToolInfo[];
  metadata: Record<string, any>;
}

export interface ToolInfo {
  name: string;
  description: string;
  parameters: Record<string, any>;
  category: string;
}

export interface CreateConversationRequest {
  title?: string;
  userId?: string;
}

// ============================================================================
// INTELLIGENT CHAT METADATA TYPES
// ============================================================================

export interface MessageMetadata {
  intentType?: string;
  confidence?: number;
  toolName?: string;
  toolExecuted?: boolean;
  toolResult?: any;
  toolChain?: ToolChainInfo;
  suggestions?: ProactiveSuggestion[];
  processingTimeMs?: number;
  fallback?: boolean;
  error?: string;
  errorType?: string;
  [key: string]: any;
}

export interface ToolChainInfo {
  chainId: string;
  status: string;
  totalSteps: number;
  completedSteps: number;
  successRate: number;
  summary?: string;
}

export interface ProactiveSuggestion {
  title: string;
  description: string;
  priority: 'High' | 'Medium' | 'Low';
  category: string;
  icon?: string;
  confidence?: number;
  suggestedPrompt: string;
  expectedOutcome?: string;
}