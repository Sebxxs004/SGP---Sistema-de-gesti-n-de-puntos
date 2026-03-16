import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../api/apiClient';
import { useAuthStore } from '../store/useAuthStore';

type UserBranch = {
  id: string;
  name: string;
  isPrimary: boolean;
};

type TenantUser = {
  id: string;
  name: string;
  email: string;
  role: string;
  branches: UserBranch[];
};

type UsersResponse = {
  success: boolean;
  data: TenantUser[];
};

type CreateUserPayload = {
  name: string;
  email: string;
  password: string;
  branchId: string;
  role: 'Admin' | 'Cajero';
};

export const Users = () => {
  const queryClient = useQueryClient();
  const availableBranches = useAuthStore((state) => state.branches);

  const [form, setForm] = useState<CreateUserPayload>({
    name: '',
    email: '',
    password: '',
    branchId: availableBranches[0]?.id ?? '',
    role: 'Cajero',
  });

  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const usersQuery = useQuery({
    queryKey: ['tenant-users'],
    queryFn: async () => {
      const response = await apiClient.get<UsersResponse>('/users');
      return response.data.data;
    },
  });

  const createUserMutation = useMutation({
    mutationFn: async (payload: CreateUserPayload) => {
      await apiClient.post('/users', payload);
    },
    onSuccess: async () => {
      setSuccessMessage('Usuario creado correctamente.');
      setErrorMessage(null);
      setForm((prev) => ({
        ...prev,
        name: '',
        email: '',
        password: '',
      }));
      await queryClient.invalidateQueries({ queryKey: ['tenant-users'] });
    },
    onError: (error: unknown) => {
      const message =
        typeof error === 'object' && error !== null && 'response' in error
          ? (error as { response?: { data?: { error?: { message?: string } } } }).response?.data?.error?.message
          : undefined;

      setSuccessMessage(null);
      setErrorMessage(message ?? 'No fue posible crear el usuario.');
    },
  });

  const users = useMemo(() => usersQuery.data ?? [], [usersQuery.data]);

  const handleChange = (field: keyof CreateUserPayload, value: string) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setErrorMessage(null);
    setSuccessMessage(null);
    await createUserMutation.mutateAsync(form);
  };

  return (
    <div className="space-y-8">
      <section className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
        <h1 className="text-2xl font-bold text-gray-900">Usuarios</h1>
        <p className="mt-1 text-sm text-gray-500">Administra usuarios del tenant actual.</p>
      </section>

      <section className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
        <h2 className="text-lg font-semibold text-gray-900">Invitar / Crear Usuario</h2>

        {errorMessage && (
          <div className="mt-4 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {errorMessage}
          </div>
        )}

        {successMessage && (
          <div className="mt-4 rounded-lg border border-green-200 bg-green-50 px-4 py-3 text-sm text-green-700">
            {successMessage}
          </div>
        )}

        <form className="mt-6 grid gap-4 md:grid-cols-2" onSubmit={handleSubmit}>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">Nombre</label>
            <input
              value={form.name}
              onChange={(e) => handleChange('name', e.target.value)}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
              placeholder="Nombre completo"
              required
            />
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">Email</label>
            <input
              type="email"
              value={form.email}
              onChange={(e) => handleChange('email', e.target.value)}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
              placeholder="correo@empresa.com"
              required
            />
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">Password</label>
            <input
              type="password"
              value={form.password}
              onChange={(e) => handleChange('password', e.target.value)}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
              placeholder="Temporal o inicial"
              required
            />
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">Sucursal Asignada</label>
            <select
              value={form.branchId}
              onChange={(e) => handleChange('branchId', e.target.value)}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
              required
            >
              <option value="" disabled>Selecciona una sucursal</option>
              {availableBranches.map((branch) => (
                <option key={branch.id} value={branch.id}>
                  {branch.name}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">Rol</label>
            <select
              value={form.role}
              onChange={(e) => handleChange('role', e.target.value)}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
              required
            >
              <option value="Admin">Admin</option>
              <option value="Cajero">Cajero</option>
            </select>
          </div>

          <div className="md:col-span-2">
            <button
              type="submit"
              disabled={createUserMutation.isPending}
              className="rounded-lg bg-blue-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-70"
            >
              {createUserMutation.isPending ? 'Creando...' : 'Crear Usuario'}
            </button>
          </div>
        </form>
      </section>

      <section className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
        <div className="border-b border-gray-200 px-6 py-4">
          <h2 className="text-lg font-semibold text-gray-900">Usuarios del Tenant</h2>
        </div>

        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-6 py-3 text-left font-semibold text-gray-600">Nombre</th>
                <th className="px-6 py-3 text-left font-semibold text-gray-600">Email</th>
                <th className="px-6 py-3 text-left font-semibold text-gray-600">Rol</th>
                <th className="px-6 py-3 text-left font-semibold text-gray-600">Sucursal</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200 bg-white">
              {usersQuery.isLoading && (
                <tr>
                  <td colSpan={4} className="px-6 py-8 text-center text-gray-500">Cargando usuarios...</td>
                </tr>
              )}

              {!usersQuery.isLoading && users.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-6 py-8 text-center text-gray-500">No hay usuarios registrados.</td>
                </tr>
              )}

              {users.map((user) => {
                const primaryBranch = user.branches.find((b) => b.isPrimary) ?? user.branches[0];
                return (
                  <tr key={user.id}>
                    <td className="px-6 py-4 text-gray-900">{user.name}</td>
                    <td className="px-6 py-4 text-gray-700">{user.email}</td>
                    <td className="px-6 py-4">
                      <span className="rounded-full bg-blue-50 px-2.5 py-1 text-xs font-medium text-blue-700">
                        {user.role}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-gray-700">{primaryBranch?.name ?? 'Sin sucursal'}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
};
