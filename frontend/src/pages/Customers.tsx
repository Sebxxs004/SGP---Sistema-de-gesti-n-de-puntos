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

type AccountReceivableItem = {
  id: string;
  saleId: string;
  totalAmount: number;
  paidAmount: number;
  balance: number;
  dueDate: string;
  status: 'Pending' | 'Partial' | 'Paid';
  createdAt: string;
};

type ReceivablesResponse = {
  success: boolean;
  data: AccountReceivableItem[];
};

type CustomerPaymentHistoryItem = {
  cashMovementId: string;
  accountReceivableId: string;
  saleId: string;
  amount: number;
  registeredAt: string;
  note?: string;
};

type CustomerPaymentHistoryResponse = {
  success: boolean;
  data: CustomerPaymentHistoryItem[];
};

type ReceivablePaymentForm = {
  accountReceivableId: string;
  amount: string;
  paymentMethod: 'Cash' | 'CreditCard' | 'DebitCard' | 'Transfer' | 'Other';
  note: string;
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
  const [selectedCustomerForReceivables, setSelectedCustomerForReceivables] = useState<CustomerItem | null>(null);
  const [receivables, setReceivables] = useState<AccountReceivableItem[]>([]);
  const [isLoadingReceivables, setIsLoadingReceivables] = useState(false);
  const [paymentHistory, setPaymentHistory] = useState<CustomerPaymentHistoryItem[]>([]);
  const [isLoadingPaymentHistory, setIsLoadingPaymentHistory] = useState(false);
  const [paymentModal, setPaymentModal] = useState<ReceivablePaymentForm | null>(null);
  const [isRegisteringPayment, setIsRegisteringPayment] = useState(false);

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

  const loadPaymentHistory = async (customer: CustomerItem) => {
    setIsLoadingPaymentHistory(true);
    try {
      const response = await apiClient.get<CustomerPaymentHistoryResponse>(`/customers/${customer.id}/payments/history`);
      setPaymentHistory(response.data.data ?? []);
    } catch (error) {
      const apiErrorMessage = isAxiosError(error) ? error.response?.data?.error?.message : undefined;
      setPaymentHistory([]);
      setNotification({ message: apiErrorMessage ?? 'No se pudo cargar el historial de abonos.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsLoadingPaymentHistory(false);
    }
  };

  const loadReceivables = async (customer: CustomerItem) => {
    setSelectedCustomerForReceivables(customer);
    setIsLoadingReceivables(true);
    try {
      const response = await apiClient.get<ReceivablesResponse>(`/customers/${customer.id}/receivables`);
      setReceivables(response.data.data ?? []);
      await loadPaymentHistory(customer);
    } catch (error) {
      const apiErrorMessage = isAxiosError(error) ? error.response?.data?.error?.message : undefined;
      setReceivables([]);
      setPaymentHistory([]);
      setNotification({ message: apiErrorMessage ?? 'No se pudo cargar el estado de cuenta del cliente.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsLoadingReceivables(false);
    }
  };

  const openPaymentModal = (receivable: AccountReceivableItem) => {
    setPaymentModal({
      accountReceivableId: receivable.id,
      amount: receivable.balance.toFixed(2),
      paymentMethod: 'Cash',
      note: '',
    });
  };

  const handleRegisterReceivablePayment = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!selectedCustomerForReceivables || !paymentModal) {
      return;
    }

    const amount = Number.parseFloat(paymentModal.amount);
    if (!Number.isFinite(amount) || amount <= 0) {
      setNotification({ message: 'El monto del abono debe ser mayor a cero.', isError: true });
      setTimeout(() => setNotification(null), 4000);
      return;
    }

    setIsRegisteringPayment(true);
    try {
      await apiClient.post(`/customers/${selectedCustomerForReceivables.id}/payments`, {
        accountReceivableId: paymentModal.accountReceivableId,
        amount,
        paymentMethod: paymentModal.paymentMethod,
        note: paymentModal.note || undefined,
      });

      setNotification({ message: 'Abono registrado correctamente.', isError: false });
      setPaymentModal(null);
      await loadReceivables(selectedCustomerForReceivables);
      setTimeout(() => setNotification(null), 4000);
    } catch (error) {
      const apiErrorMessage = isAxiosError(error) ? error.response?.data?.error?.message : undefined;
      setNotification({ message: apiErrorMessage ?? 'No se pudo registrar el abono.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsRegisteringPayment(false);
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
                      <button
                        type="button"
                        onClick={() => loadReceivables(customer)}
                        className="rounded-md border border-blue-200 px-2.5 py-1 text-xs text-blue-700 hover:bg-blue-50"
                      >
                        Estado de Cuenta
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

        {selectedCustomerForReceivables && (
          <section className="mt-4 rounded-xl border border-gray-200 bg-white p-4">
            <div className="mb-3 flex items-center justify-between">
              <div>
                <h3 className="text-base font-semibold text-gray-900">Cuentas por Cobrar</h3>
                <p className="text-sm text-gray-500">Cliente: {selectedCustomerForReceivables.name}</p>
              </div>
              <button
                type="button"
                onClick={() => {
                  setSelectedCustomerForReceivables(null);
                  setReceivables([]);
                  setPaymentHistory([]);
                }}
                className="rounded-md border border-gray-300 px-3 py-1.5 text-xs text-gray-700 hover:bg-gray-50"
              >
                Cerrar vista
              </button>
            </div>

            <div className="overflow-x-auto rounded-lg border border-gray-200">
              <table className="min-w-full divide-y divide-gray-200 text-sm">
                <thead className="bg-gray-50 text-xs uppercase text-gray-600">
                  <tr>
                    <th className="px-4 py-3 text-left">Venta</th>
                    <th className="px-4 py-3 text-right">Total</th>
                    <th className="px-4 py-3 text-right">Pagado</th>
                    <th className="px-4 py-3 text-right">Saldo</th>
                    <th className="px-4 py-3 text-left">Vence</th>
                    <th className="px-4 py-3 text-center">Estado</th>
                    <th className="px-4 py-3 text-right">Acciones</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 bg-white">
                  {isLoadingReceivables && (
                    <tr>
                      <td colSpan={7} className="px-4 py-6 text-center text-gray-500">Cargando estado de cuenta...</td>
                    </tr>
                  )}

                  {!isLoadingReceivables && receivables.length === 0 && (
                    <tr>
                      <td colSpan={7} className="px-4 py-6 text-center text-gray-500">No hay deudas pendientes para este cliente.</td>
                    </tr>
                  )}

                  {!isLoadingReceivables && receivables.map((receivable) => (
                    <tr key={receivable.id}>
                      <td className="px-4 py-3 font-medium text-gray-900">{receivable.saleId.slice(0, 8)}</td>
                      <td className="px-4 py-3 text-right">{receivable.totalAmount.toLocaleString('es-PE', { style: 'currency', currency: 'PEN' })}</td>
                      <td className="px-4 py-3 text-right">{receivable.paidAmount.toLocaleString('es-PE', { style: 'currency', currency: 'PEN' })}</td>
                      <td className="px-4 py-3 text-right font-semibold text-red-700">{receivable.balance.toLocaleString('es-PE', { style: 'currency', currency: 'PEN' })}</td>
                      <td className="px-4 py-3">{new Date(receivable.dueDate).toLocaleDateString('es-CO')}</td>
                      <td className="px-4 py-3 text-center">
                        <span className={`rounded-full px-2 py-1 text-xs font-medium ${receivable.status === 'Paid' ? 'bg-green-50 text-green-700' : receivable.status === 'Partial' ? 'bg-amber-100 text-amber-700' : 'bg-red-50 text-red-700'}`}>
                          {receivable.status}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right">
                        {receivable.status !== 'Paid' && (
                          <button
                            type="button"
                            onClick={() => openPaymentModal(receivable)}
                            className="rounded-md border border-blue-200 px-2.5 py-1 text-xs text-blue-700 hover:bg-blue-50"
                          >
                            Registrar Abono
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="mt-4 rounded-lg border border-gray-200">
              <div className="border-b border-gray-200 bg-gray-50 px-4 py-3">
                <h4 className="text-sm font-semibold text-gray-900">Historial de Abonos</h4>
                <p className="text-xs text-gray-500">Movimientos registrados para las cuentas por cobrar del cliente.</p>
              </div>

              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-gray-200 text-sm">
                  <thead className="bg-white text-xs uppercase text-gray-600">
                    <tr>
                      <th className="px-4 py-3 text-left">Fecha</th>
                      <th className="px-4 py-3 text-left">Venta</th>
                      <th className="px-4 py-3 text-left">Cuenta</th>
                      <th className="px-4 py-3 text-right">Monto</th>
                      <th className="px-4 py-3 text-left">Nota</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100 bg-white">
                    {isLoadingPaymentHistory && (
                      <tr>
                        <td colSpan={5} className="px-4 py-6 text-center text-gray-500">Cargando historial de abonos...</td>
                      </tr>
                    )}

                    {!isLoadingPaymentHistory && paymentHistory.length === 0 && (
                      <tr>
                        <td colSpan={5} className="px-4 py-6 text-center text-gray-500">Este cliente aún no tiene abonos registrados.</td>
                      </tr>
                    )}

                    {!isLoadingPaymentHistory && paymentHistory.map((entry) => (
                      <tr key={entry.cashMovementId}>
                        <td className="px-4 py-3 text-gray-700">{new Date(entry.registeredAt).toLocaleString('es-CO')}</td>
                        <td className="px-4 py-3 font-medium text-gray-900">{entry.saleId.slice(0, 8)}</td>
                        <td className="px-4 py-3 text-gray-700">{entry.accountReceivableId.slice(0, 8)}</td>
                        <td className="px-4 py-3 text-right font-semibold text-emerald-700">{entry.amount.toLocaleString('es-PE', { style: 'currency', currency: 'PEN' })}</td>
                        <td className="px-4 py-3 text-gray-700">{entry.note?.trim() || '-'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </section>
        )}
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

      {paymentModal && selectedCustomerForReceivables && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="w-full max-w-md rounded-xl bg-white p-6 shadow-lg">
            <h3 className="text-lg font-semibold text-gray-900">Registrar Abono</h3>
            <p className="mt-1 text-sm text-gray-500">Cliente: {selectedCustomerForReceivables.name}</p>

            <form className="mt-4 space-y-3" onSubmit={handleRegisterReceivablePayment}>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Monto</label>
                <input
                  type="number"
                  min="0.01"
                  step="0.01"
                  value={paymentModal.amount}
                  onChange={(e) => setPaymentModal((prev) => prev ? { ...prev, amount: e.target.value } : prev)}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  required
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Método de pago</label>
                <select
                  value={paymentModal.paymentMethod}
                  onChange={(e) => setPaymentModal((prev) => prev ? { ...prev, paymentMethod: e.target.value as ReceivablePaymentForm['paymentMethod'] } : prev)}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                >
                  <option value="Cash">Efectivo</option>
                  <option value="CreditCard">Tarjeta Crédito</option>
                  <option value="DebitCard">Tarjeta Débito</option>
                  <option value="Transfer">Transferencia</option>
                  <option value="Other">Otro</option>
                </select>
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Nota (opcional)</label>
                <input
                  value={paymentModal.note}
                  onChange={(e) => setPaymentModal((prev) => prev ? { ...prev, note: e.target.value } : prev)}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  placeholder="Ej. Abono cuota marzo"
                />
              </div>

              <div className="mt-2 flex gap-2">
                <button
                  type="button"
                  onClick={() => setPaymentModal(null)}
                  className="flex-1 rounded-lg border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  disabled={isRegisteringPayment}
                  className="flex-1 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
                >
                  {isRegisteringPayment ? 'Registrando...' : 'Registrar'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
