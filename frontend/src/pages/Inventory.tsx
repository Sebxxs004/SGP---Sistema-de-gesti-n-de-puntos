import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { PackageSearch, Boxes, AlertCircle, Plus, X } from 'lucide-react';
import apiClient from '../api/apiClient';
import { useAuthStore } from '../store/useAuthStore';
import { useCompanySettings } from '../hooks/useCompanySettings';
import { formatCurrency } from '../utils/currency';

type InventoryProduct = {
  id: string;
  name: string;
  sku: string;
  categoryId: string;
  category: string;
  price: number;
  isActive: boolean;
  stock: number;
};

type InventoryStockResponse = {
  success: boolean;
  data: {
    branchId: string;
    products: InventoryProduct[];
  };
};

type AdjustStockPayload = {
  branchId: string;
  productId: string;
  quantityDelta: number;
  reason: string;
};

type AdjustModalState = {
  productId: string;
  productName: string;
  quantityDelta: number;
  reason: string;
};

type MovementItem = {
  id: string;
  branchId: string;
  productId: string;
  productName: string;
  userId: string;
  userName: string;
  movementType: string;
  quantity: number;
  reason?: string;
  createdAt: string;
};

type InventoryMovementsResponse = {
  success: boolean;
  data: MovementItem[];
};

type MovementFilters = {
  branchId: string;
  productId: string;
  userId: string;
  reason: string;
  from: string;
  to: string;
};

type KardexFilters = {
  branchId: string;
  productId: string;
  from: string;
  to: string;
};

type KardexRow = {
  id: string;
  createdAt: string;
  movementType: string;
  quantity: number;
  reference?: string;
  entries: number;
  exits: number;
  balance: number;
};

type KardexResponse = {
  success: boolean;
  data: {
    branchId: string;
    branchName: string;
    productId: string;
    productName: string;
    from?: string;
    to?: string;
    openingBalance: number;
    rows: KardexRow[];
  };
};

type CategoryItem = {
  id: string;
  name: string;
  description?: string;
  isActive: boolean;
};

type CategoriesResponse = {
  success: boolean;
  data: CategoryItem[];
};

type CategoryForm = {
  id?: string;
  name: string;
  description: string;
};

type ProductForm = {
  id?: string;
  name: string;
  sku: string;
  categoryId: string;
  basePrice: string;
  initialStock: string;
  barcode: string;
};

const defaultCategoryForm: CategoryForm = {
  name: '',
  description: '',
};

const defaultProductForm: ProductForm = {
  name: '',
  sku: '',
  categoryId: '',
  basePrice: '',
  initialStock: '0',
  barcode: '',
};

const defaultMovementFilters: MovementFilters = {
  branchId: '',
  productId: '',
  userId: '',
  reason: '',
  from: '',
  to: '',
};

const defaultKardexFilters: KardexFilters = {
  branchId: '',
  productId: '',
  from: '',
  to: '',
};

