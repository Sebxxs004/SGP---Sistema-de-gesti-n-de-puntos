import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/apiClient';
import { useAuthStore } from '../store/useAuthStore';

type BranchItem = {
  id: string;
  name: string;
  address: string;
  phone?: string;
  timezone: string;
  isActive: boolean;
};

type BranchesResponse = {
  success: boolean;
  data: BranchItem[];
};

type CompanySettings = {
  id: string;
  name: string;
  taxId: string;
  thankYouMessage?: string;
};

type CompanySettingsResponse = {
  success: boolean;
  data: CompanySettings;
};

type BranchForm = {
  id?: string;
  name: string;
  address: string;
  phone: string;
  timezone: string;
};

const defaultBranchForm: BranchForm = {
  name: '',
  address: '',
  phone: '',
  timezone: 'America/Bogota',
};

export const Settings = () => {
  const queryClient = useQueryClient();
  const role = useAuthStore((state) => state.user?.role);

  const [activeTab, setActiveTab] = useState<'branches' | 'company'>('branches');
  const [branchForm, setBranchForm] = useState<BranchForm>(defaultBranchForm);
  const [companyMessage, setCompanyMessage] = useState('');
  const [notification, setNotification] = useState<{ text: string; isError: boolean } | null>(null);

  const branchesQuery = useQuery({
    queryKey: ['settings-branches'],
    queryFn: async () => {
      const response = await apiClient.get<BranchesResponse>('/core/branches');
      return response.data.data;
    },
    enabled: role === 'Admin',
  });

  const companyQuery = useQuery({
    queryKey: ['settings-company'],
    queryFn: async () => {
      const response = await apiClient.get<CompanySettingsResponse>('/core/company/settings');
      return response.data.data;
    },
    enabled: role === 'Admin',
  });

  const saveBranchMutation = useMutation({
    mutationFn: async (payload: BranchForm) => {
      if (payload.id) {
        await apiClient.put(`/core/branches/${payload.id}`, {
          name: payload.name,
          address: payload.address,
          phone: payload.phone,
          timezone: payload.timezone,
        });
        return;
      }

      await apiClient.post('/core/branches', {
        name: payload.name,
        address: payload.address,
        phone: payload.phone,
        timezone: payload.timezone,
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['settings-branches'] });
      setBranchForm(defaultBranchForm);
      setNotification({ text: 'Sucursal guardada correctamente.', isError: false });
    },
    onError: () => {
      setNotification({ text: 'No se pudo guardar la sucursal.', isError: true });
    },
  });

  const deactivateBranchMutation = useMutation({
    mutationFn: async (branchId: string) => {
      await apiClient.patch(`/core/branches/${branchId}/deactivate`);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['settings-branches'] });
      setNotification({ text: 'Sucursal desactivada.', isError: false });
    },
    onError: () => {
      setNotification({ text: 'No se pudo desactivar la sucursal.', isError: true });
    },
  });

  const saveCompanyMutation = useMutation({
    mutationFn: async (message: string) => {
      await apiClient.put('/core/company/settings', { thankYouMessage: message });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['settings-company'] });
      setNotification({ text: 'Mensaje de agradecimiento actualizado.', isError: false });
    },
    onError: () => {
      setNotification({ text: 'No se pudo guardar la configuración de empresa.', isError: true });
    },
  });

  const branches = useMemo(() => branchesQuery.data ?? [], [branchesQuery.data]);

  if (role !== 'Admin') {
    return (
      <div className="rounded-xl border border-amber-200 bg-amber-50 p-6 text-amber-800">
        Solo los administradores pueden acceder a Configuracion.
      </div>
    );
  }

  const handleBranchSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    await saveBranchMutation.mutateAsync(branchForm);
  };

  const handleCompanySubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    await saveCompanyMutation.mutateAsync(companyMessage);
  };

  const beginEditBranch = (branch: BranchItem) => {
    setBranchForm({
      id: branch.id,
      name: branch.name,
      address: branch.address,
      phone: branch.phone ?? '',
      timezone: branch.timezone,
    });
    setActiveTab('branches');
  };

  const companyData = companyQuery.data;

  return (
    <div className="space-y-6">
      <section className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
        <h1 className="text-2xl font-bold text-gray-900">Configuracion</h1>
        <p className="mt-1 text-sm text-gray-500">Gestiona sucursales y branding administrativo.</p>
      </section>

      <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <div className="mb-4 flex gap-2 border-b border-gray-200 pb-3">
          <button
            type="button"
            className={`rounded-lg px-3 py-1.5 text-sm font-medium ${activeTab === 'branches' ? 'bg-blue-100 text-blue-700' : 'text-gray-600 hover:bg-gray-100'}`}
            onClick={() => setActiveTab('branches')}
          >
            Sucursales
          </button>
          <button
            type="button"
            className={`rounded-lg px-3 py-1.5 text-sm font-medium ${activeTab === 'company' ? 'bg-blue-100 text-blue-700' : 'text-gray-600 hover:bg-gray-100'}`}
            onClick={() => {
              setActiveTab('company');
              setCompanyMessage(companyData?.thankYouMessage ?? '');
            }}
          >
            Empresa
          </button>
        </div>

        {notification && (
          <div className={`mb-4 rounded-lg px-4 py-3 text-sm ${notification.isError ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-700'}`}>
            {notification.text}
          </div>
        )}

        {activeTab === 'branches' && (
          <div className="grid gap-6 lg:grid-cols-2">
            <form className="space-y-4 rounded-xl border border-gray-200 p-4" onSubmit={handleBranchSubmit}>
              <h2 className="text-lg font-semibold text-gray-900">{branchForm.id ? 'Editar sucursal' : 'Nueva sucursal'}</h2>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Nombre</label>
                <input
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  value={branchForm.name}
                  onChange={(e) => setBranchForm((prev) => ({ ...prev, name: e.target.value }))}
                  required
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Direccion</label>
                <input
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  value={branchForm.address}
                  onChange={(e) => setBranchForm((prev) => ({ ...prev, address: e.target.value }))}
                  required
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Telefono</label>
                <input
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  value={branchForm.phone}
                  onChange={(e) => setBranchForm((prev) => ({ ...prev, phone: e.target.value }))}
                  placeholder="+57 300 000 0000"
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Zona horaria</label>
                <input
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  value={branchForm.timezone}
                  onChange={(e) => setBranchForm((prev) => ({ ...prev, timezone: e.target.value }))}
                  required
                />
              </div>

              <div className="flex gap-2">
                <button
                  type="submit"
                  disabled={saveBranchMutation.isPending}
                  className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
                >
                  {saveBranchMutation.isPending ? 'Guardando...' : 'Guardar sucursal'}
                </button>
                {branchForm.id && (
                  <button
                    type="button"
                    onClick={() => setBranchForm(defaultBranchForm)}
                    className="rounded-lg border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-100"
                  >
                    Cancelar edicion
                  </button>
                )}
              </div>
            </form>

            <div className="overflow-hidden rounded-xl border border-gray-200">
              <table className="min-w-full divide-y divide-gray-200 text-sm">
                <thead className="bg-gray-50 text-xs uppercase text-gray-600">
                  <tr>
                    <th className="px-4 py-3 text-left">Sucursal</th>
                    <th className="px-4 py-3 text-left">Telefono</th>
                    <th className="px-4 py-3 text-center">Estado</th>
                    <th className="px-4 py-3 text-right">Acciones</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 bg-white">
                  {branchesQuery.isLoading && (
                    <tr><td colSpan={4} className="px-4 py-8 text-center text-gray-500">Cargando sucursales...</td></tr>
                  )}

                  {!branchesQuery.isLoading && branches.length === 0 && (
                    <tr><td colSpan={4} className="px-4 py-8 text-center text-gray-500">Sin sucursales registradas.</td></tr>
                  )}

                  {branches.map((branch) => (
                    <tr key={branch.id}>
                      <td className="px-4 py-3">
                        <p className="font-medium text-gray-900">{branch.name}</p>
                        <p className="text-xs text-gray-500">{branch.address}</p>
                      </td>
                      <td className="px-4 py-3 text-gray-700">{branch.phone || '-'}</td>
                      <td className="px-4 py-3 text-center">
                        <span className={`rounded-full px-2 py-1 text-xs font-medium ${branch.isActive ? 'bg-green-50 text-green-700' : 'bg-gray-100 text-gray-500'}`}>
                          {branch.isActive ? 'Activa' : 'Inactiva'}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right">
                        <div className="flex justify-end gap-2">
                          <button
                            type="button"
                            onClick={() => beginEditBranch(branch)}
                            className="rounded-md border border-gray-300 px-2.5 py-1 text-xs text-gray-700 hover:bg-gray-100"
                          >
                            Editar
                          </button>
                          {branch.isActive && (
                            <button
                              type="button"
                              onClick={() => deactivateBranchMutation.mutate(branch.id)}
                              className="rounded-md border border-red-200 px-2.5 py-1 text-xs text-red-700 hover:bg-red-50"
                            >
                              Desactivar
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {activeTab === 'company' && (
          <form className="max-w-2xl space-y-4 rounded-xl border border-gray-200 p-4" onSubmit={handleCompanySubmit}>
            <h2 className="text-lg font-semibold text-gray-900">Empresa y Ticket</h2>
            <p className="text-sm text-gray-500">Define el mensaje de agradecimiento que aparecera en los comprobantes.</p>

            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Mensaje de agradecimiento</label>
              <textarea
                className="min-h-28 w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                value={companyMessage}
                onChange={(e) => setCompanyMessage(e.target.value)}
                placeholder="Gracias por su compra. Vuelva pronto."
              />
            </div>

            <button
              type="submit"
              disabled={saveCompanyMutation.isPending}
              className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
            >
              {saveCompanyMutation.isPending ? 'Guardando...' : 'Guardar mensaje'}
            </button>
          </form>
        )}
      </div>
    </div>
  );
};
