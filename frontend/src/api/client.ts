/**
 * Centralized API client with auth interceptor placeholder (Phase 5).
 */
import axios from 'axios';
import type { ErrorResponse } from './contracts/data';

const api = axios.create({
  baseURL: '/api',
  timeout: 30000,
});

// Phase 4: Auth interceptor placeholder (Phase 5 will add JWT)
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('auth_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Normalize error responses into a consistent shape
api.interceptors.response.use(
  (response) => response,
  (error) => {
    const data = error.response?.data as unknown as ErrorResponse | undefined;
    const normalized = data?.error
      ? data.error
      : { code: 'UnknownError', message: error.message };
    return Promise.reject({ ...normalized, status: error.response?.status ?? 0 });
  },
);

export default api;
