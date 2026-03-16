import axios from 'axios';
import { useAuthStore } from '../store/useAuthStore';

// Read API URL from Vite env and fallback to local API launchSettings port.
export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5102/api/v1';

const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor to add token and tenantId
apiClient.interceptors.request.use(
  (config) => {
    // We read directly from the Zustand store's state
    const { token, tenantId, branchId } = useAuthStore.getState();

    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    if (tenantId) {
      config.headers['X-Tenant-Id'] = tenantId;
    }

    if (branchId) {
      config.headers['X-Branch-Id'] = branchId;
    }

    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Response interceptor to handle 401s
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Token is invalid or expired
      useAuthStore.getState().logout();
      window.location.href = '/login'; 
    }
    return Promise.reject(error);
  }
);

export default apiClient;
