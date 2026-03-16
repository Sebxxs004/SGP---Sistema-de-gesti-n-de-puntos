import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export interface User {
  id: string;
  email: string;
  role: string;
}

export interface BranchSummary {
  id: string;
  name: string;
  isPrimary: boolean;
}

interface AuthState {
  token: string | null;
  tenantId: string | null;
  currentBranchId: string | null;
  // Compatibility alias for existing consumers; mirrors currentBranchId.
  branchId: string | null;
  branches: BranchSummary[];
  user: User | null;
  setCredentials: (
    token: string,
    tenantId: string,
    user: User,
    branches: BranchSummary[],
    currentBranchId?: string | null
  ) => void;
  setTenantId: (tenantId: string) => void;
  setCurrentBranchId: (branchId: string) => void;
  setBranchId: (branchId: string) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      token: null,
      tenantId: null,
      currentBranchId: null,
      branchId: null,
      branches: [],
      user: null,

      setCredentials: (token, tenantId, user, branches, currentBranchId = null) =>
        set({ token, tenantId, user, branches, currentBranchId, branchId: currentBranchId }),
      
      setTenantId: (tenantId) => set({ tenantId }),

      setCurrentBranchId: (currentBranchId) => set({ currentBranchId, branchId: currentBranchId }),

      setBranchId: (branchId) => set({ currentBranchId: branchId, branchId }),

      logout: () => set({ token: null, tenantId: null, currentBranchId: null, branchId: null, branches: [], user: null }),
    }),
    {
      name: 'auth-storage', // name of the item in the storage (must be unique)
    }
  )
);
