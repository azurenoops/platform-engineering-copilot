import axios from 'axios';
import { Conversation, ChatMessage, ChatRequest, CreateConversationRequest } from '../types/chat';

const API_BASE_URL = process.env.REACT_APP_API_URL || '';

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

export const chatApi = {
  // Conversations
  async getConversations(userId: string = 'default-user'): Promise<Conversation[]> {
    const response = await apiClient.get(`/api/conversations?userId=${userId}`);
    return response.data;
  },

  async getConversation(conversationId: string): Promise<Conversation> {
    const response = await apiClient.get(`/api/conversations/${conversationId}`);
    return response.data;
  },

  async createConversation(request: CreateConversationRequest): Promise<Conversation> {
    const response = await apiClient.post('/api/conversations', request);
    return response.data;
  },

  async deleteConversation(conversationId: string): Promise<void> {
    await apiClient.delete(`/api/conversations/${conversationId}`);
  },

  async searchConversations(query: string, userId: string = 'default-user'): Promise<Conversation[]> {
    const response = await apiClient.get(`/api/conversations/search?query=${encodeURIComponent(query)}&userId=${userId}`);
    return response.data;
  },

  // Messages
  async getMessages(conversationId: string): Promise<ChatMessage[]> {
    const response = await apiClient.get(`/api/messages?conversationId=${conversationId}`);
    return response.data;
  },

  async sendMessage(request: ChatRequest): Promise<ChatMessage> {
    const response = await apiClient.post('/api/messages', request);
    return response.data;
  },

  // File upload
  async uploadFile(messageId: string, file: File): Promise<any> {
    const formData = new FormData();
    formData.append('file', file);
    
    const response = await apiClient.post(`/api/messages/${messageId}/attachments`, formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    
    return response.data;
  },
};