import { useEffect, useState } from 'react';
import { isAxiosError } from 'axios';
import apiClient from '../api/apiClient';

type CustomerItem = {
  id: string;
  name: string;
  documentNumber?: string;
  email?: string;
  phone?: string;
  address?: string;
  isActive: boolean;
};

type CustomersResponse = {
  success: boolean;
  data: {
    items: CustomerItem[];
    page: number;
    pageSize: number;
    total: number;
    totalPages: number;
  };
};

type CustomerForm = {
  name: string;
  documentNumber: string;
  email: string;
  phone: string;
  address: string;
};

const defaultForm: CustomerForm = {
  name: '',
  documentNumber: '',
  email: '',
  phone: '',
  address: '',
};

export const Customers = () => {
  const [customers, setCustomers] = useState<CustomerItem[]>([]);
  const [search, setSearch] = useState('');
  const [includeInactive, setIncludeInactive] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);
  const [totalPages, setTotalPages] = useState(1);
  const [total, setTotal] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [notification, setNotification] = useState<{ message: string; isError: boolean } | null>(null);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<CustomerForm>(defaultForm);
  const [isSaving, setIsSaving] = useState(false);

  const loadCustomers = async () => {
    setIsLoading(true);
    try {
      const response = await apiClient.get<CustomersResponse>('/customers', {
        params: {
          page,
          pageSize,
          search: search.trim() || undefined,
          includeInactive,
        },
      });

      setCustomers(response.data.data.items ?? []);
      setTotal(response.data.data.total ?? 0);
      setTotalPages(Math.max(response.data.data.totalPages ?? 1, 1));
    } catch (error) {
      const apiErrorMessage = isAxiosError(error) ? error.response?.data?.error?.message : undefined;
      setNotification({ message: apiErrorMessage ?? 'No se pudo cargar el directorio de clientes.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      loadCustomers();
    }, 250);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [page, pageSize, search, includeInactive]);

  const openCreateModal = () => {
    setEditingId(null);
    setForm(defaultForm);
    setIsModalOpen(true);
  };

  const openEditModal = (customer: CustomerItem) => {
    setEditingId(customer.id);
    setForm({
      name: customer.name,
      documentNumber: customer.documentNumber ?? '',
      email: customer.email ?? '',
      phone: customer.phone ?? '',
      address: customer.address ?? '',
    });
    setIsModalOpen(true);
  };

  const closeModal = () => {
    if (isSaving) return;
    setIsModalOpen(false);
    setEditingId(null);
    setForm(defaultForm);
  };

  const handleSaveCustomer = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!form.name.trim()) {
      setNotification({ message: 'El nombre del cliente es obligatorio.', isError: true });
      setTimeout(() => setNotification(null), 4000);
      return;
    }

    setIsSaving(true);
    try {
      const payload = {
        name: form.name,
        documentNumber: form.documentNumber || undefined,
        email: form.email || undefined,
        phone: form.phone || undefined,
        address: form.address || undefined,
      };

      if (editingId) {
        await apiClient.put(`/customers/${editingId}`, payload);
      } else {
        await apiClient.post('/customers', payload);
      }

      setNotification({ message: editingId ? 'Cliente actualizado correctamente.' : 'Cliente creado correctamente.', isError: false });
      closeModal();
      await loadCustomers();
      setTimeout(() => setNotification(null), 4000);
    } catch (error) {
      const apiErrorMessage = isAxiosError(error) ? error.response?.data?.error?.message : undefined;
      setNotification({ message: apiErrorMessage ?? 'No se pudo guardar el cliente.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsSaving(false);
    }
  };

  const handleDeactivateCustomer = async (customerId: string) => {
    try {
      await apiClient.patch(`/customers/${customerId}/deactivate`);
      setNotification({ message: 'Cliente desactivado.', isError: false });
      await loadCustomers();
      setTimeout(() => setNotification(null), 4000);
    } catch (error) {
      const apiErrorMessage = isAxiosError(error) ? error.response?.data?.error?.message : undefined;
      setNotification({ message: apiErrorMessage ?? 'No se pudo desactivar el cliente.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    }
  };

  return (
    <div className="space-y-6">
      <section className="rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
        <h1 className="text-2xl font-bold text-gray-900">Clientes</h1>
        <p className="mt-1 text-sm text-gray-500">Directorio central de clientes por tenant.</p>
      </section>

      <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex flex-1 items-center gap-2">
            <input
              value={search}
              onChange={(e) => {
                setPage(1);
                setSearch(e.target.value);
              }}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
              placeholder="Buscar por nombre o documento"
            />
            <label className="inline-flex items-center gap-2 text-sm text-gray-700">
              <input
                type="checkbox"
                checked={includeInactive}
                onChange={(e) => {
                  setPage(1);
                  setIncludeInactive(e.target.checked);
                }}
              />
              Incluir inactivos
            </label>
          </div>

          <button
            type="button"
            onClick={openCreateModal}
            className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            Nuevo Cliente
          </button>
        </div>

        {notification && (
          <div className={`mt-4 rounded-lg px-4 py-3 text-sm ${notification.isError ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-700'}`}>
            {notification.message}
          </div>
        )}

        <div className="mt-4 overflow-x-auto rounded-xl border border-gray-200">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50 text-xs uppercase text-gray-600">
              <tr>
                <th className="px-4 py-3 text-left">Cliente</th>
                <th className="px-4 py-3 text-left">Documento</th>
                <th className="px-4 py-3 text-left">Contacto</th>
                <th className="px-4 py-3 text-left">Dirección</th>
                <th className="px-4 py-3 text-center">Estado</th>
                <th className="px-4 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 bg-white">
              {isLoading && (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-gray-500">Cargando clientes...</td>
                </tr>
              )}

              {!isLoading && customers.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-gray-500">No se encontraron clientes.</td>
                </tr>
              )}

              {!isLoading && customers.map((customer) => (
                <tr key={customer.id}>
                  <td className="px-4 py-3 font-medium text-gray-900">{customer.name}</td>
                  <td className="px-4 py-3 text-gray-700">{customer.documentNumber || '-'}</td>
                  <td className="px-4 py-3 text-gray-700">
                    <p>{customer.email || '-'}</p>
                    <p className="text-xs text-gray-500">{customer.phone || ''}</p>
                  </td>
                  <td className="px-4 py-3 text-gray-700">{customer.address || '-'}</td>
                  <td className="px-4 py-3 text-center">
                    <span className={`rounded-full px-2 py-1 text-xs font-medium ${customer.isActive ? 'bg-green-50 text-green-700' : 'bg-gray-100 text-gray-600'}`}>
                      {customer.isActive ? 'Activo' : 'Inactivo'}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right">
                    <div className="flex justify-end gap-2">
                      <button
                        type="button"
                        onClick={() => openEditModal(customer)}
                        className="rounded-md border border-gray-300 px-2.5 py-1 text-xs text-gray-700 hover:bg-gray-100"
                      >
                        Editar
                      </button>
                      {customer.isActive && (
                        <button
                          type="button"
                          onClick={() => handleDeactivateCustomer(customer.id)}
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

        <div className="mt-4 flex flex-col gap-2 text-sm text-gray-600 sm:flex-row sm:items-center sm:justify-between">
          <p>Total: {total} clientes</p>
          <div className="flex items-center gap-2">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((prev) => Math.max(prev - 1, 1))}
              className="rounded-md border border-gray-300 px-3 py-1.5 disabled:opacity-50"
            >
              Anterior
            </button>
            <span>Página {page} de {totalPages}</span>
            <button
              type="button"
              disabled={page >= totalPages}
              onClick={() => setPage((prev) => Math.min(prev + 1, totalPages))}
              className="rounded-md border border-gray-300 px-3 py-1.5 disabled:opacity-50"
            >
              Siguiente
            </button>
          </div>
        </div>
      </section>

      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="w-full max-w-xl rounded-xl bg-white p-6 shadow-lg">
            <h3 className="text-lg font-semibold text-gray-900">{editingId ? 'Editar Cliente' : 'Nuevo Cliente'}</h3>
            <p className="mt-1 text-sm text-gray-500">Los campos opcionales te ayudan a identificar mejor a quién le vendes.</p>

            <form className="mt-4 grid gap-3 sm:grid-cols-2" onSubmit={handleSaveCustomer}>
              <div className="sm:col-span-2">
                <label className="mb-1 block text-sm font-medium text-gray-700">Nombre</label>
                <input
                  value={form.name}
                  onChange={(e) => setForm((prev) => ({ ...prev, name: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  required
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Documento</label>
                <input
                  value={form.documentNumber}
                  onChange={(e) => setForm((prev) => ({ ...prev, documentNumber: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Teléfono</label>
                <input
                  value={form.phone}
                  onChange={(e) => setForm((prev) => ({ ...prev, phone: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                />
              </div>

              <div className="sm:col-span-2">
                <label className="mb-1 block text-sm font-medium text-gray-700">Email</label>
                <input
                  type="email"
                  value={form.email}
                  onChange={(e) => setForm((prev) => ({ ...prev, email: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                />
              </div>

              <div className="sm:col-span-2">
                <label className="mb-1 block text-sm font-medium text-gray-700">Dirección</label>
                <input
                  value={form.address}
                  onChange={(e) => setForm((prev) => ({ ...prev, address: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                />
              </div>

              <div className="sm:col-span-2 mt-2 flex gap-2">
                <button
                  type="button"
                  onClick={closeModal}
                  className="flex-1 rounded-lg border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  disabled={isSaving}
                  className="flex-1 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
                >
                  {isSaving ? 'Guardando...' : (editingId ? 'Guardar cambios' : 'Crear cliente')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
