import { Navigate, Outlet } from 'react-router-dom';
import { useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/useAuthStore';

export const ProtectedRoute = () => {
  const location = useLocation();
  const token = useAuthStore((state) => state.token);
  const tenantId = useAuthStore((state) => state.tenantId);
  const branchId = useAuthStore((state) => state.branchId);

  // Consider authenticated if we have both Token and TenantId
  if (!token || !tenantId) {
    return <Navigate to="/login" replace />;
  }

  // Force branch selection before entering module pages.
  if (!branchId && location.pathname !== '/select-branch') {
    return <Navigate to="/select-branch" replace />;
  }

  return <Outlet />; // Render child routes
};
