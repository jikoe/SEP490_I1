import { apiClient } from '../common/baseApiClient';
import type { LoginRequest, LoginResponse } from '../types/auth';

export const authApi = {
  login: async (payload: LoginRequest) =>
    apiClient.request<LoginResponse>('/Authentication/login', {
      method: 'POST',
      body: JSON.stringify(payload)
    }),

  refresh: async () =>
    apiClient.request<LoginResponse>('/Authentication/refresh', {
      method: 'POST',
      body: JSON.stringify({})
    }),

  logout: async (token?: string) =>
    apiClient.request<{ message: string }>('/Authentication/logout', {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      body: JSON.stringify({})
    })
};
