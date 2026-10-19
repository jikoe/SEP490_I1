export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api';

class BaseApiClient {
  private buildUrl(path: string) {
    return `${API_BASE_URL}${path}`;
  }

  async request<T>(path: string, options: RequestInit = {}): Promise<T> {
    const response = await fetch(this.buildUrl(path), {
      ...options,
      credentials: 'include',
      headers: {
        'Content-Type': 'application/json',
        ...(options.headers ?? {})
      }
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(errorText || 'Request failed.');
    }

    if (response.status === 204) {
      return undefined as T;
    }

    return (await response.json()) as T;
  }
}

export const apiClient = new BaseApiClient();
