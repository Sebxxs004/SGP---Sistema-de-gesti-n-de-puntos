import { Navigate, useNavigate } from 'react-router-dom';
import { Store } from 'lucide-react';
import { useAuthStore } from '../store/useAuthStore';

export const BranchSelector = () => {
  const navigate = useNavigate();

  const token = useAuthStore((state) => state.token);
  const tenantId = useAuthStore((state) => state.tenantId);
  const branchId = useAuthStore((state) => state.branchId);
  const branches = useAuthStore((state) => state.branches);
  const setBranchId = useAuthStore((state) => state.setBranchId);

  if (!token || !tenantId) {
    return <Navigate to="/login" replace />;
  }

  if (branchId) {
    return <Navigate to="/" replace />;
  }

  const handleSelectBranch = (selectedBranchId: string) => {
    setBranchId(selectedBranchId);
    navigate('/');
  };

  return (
    <div className="min-h-screen bg-gray-50 p-6">
      <div className="mx-auto max-w-4xl">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-bold text-gray-900">Selecciona una sucursal</h1>
          <p className="mt-2 text-sm text-gray-500">Debes elegir una sucursal para continuar.</p>
        </div>

        {branches.length === 0 ? (
          <div className="rounded-xl border border-amber-200 bg-amber-50 p-6 text-center text-amber-800">
            No tienes sucursales asignadas. Contacta al administrador de tu empresa.
          </div>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {branches.map((branch) => (
              <button
                key={branch.id}
                type="button"
                onClick={() => handleSelectBranch(branch.id)}
                className="rounded-xl border border-gray-200 bg-white p-5 text-left shadow-sm transition hover:-translate-y-0.5 hover:border-blue-300 hover:shadow-md"
              >
                <div className="mb-3 inline-flex rounded-lg bg-blue-50 p-2 text-blue-600">
                  <Store size={18} />
                </div>
                <h2 className="text-lg font-semibold text-gray-900">{branch.name}</h2>
                {branch.isPrimary && (
                  <p className="mt-1 text-xs font-medium uppercase tracking-wide text-blue-600">
                    Sucursal principal sugerida
                  </p>
                )}
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
