import { Navigate, Outlet } from 'react-router-dom';
import { useAuthStore } from '../store/useAuthStore';

type RoleGuardProps = {
  allowedRoles: string[];
};

export const RoleGuard = ({ allowedRoles }: RoleGuardProps) => {
  const role = useAuthStore((state) => state.user?.role);

  if (!role || !allowedRoles.includes(role)) {
    return <Navigate to="/" replace />;
  }

  return <Outlet />;
};
