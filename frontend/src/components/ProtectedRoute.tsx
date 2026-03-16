import { Navigate, Outlet } from 'react-router-dom';
import { useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/useAuthStore';

export const ProtectedRoute = () => {
  const location = useLocation();
  const token = useAuthStore((state) => state.token);
  const tenantId = useAuthStore((state) => state.tenantId);
  const currentBranchId = useAuthStore((state) => state.currentBranchId);
  const currentSessionId = useAuthStore((state) => state.currentSessionId);

  // Consider authenticated if we have both Token and TenantId
  if (!token || !tenantId) {
    return <Navigate to="/login" replace />;
  }

  // Force branch selection before entering module pages.
  if (!currentBranchId && location.pathname !== '/select-branch') {
    return <Navigate to="/select-branch" replace />;
  }

  if (
    location.pathname.startsWith('/pos') &&
    location.pathname !== '/pos/open' &&
    !currentSessionId
  ) {
    return <Navigate to="/pos/open" replace />;
  }

  return <Outlet />; // Render child routes
};