export const Inventory = () => {
  const queryClient = useQueryClient();
  const currentBranchId = useAuthStore((state) => state.currentBranchId);
  const isAdmin = useAuthStore((state) => state.user?.role === 'Admin');
  const branches = useAuthStore((state) => state.branches);
  const companySettingsQuery = useCompanySettings();
  const currencySymbol = companySettingsQuery.data?.currencySymbol ?? '$';
  const [searchTerm, setSearchTerm] = useState('');
  const [notification, setNotification] = useState<{ text: string; isError: boolean } | null>(null);
  const [adjustModal, setAdjustModal] = useState<AdjustModalState | null>(null);
  const [activeView, setActiveView] = useState<'stock' | 'movements' | 'kardex' | 'categories'>('stock');
  const [movementFilters, setMovementFilters] = useState<MovementFilters>(defaultMovementFilters);
  const [kardexFilters, setKardexFilters] = useState<KardexFilters>(defaultKardexFilters);
  const [kardexRequest, setKardexRequest] = useState<KardexFilters | null>(null);
  const [categoryForm, setCategoryForm] = useState<CategoryForm>(defaultCategoryForm);
  const [productForm, setProductForm] = useState<ProductForm>(defaultProductForm);
  const [showProductModal, setShowProductModal] = useState(false);
  const [stockPage, setStockPage] = useState(1);

  const stockQuery = useQuery({
    queryKey: ['inventory-stock', currentBranchId],
    queryFn: async () => {
      const response = await apiClient.get<InventoryStockResponse>('/inventory/stock');
      return response.data.data.products;
    },
    enabled: !!currentBranchId,
  });

  const adjustStockMutation = useMutation({
    mutationFn: async (payload: AdjustStockPayload) => {
      await apiClient.post('/inventory/stock/adjust', payload);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['inventory-stock', currentBranchId] });
      await queryClient.invalidateQueries({ queryKey: ['inventory-movements'] });
      setNotification({ text: 'Ajuste de inventario aplicado.', isError: false });
      setAdjustModal(null);
    },
    onError: () => {
      setNotification({ text: 'No se pudo aplicar el ajuste de inventario.', isError: true });
    },
  });

  const products = useMemo(() => stockQuery.data ?? [], [stockQuery.data]);

  const movementsQuery = useQuery({
    queryKey: ['inventory-movements', currentBranchId, movementFilters],
    queryFn: async () => {
      const params = new URLSearchParams();

      const branchFilter = isAdmin
        ? (movementFilters.branchId || currentBranchId || '')
        : (currentBranchId || '');

      if (branchFilter) {
        params.set('branchId', branchFilter);
      }

      if (movementFilters.productId) {
        params.set('productId', movementFilters.productId);
      }

      if (movementFilters.userId) {
        params.set('userId', movementFilters.userId);
      }

      if (movementFilters.reason.trim()) {
        params.set('reason', movementFilters.reason.trim());
      }

      if (movementFilters.from) {
        params.set('from', movementFilters.from);
      }

      if (movementFilters.to) {
        params.set('to', movementFilters.to);
      }

      const response = await apiClient.get<InventoryMovementsResponse>(`/inventory/movements?${params.toString()}`);
      return response.data.data;
    },
    enabled: !!currentBranchId,
  });

  const categoriesQuery = useQuery({
    queryKey: ['inventory-categories'],
    queryFn: async () => {
      const response = await apiClient.get<CategoriesResponse>('/inventory/categories');
      return response.data.data;
    },
    enabled: isAdmin,
  });

  const kardexQuery = useQuery({
    queryKey: ['inventory-kardex', kardexRequest],
    queryFn: async () => {
      if (!kardexRequest) {
        return null;
      }

      const params = new URLSearchParams();
      params.set('branchId', kardexRequest.branchId);
      params.set('productId', kardexRequest.productId);

      if (kardexRequest.from) {
        params.set('from', new Date(`${kardexRequest.from}T00:00:00`).toISOString());
      }

      if (kardexRequest.to) {
        params.set('to', new Date(`${kardexRequest.to}T23:59:59`).toISOString());
      }

      const response = await apiClient.get<KardexResponse>(`/inventory/reports/kardex?${params.toString()}`);
      return response.data.data;
    },
    enabled: !!kardexRequest,
  });

  const saveCategoryMutation = useMutation({
    mutationFn: async (payload: CategoryForm) => {
      if (payload.id) {
        await apiClient.put(`/inventory/categories/${payload.id}`, {
          name: payload.name,
          description: payload.description,
        });
        return;
      }

      await apiClient.post('/inventory/categories', {
        name: payload.name,
        description: payload.description,
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['inventory-categories'] });
      setCategoryForm(defaultCategoryForm);
      setNotification({ text: 'Categoria guardada correctamente.', isError: false });
    },
    onError: () => {
      setNotification({ text: 'No se pudo guardar la categoria.', isError: true });
    },
  });

  const deleteCategoryMutation = useMutation({
    mutationFn: async (categoryId: string) => {
      await apiClient.delete(`/inventory/categories/${categoryId}`);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['inventory-categories'] });
      setNotification({ text: 'Categoria eliminada/desactivada.', isError: false });
    },
    onError: () => {
      setNotification({ text: 'No se pudo eliminar la categoria.', isError: true });
    },
  });

  const saveProductMutation = useMutation({
    mutationFn: async (payload: ProductForm) => {
      const requestBody = {
        name: payload.name,
        sku: payload.sku,
        categoryId: payload.categoryId,
        basePrice: Number(payload.basePrice),
        initialStock: Number(payload.initialStock),
        barcode: payload.barcode || null,
      };

      if (payload.id) {
        await apiClient.put(`/inventory/products/${payload.id}`, requestBody);
        return;
      }

      await apiClient.post('/inventory/products', requestBody);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['inventory-stock', currentBranchId] });
      setNotification({ text: 'Producto guardado correctamente.', isError: false });
      setProductForm(defaultProductForm);
      setShowProductModal(false);
    },
    onError: () => {
      setNotification({ text: 'No se pudo guardar el producto.', isError: true });
    },
  });

  const deleteProductMutation = useMutation({
    mutationFn: async (productId: string) => {
      await apiClient.delete(`/inventory/products/${productId}`);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['inventory-stock', currentBranchId] });
      setNotification({ text: 'Producto eliminado/desactivado.', isError: false });
    },
    onError: () => {
      setNotification({ text: 'No se pudo eliminar el producto.', isError: true });
    },
  });

  const filteredProducts = products.filter(p => 
    p.name.toLowerCase().includes(searchTerm.toLowerCase()) || 
    p.sku.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const pageSize = 10;
  const totalStockPages = Math.max(1, Math.ceil(filteredProducts.length / pageSize));
  const pagedProducts = filteredProducts.slice((stockPage - 1) * pageSize, stockPage * pageSize);

  const movementUsers = useMemo(() => {
    const users = new Map<string, string>();

    for (const movement of movementsQuery.data ?? []) {
      users.set(movement.userId, movement.userName);
    }

    return Array.from(users.entries())
      .map(([id, name]) => ({ id, name }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [movementsQuery.data]);

  const openAdjustModal = (product: InventoryProduct) => {
    if (!isAdmin) {
      return;
    }

    setAdjustModal({
      productId: product.id,
      productName: product.name,
      quantityDelta: 1,
      reason: 'Error de conteo',
    });
  };

  const submitAdjustStock = async (event: React.FormEvent) => {
    event.preventDefault();

    if (!adjustModal || !currentBranchId) {
      return;
    }

    await adjustStockMutation.mutateAsync({
      branchId: currentBranchId,
      productId: adjustModal.productId,
      quantityDelta: adjustModal.quantityDelta,
      reason: adjustModal.reason,
    });
  };

  const getMovementTypeLabel = (movementType: string) => {
    switch (movementType.toLowerCase()) {
      case 'sale':
        return 'Venta';
      case 'adjustment':
        return 'Ajuste';
      case 'in':
        return 'Entrada';
      case 'out':
        return 'Salida';
      case 'transfer':
        return 'Transferencia';
      default:
        return movementType;
    }
  };

  const getBranchName = (branchId: string) => branches.find((branch) => branch.id === branchId)?.name ?? 'Sucursal';

  const submitCategory = async (event: React.FormEvent) => {
    event.preventDefault();
    await saveCategoryMutation.mutateAsync(categoryForm);
  };

  const openCreateProductModal = () => {
    setProductForm({
      ...defaultProductForm,
      categoryId: (categoriesQuery.data ?? []).find((c) => c.isActive)?.id ?? '',
    });
    setShowProductModal(true);
  };

  const openEditProductModal = (product: InventoryProduct) => {
    setProductForm({
      id: product.id,
      name: product.name,
      sku: product.sku,
      categoryId: product.categoryId,
      basePrice: String(product.price),
      initialStock: String(product.stock),
      barcode: '',
    });
    setShowProductModal(true);
  };

  const submitProduct = async (event: React.FormEvent) => {
    event.preventDefault();
    await saveProductMutation.mutateAsync(productForm);
  };

  const handleGenerateKardexReport = () => {
    const branchId = isAdmin ? kardexFilters.branchId : (currentBranchId ?? '');

    if (!branchId) {
      setNotification({ text: 'Debes seleccionar una sucursal para generar el kardex.', isError: true });
      return;
    }

    if (!kardexFilters.productId) {
      setNotification({ text: 'Debes seleccionar un producto para generar el kardex.', isError: true });
      return;
    }

    setNotification(null);
    setKardexRequest({
      ...kardexFilters,
      branchId,
    });
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Inventario</h1>
          <p className="mt-1 text-sm text-gray-500">
            {isAdmin ? 'Gestion de stock y auditoria de movimientos' : 'Inventario en modo solo lectura'}
          </p>
        </div>

        <div className="flex gap-2">
          <button
            type="button"
            className={`rounded-lg px-3 py-2 text-sm font-medium ${activeView === 'stock' ? 'bg-blue-600 text-white' : 'border border-gray-300 bg-white text-gray-700 hover:bg-gray-100'}`}
            onClick={() => setActiveView('stock')}
          >
            Stock Actual
          </button>
          <button
            type="button"
            className={`rounded-lg px-3 py-2 text-sm font-medium ${activeView === 'movements' ? 'bg-blue-600 text-white' : 'border border-gray-300 bg-white text-gray-700 hover:bg-gray-100'}`}
            onClick={() => setActiveView('movements')}
          >
            Historial de Movimientos
          </button>
          <button
            type="button"
            className={`rounded-lg px-3 py-2 text-sm font-medium ${activeView === 'kardex' ? 'bg-blue-600 text-white' : 'border border-gray-300 bg-white text-gray-700 hover:bg-gray-100'}`}
            onClick={() => setActiveView('kardex')}
          >
            Kardex
          </button>
          {isAdmin && (
            <button
              type="button"
              className={`rounded-lg px-3 py-2 text-sm font-medium ${activeView === 'categories' ? 'bg-blue-600 text-white' : 'border border-gray-300 bg-white text-gray-700 hover:bg-gray-100'}`}
              onClick={() => setActiveView('categories')}
            >
              Categorias
            </button>
          )}
        </div>
      </div>

      {notification && (
        <div className={`rounded-lg px-4 py-3 text-sm ${notification.isError ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-700'}`}>
          {notification.text}
        </div>
      )}

      {activeView === 'stock' && (
        <>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="relative w-full sm:w-72">
              <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
                <PackageSearch size={20} className="text-gray-400" />
              </div>
              <input
                type="text"
                className="w-full rounded-lg border border-gray-300 py-2 pl-10 pr-3 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                placeholder="Buscar por nombre o SKU..."
                value={searchTerm}
                onChange={(e) => {
                  setSearchTerm(e.target.value);
                  setStockPage(1);
                }}
              />
            </div>

            {isAdmin && (
              <button
                type="button"
                className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
                onClick={openCreateProductModal}
              >
                <Plus size={14} /> Nuevo Producto
              </button>
            )}
          </div>

          <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
            <div className="overflow-x-auto">
              <table className="min-w-[920px] w-full text-left text-sm text-gray-600">
                <thead className="bg-gray-50 text-xs uppercase text-gray-700">
                  <tr>
                    <th scope="col" className="px-6 py-4 font-semibold">Producto</th>
                    <th scope="col" className="px-6 py-4 font-semibold">SKU</th>
                    <th scope="col" className="px-6 py-4 font-semibold">Categoria</th>
                    <th scope="col" className="px-6 py-4 font-semibold text-right">Precio</th>
                    <th scope="col" className="px-6 py-4 font-semibold text-right">Stock</th>
                    <th scope="col" className="px-6 py-4 font-semibold text-center">Estado</th>
                    {isAdmin && <th scope="col" className="px-6 py-4 font-semibold text-center">Accion</th>}
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {stockQuery.isLoading && (
                    <tr>
                      <td colSpan={isAdmin ? 7 : 6} className="px-6 py-12 text-center text-gray-500">
                        Cargando inventario de la sucursal...
                      </td>
                    </tr>
                  )}

                  {!stockQuery.isLoading && pagedProducts.map((product) => (
                    <tr key={product.id} className="hover:bg-gray-50">
                      <td className="px-6 py-4 font-medium text-gray-900">{product.name}</td>
                      <td className="px-6 py-4">{product.sku}</td>
                      <td className="px-6 py-4">
                        <span className="inline-flex rounded-full bg-blue-50 px-2 py-1 text-xs font-semibold text-blue-700">
                          {product.category}
                        </span>
                      </td>
                      <td className="px-6 py-4 text-right">{formatCurrency(product.price, currencySymbol)}</td>
                      <td className="px-6 py-4 text-right font-medium">
                        {product.stock}
                      </td>
                      <td className="px-6 py-4 text-center">
                        {!product.isActive ? (
                          <span className="inline-flex items-center gap-1.5 rounded-full bg-gray-700 px-2.5 py-1 text-xs font-medium text-white">
                            Inactivo
                          </span>
                        ) : product.stock > 10 ? (
                          <span className="inline-flex items-center gap-1.5 rounded-full bg-green-50 px-2.5 py-1 text-xs font-medium text-green-700">
                            <Boxes size={14} /> Normal
                          </span>
                        ) : product.stock > 0 ? (
                          <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-50 px-2.5 py-1 text-xs font-medium text-amber-700">
                            <AlertCircle size={14} /> Bajo
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1.5 rounded-full bg-red-50 px-2.5 py-1 text-xs font-medium text-red-700">
                            <AlertCircle size={14} /> Agotado
                          </span>
                        )}
                      </td>
                      {isAdmin && (
                        <td className="px-6 py-4 text-center">
                          <div className="flex justify-center gap-2">
                            <button
                              type="button"
                              className="rounded-md border border-gray-300 px-2.5 py-1 text-xs text-gray-700 hover:bg-gray-100"
                              onClick={() => openEditProductModal(product)}
                            >
                              Editar
                            </button>
                            <button
                              type="button"
                              className="rounded-md border border-red-200 px-2.5 py-1 text-xs text-red-700 hover:bg-red-50"
                              onClick={() => deleteProductMutation.mutate(product.id)}
                            >
                              Eliminar
                            </button>
                            <button
                              type="button"
                              className="inline-flex items-center rounded-md border border-gray-300 p-2 text-gray-700 hover:bg-gray-100"
                              title="Ajustar stock"
                              onClick={() => openAdjustModal(product)}
                            >
                              <Plus size={14} />
                            </button>
                          </div>
                        </td>
                      )}
                    </tr>
                  ))}
                  {filteredProducts.length === 0 && (
                    <tr>
                      <td colSpan={isAdmin ? 7 : 6} className="px-6 py-12 text-center text-gray-500">
                        No se encontraron productos coincidentes.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <div className="flex items-center justify-between rounded-lg bg-white px-4 py-3 text-sm text-gray-600 shadow-sm">
            <span>Mostrando {pagedProducts.length} de {filteredProducts.length} productos</span>
            <div className="flex items-center gap-2">
              <button
                type="button"
                disabled={stockPage <= 1}
                onClick={() => setStockPage((prev) => Math.max(1, prev - 1))}
                className="rounded-md border border-gray-300 px-3 py-1.5 disabled:opacity-50"
              >
                Anterior
              </button>
              <span>Pagina {stockPage} de {totalStockPages}</span>
              <button
                type="button"
                disabled={stockPage >= totalStockPages}
                onClick={() => setStockPage((prev) => Math.min(totalStockPages, prev + 1))}
                className="rounded-md border border-gray-300 px-3 py-1.5 disabled:opacity-50"
              >
                Siguiente
              </button>
            </div>
            </div>
        </>
      )}

      {activeView === 'movements' && (
        <div className="space-y-4">
          <div className="grid gap-3 rounded-xl border border-gray-200 bg-white p-4 shadow-sm md:grid-cols-2 xl:grid-cols-6">
            {isAdmin && (
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600">Sucursal</label>
                <select
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                  value={movementFilters.branchId}
                  onChange={(e) => setMovementFilters((prev) => ({ ...prev, branchId: e.target.value }))}
                >
                  <option value="">Actual ({getBranchName(currentBranchId ?? '')})</option>
                  {branches.map((branch) => (
                    <option key={branch.id} value={branch.id}>{branch.name}</option>
                  ))}
                </select>
              </div>
            )}

            <div>
              <label className="mb-1 block text-xs font-medium text-gray-600">Producto</label>
              <select
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                value={movementFilters.productId}
                onChange={(e) => setMovementFilters((prev) => ({ ...prev, productId: e.target.value }))}
              >
                <option value="">Todos</option>
                {products.map((product) => (
                  <option key={product.id} value={product.id}>{product.name}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-gray-600">Usuario</label>
              <select
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                value={movementFilters.userId}
                onChange={(e) => setMovementFilters((prev) => ({ ...prev, userId: e.target.value }))}
              >
                <option value="">Todos</option>
                {movementUsers.map((user) => (
                  <option key={user.id} value={user.id}>{user.name}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-gray-600">Motivo</label>
              <input
                type="text"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                value={movementFilters.reason}
                onChange={(e) => setMovementFilters((prev) => ({ ...prev, reason: e.target.value }))}
                placeholder="Merma, conteo..."
              />
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-gray-600">Desde</label>
              <input
                type="date"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                value={movementFilters.from}
                onChange={(e) => setMovementFilters((prev) => ({ ...prev, from: e.target.value }))}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-gray-600">Hasta</label>
              <input
                type="date"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                value={movementFilters.to}
                onChange={(e) => setMovementFilters((prev) => ({ ...prev, to: e.target.value }))}
              />
            </div>

            <div className="md:col-span-2 xl:col-span-6">
              <button
                type="button"
                className="rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-700 hover:bg-gray-100"
                onClick={() => setMovementFilters(defaultMovementFilters)}
              >
                Limpiar filtros
              </button>
            </div>
          </div>

          <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm text-gray-600">
                <thead className="bg-gray-50 text-xs uppercase text-gray-700">
                  <tr>
                    <th scope="col" className="px-6 py-4 font-semibold">Fecha</th>
                    <th scope="col" className="px-6 py-4 font-semibold">Producto</th>
                    <th scope="col" className="px-6 py-4 font-semibold">Tipo</th>
                    <th scope="col" className="px-6 py-4 font-semibold text-right">Cantidad</th>
                    <th scope="col" className="px-6 py-4 font-semibold">Motivo</th>
                    <th scope="col" className="px-6 py-4 font-semibold">Usuario</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {movementsQuery.isLoading && (
                    <tr>
                      <td colSpan={6} className="px-6 py-12 text-center text-gray-500">
                        Cargando historial de movimientos...
                      </td>
                    </tr>
                  )}

                  {!movementsQuery.isLoading && (movementsQuery.data ?? []).map((movement) => (
                    <tr key={movement.id} className="hover:bg-gray-50">
                      <td className="px-6 py-4">{new Date(movement.createdAt).toLocaleString('es-CO')}</td>
                      <td className="px-6 py-4 font-medium text-gray-900">{movement.productName}</td>
                      <td className="px-6 py-4">{getMovementTypeLabel(movement.movementType)}</td>
                      <td className={`px-6 py-4 text-right font-semibold ${movement.quantity >= 0 ? 'text-emerald-700' : 'text-red-700'}`}>
                        {movement.quantity >= 0 ? '+' : ''}{movement.quantity}
                      </td>
                      <td className="px-6 py-4">{movement.reason || '-'}</td>
                      <td className="px-6 py-4">{movement.userName}</td>
                    </tr>
                  ))}

                  {!movementsQuery.isLoading && (movementsQuery.data ?? []).length === 0 && (
                    <tr>
                      <td colSpan={6} className="px-6 py-12 text-center text-gray-500">
                        No hay movimientos para los filtros seleccionados.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {activeView === 'kardex' && (
        <div className="space-y-4">
          <div className="grid gap-3 rounded-xl border border-gray-200 bg-white p-4 shadow-sm md:grid-cols-2 xl:grid-cols-5">
            {isAdmin && (
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600">Sucursal</label>
                <select
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                  value={kardexFilters.branchId}
                  onChange={(e) => setKardexFilters((prev) => ({ ...prev, branchId: e.target.value }))}
                >
                  <option value="">Selecciona sucursal</option>
                  {branches.map((branch) => (
                    <option key={branch.id} value={branch.id}>{branch.name}</option>
                  ))}
                </select>
              </div>
            )}

            {!isAdmin && (
              <div>
                <label className="mb-1 block text-xs font-medium text-gray-600">Sucursal</label>
                <input
                  readOnly
                  value={getBranchName(currentBranchId ?? '')}
                  className="w-full rounded-lg border border-gray-200 bg-gray-100 px-3 py-2 text-sm text-gray-700"
                />
              </div>
            )}

            <div>
              <label className="mb-1 block text-xs font-medium text-gray-600">Producto</label>
              <select
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                value={kardexFilters.productId}
                onChange={(e) => setKardexFilters((prev) => ({ ...prev, productId: e.target.value }))}
              >
                <option value="">Selecciona producto</option>
                {products.map((product) => (
                  <option key={product.id} value={product.id}>{product.name}</option>
                ))}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-gray-600">Desde</label>
              <input
                type="date"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                value={kardexFilters.from}
                onChange={(e) => setKardexFilters((prev) => ({ ...prev, from: e.target.value }))}
              />
            </div>

            <div>
              <label className="mb-1 block text-xs font-medium text-gray-600">Hasta</label>
              <input
                type="date"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500"
                value={kardexFilters.to}
                onChange={(e) => setKardexFilters((prev) => ({ ...prev, to: e.target.value }))}
              />
            </div>

            <div className="flex items-end gap-2">
              <button
                type="button"
                className="w-full rounded-lg bg-blue-600 px-3 py-2 text-sm font-medium text-white hover:bg-blue-700"
                onClick={handleGenerateKardexReport}
              >
                Generar Reporte
              </button>
            </div>
          </div>

          <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
            <div className="overflow-x-auto">
              <table className="w-full text-left text-sm text-gray-600">
                <thead className="bg-gray-50 text-xs uppercase text-gray-700">
                  <tr>
                    <th className="px-6 py-4 font-semibold">Fecha</th>
                    <th className="px-6 py-4 font-semibold">Tipo</th>
                    <th className="px-6 py-4 font-semibold">Referencia</th>
                    <th className="px-6 py-4 font-semibold text-right">Entradas</th>
                    <th className="px-6 py-4 font-semibold text-right">Salidas</th>
                    <th className="px-6 py-4 font-semibold text-right">Balance</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {kardexQuery.isLoading && (
                    <tr>
                      <td colSpan={6} className="px-6 py-12 text-center text-gray-500">
                        Generando reporte kardex...
                      </td>
                    </tr>
                  )}

                  {!kardexQuery.isLoading && kardexRequest && (kardexQuery.data?.rows ?? []).length === 0 && (
                    <tr>
                      <td colSpan={6} className="px-6 py-12 text-center text-gray-500">
                        No hay movimientos para los filtros seleccionados.
                      </td>
                    </tr>
                  )}

                  {!kardexQuery.isLoading && (kardexQuery.data?.rows ?? []).map((row) => (
                    <tr key={row.id} className="hover:bg-gray-50">
                      <td className="px-6 py-4">{new Date(row.createdAt).toLocaleString('es-CO')}</td>
                      <td className="px-6 py-4">{row.movementType}</td>
                      <td className="px-6 py-4">{row.reference || '-'}</td>
                      <td className="px-6 py-4 text-right font-semibold text-emerald-700">
                        {row.entries > 0 ? row.entries : '-'}
                      </td>
                      <td className="px-6 py-4 text-right font-semibold text-red-700">
                        {row.exits > 0 ? row.exits : '-'}
                      </td>
                      <td className="px-6 py-4 text-right font-semibold text-gray-900">{row.balance}</td>
                    </tr>
                  ))}

                  {!kardexQuery.isLoading && !kardexRequest && (
                    <tr>
                      <td colSpan={6} className="px-6 py-12 text-center text-gray-500">
                        Selecciona producto, sucursal y presiona Generar Reporte.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {isAdmin && activeView === 'categories' && (
        <div className="grid gap-6 lg:grid-cols-2">
          <form className="space-y-4 rounded-xl border border-gray-200 bg-white p-4 shadow-sm" onSubmit={submitCategory}>
            <h2 className="text-lg font-semibold text-gray-900">{categoryForm.id ? 'Editar categoria' : 'Nueva categoria'}</h2>

            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Nombre</label>
              <input
                className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                value={categoryForm.name}
                onChange={(e) => setCategoryForm((prev) => ({ ...prev, name: e.target.value }))}
                required
              />
            </div>

            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Descripcion</label>
              <textarea
                className="min-h-20 w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                value={categoryForm.description}
                onChange={(e) => setCategoryForm((prev) => ({ ...prev, description: e.target.value }))}
              />
            </div>

            <div className="flex gap-2">
              <button
                type="submit"
                disabled={saveCategoryMutation.isPending}
                className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
              >
                {saveCategoryMutation.isPending ? 'Guardando...' : 'Guardar categoria'}
              </button>

              {categoryForm.id && (
                <button
                  type="button"
                  onClick={() => setCategoryForm(defaultCategoryForm)}
                  className="rounded-lg border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-100"
                >
                  Cancelar
                </button>
              )}
            </div>
          </form>

          <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
            <div className="overflow-x-auto">
            <table className="min-w-[620px] w-full divide-y divide-gray-200 text-sm">
              <thead className="bg-gray-50 text-xs uppercase text-gray-600">
                <tr>
                  <th className="px-4 py-3 text-left">Categoria</th>
                  <th className="px-4 py-3 text-center">Estado</th>
                  <th className="px-4 py-3 text-right">Acciones</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 bg-white">
                {categoriesQuery.isLoading && (
                  <tr>
                    <td colSpan={3} className="px-4 py-8 text-center text-gray-500">Cargando categorias...</td>
                  </tr>
                )}

                {!categoriesQuery.isLoading && (categoriesQuery.data ?? []).length === 0 && (
                  <tr>
                    <td colSpan={3} className="px-4 py-8 text-center text-gray-500">Sin categorias registradas.</td>
                  </tr>
                )}

                {(categoriesQuery.data ?? []).map((category) => (
                  <tr key={category.id}>
                    <td className="px-4 py-3">
                      <p className="font-medium text-gray-900">{category.name}</p>
                      <p className="text-xs text-gray-500">{category.description || 'Sin descripcion'}</p>
                    </td>
                    <td className="px-4 py-3 text-center">
                      <span className={`rounded-full px-2 py-1 text-xs font-medium ${category.isActive ? 'bg-green-50 text-green-700' : 'bg-gray-700 text-white'}`}>
                        {category.isActive ? 'Activa' : 'Inactivo'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => setCategoryForm({
                            id: category.id,
                            name: category.name,
                            description: category.description ?? '',
                          })}
                          className="rounded-md border border-gray-300 px-2.5 py-1 text-xs text-gray-700 hover:bg-gray-100"
                        >
                          Editar
                        </button>
                        <button
                          type="button"
                          onClick={() => deleteCategoryMutation.mutate(category.id)}
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
          </div>
        </div>
      )}

      {isAdmin && showProductModal && (
        <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-2xl rounded-xl bg-white p-5 shadow-xl">
            <div className="mb-4 flex items-start justify-between">
              <div>
                <h2 className="text-lg font-semibold text-gray-900">{productForm.id ? 'Editar Producto' : 'Nuevo Producto'}</h2>
                <p className="text-sm text-gray-500">Completa los datos del producto para el catálogo.</p>
              </div>
              <button
                type="button"
                className="rounded-md p-1 text-gray-500 hover:bg-gray-100"
                onClick={() => {
                  setShowProductModal(false);
                  setProductForm(defaultProductForm);
                }}
              >
                <X size={16} />
              </button>
            </div>

            <form className="space-y-4" onSubmit={submitProduct}>
              <div className="grid gap-3 md:grid-cols-2">
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Nombre</label>
                  <input
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                    value={productForm.name}
                    onChange={(e) => setProductForm((prev) => ({ ...prev, name: e.target.value }))}
                    required
                  />
                </div>
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">SKU</label>
                  <input
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                    value={productForm.sku}
                    onChange={(e) => setProductForm((prev) => ({ ...prev, sku: e.target.value }))}
                    required
                  />
                </div>
              </div>

              <div className="grid gap-3 md:grid-cols-2">
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Categoria</label>
                  <select
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                    value={productForm.categoryId}
                    onChange={(e) => setProductForm((prev) => ({ ...prev, categoryId: e.target.value }))}
                    required
                  >
                    <option value="">Selecciona categoria</option>
                    {(categoriesQuery.data ?? []).filter((c) => c.isActive).map((category) => (
                      <option key={category.id} value={category.id}>{category.name}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Precio Base</label>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                    value={productForm.basePrice}
                    onChange={(e) => setProductForm((prev) => ({ ...prev, basePrice: e.target.value }))}
                    required
                  />
                </div>
              </div>

              <div className="grid gap-3 md:grid-cols-2">
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Stock Inicial (opcional)</label>
                  <input
                    type="number"
                    min="0"
                    step="1"
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                    value={productForm.initialStock}
                    onChange={(e) => setProductForm((prev) => ({ ...prev, initialStock: e.target.value }))}
                  />
                </div>

                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Codigo de barras (opcional)</label>
                  <input
                    className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                    value={productForm.barcode}
                    onChange={(e) => setProductForm((prev) => ({ ...prev, barcode: e.target.value }))}
                  />
                </div>
              </div>

              <div className="flex justify-end gap-2 pt-2">
                <button
                  type="button"
                  onClick={() => {
                    setShowProductModal(false);
                    setProductForm(defaultProductForm);
                  }}
                  className="rounded-lg border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-100"
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  disabled={saveProductMutation.isPending}
                  className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
                >
                  {saveProductMutation.isPending ? 'Guardando...' : 'Guardar producto'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {isAdmin && adjustModal && (
        <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-xl bg-white p-5 shadow-xl">
            <div className="mb-4 flex items-start justify-between">
              <div>
                <h2 className="text-lg font-semibold text-gray-900">Ajuste Rapido de Stock</h2>
                <p className="text-sm text-gray-500">Producto: {adjustModal.productName}</p>
              </div>
              <button
                type="button"
                className="rounded-md p-1 text-gray-500 hover:bg-gray-100"
                onClick={() => setAdjustModal(null)}
              >
                <X size={16} />
              </button>
            </div>

            <form className="space-y-4" onSubmit={submitAdjustStock}>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Cantidad (+/-)</label>
                <input
                  type="number"
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  value={adjustModal.quantityDelta}
                  onChange={(e) => setAdjustModal((prev) => prev ? { ...prev, quantityDelta: Number(e.target.value) } : prev)}
                  step="1"
                  required
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Motivo</label>
                <select
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  value={adjustModal.reason}
                  onChange={(e) => setAdjustModal((prev) => prev ? { ...prev, reason: e.target.value } : prev)}
                >
                  <option value="Merma">Merma</option>
                  <option value="Error de conteo">Error de conteo</option>
                  <option value="Ingreso por compra">Ingreso por compra</option>
                </select>
              </div>

              <button
                type="submit"
                disabled={adjustStockMutation.isPending}
                className="w-full rounded-lg bg-blue-600 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
              >
                {adjustStockMutation.isPending ? 'Aplicando...' : 'Aplicar ajuste'}
              </button>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
