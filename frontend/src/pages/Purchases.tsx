import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2 } from 'lucide-react';
import apiClient from '../api/apiClient';
import { useAuthStore } from '../store/useAuthStore';
import { useCompanySettings } from '../hooks/useCompanySettings';
import { formatCurrency } from '../utils/currency';

type SupplierItem = {
  id: string;
  name: string;
  contactName?: string;
  phone?: string;
  email?: string;
  address?: string;
};

type SuppliersResponse = {
  success: boolean;
  data: SupplierItem[];
};

type SupplierForm = {
  id?: string;
  name: string;
  contactName: string;
  phone: string;
  email: string;
  address: string;
};

type InventoryProduct = {
  id: string;
  name: string;
  sku: string;
  price: number;
};

type InventoryStockResponse = {
  success: boolean;
  data: {
    products: InventoryProduct[];
  };
};

type PurchaseCartItem = {
  productId: string;
  name: string;
  sku: string;
  quantity: number;
  unitCost: number;
};

const defaultSupplierForm: SupplierForm = {
  name: '',
  contactName: '',
  phone: '',
  email: '',
  address: '',
};

export const Purchases = () => {
  const queryClient = useQueryClient();
  const branches = useAuthStore((state) => state.branches);
  const currentBranchId = useAuthStore((state) => state.currentBranchId);
  const [activeTab, setActiveTab] = useState<'new' | 'suppliers'>('new');
  const [notification, setNotification] = useState<{ text: string; isError: boolean } | null>(null);

  const [supplierModalOpen, setSupplierModalOpen] = useState(false);
  const [supplierForm, setSupplierForm] = useState<SupplierForm>(defaultSupplierForm);

  const [selectedBranchId, setSelectedBranchId] = useState(currentBranchId ?? branches[0]?.id ?? '');
  const [selectedSupplierId, setSelectedSupplierId] = useState('');
  const [purchaseDate, setPurchaseDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [referenceNumber, setReferenceNumber] = useState('');
  const [productSearch, setProductSearch] = useState('');
  const [cartItems, setCartItems] = useState<PurchaseCartItem[]>([]);

  const companySettingsQuery = useCompanySettings();
  const currencySymbol = companySettingsQuery.data?.currencySymbol ?? '$';

  const suppliersQuery = useQuery({
    queryKey: ['inventory-suppliers'],
    queryFn: async () => {
      const response = await apiClient.get<SuppliersResponse>('/inventory/suppliers');
      return response.data.data;
    },
  });

  const productsQuery = useQuery({
    queryKey: ['inventory-products-for-purchases', currentBranchId],
    queryFn: async () => {
      const response = await apiClient.get<InventoryStockResponse>('/inventory/stock');
      return response.data.data.products;
    },
    enabled: !!currentBranchId,
  });

  const saveSupplierMutation = useMutation({
    mutationFn: async (payload: SupplierForm) => {
      const requestBody = {
        name: payload.name,
        contactName: payload.contactName || undefined,
        phone: payload.phone || undefined,
        email: payload.email || undefined,
        address: payload.address || undefined,
      };

      if (payload.id) {
        await apiClient.put(`/inventory/suppliers/${payload.id}`, requestBody);
        return;
      }

      await apiClient.post('/inventory/suppliers', requestBody);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['inventory-suppliers'] });
      setNotification({ text: 'Proveedor guardado correctamente.', isError: false });
      setSupplierModalOpen(false);
      setSupplierForm(defaultSupplierForm);
    },
    onError: () => {
      setNotification({ text: 'No se pudo guardar el proveedor.', isError: true });
    },
  });

  const deleteSupplierMutation = useMutation({
    mutationFn: async (supplierId: string) => {
      await apiClient.delete(`/inventory/suppliers/${supplierId}`);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['inventory-suppliers'] });
      setNotification({ text: 'Proveedor eliminado.', isError: false });
    },
    onError: () => {
      setNotification({ text: 'No se pudo eliminar el proveedor.', isError: true });
    },
  });

  const createPurchaseMutation = useMutation({
    mutationFn: async () => {
      await apiClient.post('/inventory/purchases', {
        branchId: selectedBranchId,
        supplierId: selectedSupplierId,
        purchaseDate: purchaseDate ? new Date(`${purchaseDate}T00:00:00`).toISOString() : undefined,
        referenceNumber: referenceNumber || undefined,
        items: cartItems.map((item) => ({
          productId: item.productId,
          quantity: item.quantity,
          unitCost: item.unitCost,
        })),
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['inventory-stock', currentBranchId] });
      await queryClient.invalidateQueries({ queryKey: ['inventory-movements'] });
      setNotification({ text: 'Compra procesada correctamente.', isError: false });
      setCartItems([]);
      setReferenceNumber('');
      setPurchaseDate(new Date().toISOString().slice(0, 10));
    },
    onError: () => {
      setNotification({ text: 'No se pudo procesar la compra.', isError: true });
    },
  });

  const filteredProducts = useMemo(() => {
    const source = productsQuery.data ?? [];
    const normalizedSearch = productSearch.trim().toLowerCase();

    if (!normalizedSearch) {
      return source;
    }

    return source.filter((p) => p.name.toLowerCase().includes(normalizedSearch) || p.sku.toLowerCase().includes(normalizedSearch));
  }, [productsQuery.data, productSearch]);

  const purchaseTotal = useMemo(
    () => cartItems.reduce((acc, item) => acc + (item.quantity * item.unitCost), 0),
    [cartItems],
  );

  const addProductToCart = (product: InventoryProduct) => {
    setCartItems((prev) => {
      const existing = prev.find((item) => item.productId === product.id);
      if (existing) {
        return prev.map((item) =>
          item.productId === product.id
            ? { ...item, quantity: item.quantity + 1 }
            : item,
        );
      }

      return [
        ...prev,
        {
          productId: product.id,
          name: product.name,
          sku: product.sku,
          quantity: 1,
          unitCost: product.price,
        },
      ];
    });
  };

  const updateCartItem = (productId: string, patch: Partial<Pick<PurchaseCartItem, 'quantity' | 'unitCost'>>) => {
    setCartItems((prev) => prev.map((item) => {
      if (item.productId !== productId) {
        return item;
      }

      const nextQuantity = patch.quantity ?? item.quantity;
      const nextUnitCost = patch.unitCost ?? item.unitCost;

      return {
        ...item,
        quantity: Number.isFinite(nextQuantity) ? Math.max(0.01, nextQuantity) : item.quantity,
        unitCost: Number.isFinite(nextUnitCost) ? Math.max(0, nextUnitCost) : item.unitCost,
      };
    }));
  };

  const removeCartItem = (productId: string) => {
    setCartItems((prev) => prev.filter((item) => item.productId !== productId));
  };

  const handleSubmitSupplier = async (event: React.FormEvent) => {
    event.preventDefault();
    await saveSupplierMutation.mutateAsync(supplierForm);
  };

  const handleSubmitPurchase = async (event: React.FormEvent) => {
    event.preventDefault();

    if (!selectedBranchId) {
      setNotification({ text: 'Debes seleccionar la sucursal destino.', isError: true });
      return;
    }

    if (!selectedSupplierId) {
      setNotification({ text: 'Debes seleccionar un proveedor.', isError: true });
      return;
    }

    if (cartItems.length === 0) {
      setNotification({ text: 'Debes agregar al menos un producto.', isError: true });
      return;
    }

    await createPurchaseMutation.mutateAsync();
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Compras</h1>
          <p className="mt-1 text-sm text-gray-500">Gestion de proveedores e ingreso formal de mercancia.</p>
        </div>

        <div className="flex gap-2">
          <button
            type="button"
            className={`rounded-lg px-3 py-2 text-sm font-medium ${activeTab === 'new' ? 'bg-blue-600 text-white' : 'border border-gray-300 bg-white text-gray-700 hover:bg-gray-100'}`}
            onClick={() => setActiveTab('new')}
          >
            Nueva Compra
          </button>
          <button
            type="button"
            className={`rounded-lg px-3 py-2 text-sm font-medium ${activeTab === 'suppliers' ? 'bg-blue-600 text-white' : 'border border-gray-300 bg-white text-gray-700 hover:bg-gray-100'}`}
            onClick={() => setActiveTab('suppliers')}
          >
            Proveedores
          </button>
        </div>
      </div>

      {notification && (
        <div className={`rounded-lg px-4 py-3 text-sm ${notification.isError ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-700'}`}>
          {notification.text}
        </div>
      )}

      {activeTab === 'suppliers' && (
        <section className="space-y-4 rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold text-gray-900">Directorio de Proveedores</h2>
            <button
              type="button"
              onClick={() => {
                setSupplierForm(defaultSupplierForm);
                setSupplierModalOpen(true);
              }}
              className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700"
            >
              <Plus size={14} /> Nuevo Proveedor
            </button>
          </div>

          <div className="overflow-x-auto rounded-lg border border-gray-200">
            <table className="min-w-full divide-y divide-gray-200 text-sm">
              <thead className="bg-gray-50 text-xs uppercase text-gray-600">
                <tr>
                  <th className="px-4 py-3 text-left">Nombre</th>
                  <th className="px-4 py-3 text-left">Contacto</th>
                  <th className="px-4 py-3 text-left">Telefono</th>
                  <th className="px-4 py-3 text-left">Email</th>
                  <th className="px-4 py-3 text-left">Direccion</th>
                  <th className="px-4 py-3 text-right">Acciones</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 bg-white">
                {(suppliersQuery.data ?? []).map((supplier) => (
                  <tr key={supplier.id}>
                    <td className="px-4 py-3 font-medium text-gray-900">{supplier.name}</td>
                    <td className="px-4 py-3 text-gray-700">{supplier.contactName || '-'}</td>
                    <td className="px-4 py-3 text-gray-700">{supplier.phone || '-'}</td>
                    <td className="px-4 py-3 text-gray-700">{supplier.email || '-'}</td>
                    <td className="px-4 py-3 text-gray-700">{supplier.address || '-'}</td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => {
                            setSupplierForm({
                              id: supplier.id,
                              name: supplier.name,
                              contactName: supplier.contactName ?? '',
                              phone: supplier.phone ?? '',
                              email: supplier.email ?? '',
                              address: supplier.address ?? '',
                            });
                            setSupplierModalOpen(true);
                          }}
                          className="rounded-md border border-gray-300 px-2.5 py-1 text-xs text-gray-700 hover:bg-gray-100"
                        >
                          Editar
                        </button>
                        <button
                          type="button"
                          onClick={() => deleteSupplierMutation.mutate(supplier.id)}
                          className="rounded-md border border-red-200 px-2.5 py-1 text-xs text-red-700 hover:bg-red-50"
                        >
                          Eliminar
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {activeTab === 'new' && (
        <div className="grid gap-6 xl:grid-cols-3">
          <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm xl:col-span-2">
            <h2 className="mb-4 text-lg font-semibold text-gray-900">Ingreso de Mercancia</h2>

            <form className="space-y-4" onSubmit={handleSubmitPurchase}>
              <div className="grid gap-3 md:grid-cols-2">
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Sucursal destino</label>
                  <select
                    value={selectedBranchId}
                    onChange={(e) => setSelectedBranchId(e.target.value)}
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                  >
                    <option value="">Selecciona una sucursal</option>
                    {branches.map((branch) => (
                      <option key={branch.id} value={branch.id}>{branch.name}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Proveedor</label>
                  <select
                    value={selectedSupplierId}
                    onChange={(e) => setSelectedSupplierId(e.target.value)}
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                  >
                    <option value="">Selecciona un proveedor</option>
                    {(suppliersQuery.data ?? []).map((supplier) => (
                      <option key={supplier.id} value={supplier.id}>{supplier.name}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Fecha de compra</label>
                  <input
                    type="date"
                    value={purchaseDate}
                    onChange={(e) => setPurchaseDate(e.target.value)}
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                  />
                </div>

                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Factura / Referencia</label>
                  <input
                    value={referenceNumber}
                    onChange={(e) => setReferenceNumber(e.target.value)}
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                    placeholder="Ej. F001-000123"
                  />
                </div>
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Buscar producto</label>
                <input
                  value={productSearch}
                  onChange={(e) => setProductSearch(e.target.value)}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                  placeholder="Buscar por nombre o SKU"
                />

                <div className="mt-2 max-h-52 overflow-auto rounded-lg border border-gray-200">
                  {(filteredProducts ?? []).slice(0, 15).map((product) => (
                    <button
                      key={product.id}
                      type="button"
                      onClick={() => addProductToCart(product)}
                      className="flex w-full items-center justify-between border-b border-gray-100 px-3 py-2 text-left text-sm hover:bg-gray-50"
                    >
                      <span>{product.name}</span>
                      <span className="text-xs text-gray-500">{product.sku}</span>
                    </button>
                  ))}
                </div>
              </div>

              <div className="overflow-x-auto rounded-lg border border-gray-200">
                <table className="min-w-full divide-y divide-gray-200 text-sm">
                  <thead className="bg-gray-50 text-xs uppercase text-gray-600">
                    <tr>
                      <th className="px-4 py-3 text-left">Producto</th>
                      <th className="px-4 py-3 text-right">Cantidad</th>
                      <th className="px-4 py-3 text-right">Costo Unitario</th>
                      <th className="px-4 py-3 text-right">Subtotal</th>
                      <th className="px-4 py-3 text-right">Accion</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100 bg-white">
                    {cartItems.length === 0 && (
                      <tr>
                        <td colSpan={5} className="px-4 py-6 text-center text-gray-500">No hay productos agregados.</td>
                      </tr>
                    )}

                    {cartItems.map((item) => (
                      <tr key={item.productId}>
                        <td className="px-4 py-3">
                          <p className="font-medium text-gray-900">{item.name}</p>
                          <p className="text-xs text-gray-500">{item.sku}</p>
                        </td>
                        <td className="px-4 py-3 text-right">
                          <input
                            type="number"
                            min="0.01"
                            step="0.01"
                            value={item.quantity}
                            onChange={(e) => updateCartItem(item.productId, { quantity: Number.parseFloat(e.target.value) })}
                            className="w-24 rounded border border-gray-300 px-2 py-1 text-right outline-none focus:border-blue-500"
                          />
                        </td>
                        <td className="px-4 py-3 text-right">
                          <input
                            type="number"
                            min="0"
                            step="0.01"
                            value={item.unitCost}
                            onChange={(e) => updateCartItem(item.productId, { unitCost: Number.parseFloat(e.target.value) })}
                            className="w-28 rounded border border-gray-300 px-2 py-1 text-right outline-none focus:border-blue-500"
                          />
                        </td>
                        <td className="px-4 py-3 text-right font-semibold text-gray-800">
                          {formatCurrency(item.quantity * item.unitCost, currencySymbol)}
                        </td>
                        <td className="px-4 py-3 text-right">
                          <button
                            type="button"
                            onClick={() => removeCartItem(item.productId)}
                            className="rounded-md border border-red-200 p-1.5 text-red-700 hover:bg-red-50"
                          >
                            <Trash2 size={14} />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="flex items-center justify-between rounded-lg border border-gray-200 bg-gray-50 px-4 py-3">
                <span className="text-sm text-gray-600">Total compra</span>
                <span className="text-lg font-bold text-gray-900">{formatCurrency(purchaseTotal, currencySymbol)}</span>
              </div>

              <button
                type="submit"
                disabled={createPurchaseMutation.isPending}
                className="w-full rounded-lg bg-emerald-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-emerald-700 disabled:opacity-60"
              >
                {createPurchaseMutation.isPending ? 'Procesando...' : 'Procesar Compra'}
              </button>
            </form>
          </section>

          <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
            <h3 className="text-base font-semibold text-gray-900">Resumen</h3>
            <div className="mt-3 space-y-2 text-sm text-gray-700">
              <p><span className="font-medium">Sucursal:</span> {branches.find((b) => b.id === selectedBranchId)?.name ?? '-'}</p>
              <p><span className="font-medium">Proveedor:</span> {(suppliersQuery.data ?? []).find((s) => s.id === selectedSupplierId)?.name ?? '-'}</p>
              <p><span className="font-medium">Items:</span> {cartItems.length}</p>
              <p><span className="font-medium">Factura:</span> {referenceNumber || '-'}</p>
              <p><span className="font-medium">Fecha:</span> {purchaseDate || '-'}</p>
            </div>
          </section>
        </div>
      )}

      {supplierModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="w-full max-w-xl rounded-xl bg-white p-6 shadow-lg">
            <h3 className="text-lg font-semibold text-gray-900">{supplierForm.id ? 'Editar Proveedor' : 'Nuevo Proveedor'}</h3>

            <form className="mt-4 grid gap-3 sm:grid-cols-2" onSubmit={handleSubmitSupplier}>
              <div className="sm:col-span-2">
                <label className="mb-1 block text-sm font-medium text-gray-700">Nombre</label>
                <input
                  value={supplierForm.name}
                  onChange={(e) => setSupplierForm((prev) => ({ ...prev, name: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  required
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Contacto</label>
                <input
                  value={supplierForm.contactName}
                  onChange={(e) => setSupplierForm((prev) => ({ ...prev, contactName: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Telefono</label>
                <input
                  value={supplierForm.phone}
                  onChange={(e) => setSupplierForm((prev) => ({ ...prev, phone: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Email</label>
                <input
                  type="email"
                  value={supplierForm.email}
                  onChange={(e) => setSupplierForm((prev) => ({ ...prev, email: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Direccion</label>
                <input
                  value={supplierForm.address}
                  onChange={(e) => setSupplierForm((prev) => ({ ...prev, address: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                />
              </div>

              <div className="sm:col-span-2 mt-2 flex gap-2">
                <button
                  type="button"
                  onClick={() => {
                    setSupplierModalOpen(false);
                    setSupplierForm(defaultSupplierForm);
                  }}
                  className="flex-1 rounded-lg border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  disabled={saveSupplierMutation.isPending}
                  className="flex-1 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
                >
                  {saveSupplierMutation.isPending ? 'Guardando...' : (supplierForm.id ? 'Guardar cambios' : 'Crear proveedor')}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
