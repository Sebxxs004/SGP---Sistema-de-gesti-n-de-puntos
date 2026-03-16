import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export interface User {
  id: string;
  email: string;
}

export interface BranchSummary {
  id: string;
  name: string;
  isPrimary: boolean;
}

interface AuthState {
  token: string | null;
  tenantId: string | null;
  branchId: string | null;
  branches: BranchSummary[];
  user: User | null;
  setCredentials: (
    token: string,
    tenantId: string,
    user: User,
    branches: BranchSummary[],
    branchId?: string | null
  ) => void;
  setTenantId: (tenantId: string) => void;
  setBranchId: (branchId: string) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      tenantId: null,
      branchId: null,
      branches: [],
      user: null,

      setCredentials: (token, tenantId, user, branches, branchId = null) =>
        set({ token, tenantId, user, branches, branchId }),
      
      setTenantId: (tenantId) => set({ tenantId }),

      setBranchId: (branchId) => set({ branchId }),

      logout: () => set({ token: null, tenantId: null, branchId: null, branches: [], user: null }),
    }),
    {
      name: 'auth-storage', // name of the item in the storage (must be unique)
    }
  )
);
