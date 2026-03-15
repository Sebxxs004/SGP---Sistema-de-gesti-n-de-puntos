import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export interface User {
  id: string;
  email: string;
}

interface AuthState {
  token: string | null;
  tenantId: string | null;
  user: User | null;
  setCredentials: (token: string, tenantId: string, user: User) => void;
  setTenantId: (tenantId: string) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      tenantId: null,
      user: null,

      setCredentials: (token, tenantId, user) => set({ token, tenantId, user }),
      
      setTenantId: (tenantId) => set({ tenantId }),

      logout: () => set({ token: null, tenantId: null, user: null }),
    }),
    {
      name: 'auth-storage', // name of the item in the storage (must be unique)
    }
  )
);
