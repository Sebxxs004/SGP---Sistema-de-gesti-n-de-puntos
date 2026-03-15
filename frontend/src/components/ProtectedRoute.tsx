import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '../store/useAuthStore';

export const ProtectedRoute = () => {
  const token = useAuthStore((state) => state.token);
  const tenantId = useAuthStore((state) => state.tenantId);

  // Consider authenticated if we have both Token and TenantId
  if (!token || !tenantId) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />; // Render child routes
};
