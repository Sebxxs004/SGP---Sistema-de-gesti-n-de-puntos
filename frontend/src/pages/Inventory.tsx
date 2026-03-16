import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { PackageSearch, Boxes, AlertCircle, Plus, X } from 'lucide-react';
import apiClient from '../api/apiClient';
import { useAuthStore } from '../store/useAuthStore';

type InventoryProduct = {
  id: string;
  name: string;
  sku: string;
  category: string;
  price: number;
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

export const Inventory = () => {
  const queryClient = useQueryClient();
  const currentBranchId = useAuthStore((state) => state.currentBranchId);
  const [searchTerm, setSearchTerm] = useState('');
  const [notification, setNotification] = useState<{ text: string; isError: boolean } | null>(null);
  const [adjustModal, setAdjustModal] = useState<AdjustModalState | null>(null);

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
      setNotification({ text: 'Ajuste de inventario aplicado.', isError: false });
      setAdjustModal(null);
    },
    onError: () => {
      setNotification({ text: 'No se pudo aplicar el ajuste de inventario.', isError: true });
    },
  });

  const products = useMemo(() => stockQuery.data ?? [], [stockQuery.data]);

  const filteredProducts = products.filter(p => 
    p.name.toLowerCase().includes(searchTerm.toLowerCase()) || 
    p.sku.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const openAdjustModal = (product: InventoryProduct) => {
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

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Inventario</h1>
          <p className="mt-1 text-sm text-gray-500">Gestión de stock de la sucursal activa</p>
        </div>
        
        <div className="relative w-full sm:w-72">
          <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
            <PackageSearch size={20} className="text-gray-400" />
          </div>
          <input
            type="text"
            className="w-full rounded-lg border border-gray-300 py-2 pl-10 pr-3 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            placeholder="Buscar por nombre o SKU..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
        </div>
      </div>

      {notification && (
        <div className={`rounded-lg px-4 py-3 text-sm ${notification.isError ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-700'}`}>
          {notification.text}
        </div>
      )}

      <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm text-gray-600">
            <thead className="bg-gray-50 text-xs uppercase text-gray-700">
              <tr>
                <th scope="col" className="px-6 py-4 font-semibold">Producto</th>
                <th scope="col" className="px-6 py-4 font-semibold">SKU</th>
                <th scope="col" className="px-6 py-4 font-semibold">Categoría</th>
                <th scope="col" className="px-6 py-4 font-semibold text-right">Precio</th>
                <th scope="col" className="px-6 py-4 font-semibold text-right">Stock</th>
                <th scope="col" className="px-6 py-4 font-semibold text-center">Estado</th>
                <th scope="col" className="px-6 py-4 font-semibold text-center">Accion</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {stockQuery.isLoading && (
                <tr>
                  <td colSpan={7} className="px-6 py-12 text-center text-gray-500">
                    Cargando inventario de la sucursal...
                  </td>
                </tr>
              )}

              {!stockQuery.isLoading && filteredProducts.map((product) => (
                <tr key={product.id} className="hover:bg-gray-50">
                  <td className="px-6 py-4 font-medium text-gray-900">{product.name}</td>
                  <td className="px-6 py-4">{product.sku}</td>
                  <td className="px-6 py-4">
                    <span className="inline-flex rounded-full bg-blue-50 px-2 py-1 text-xs font-semibold text-blue-700">
                      {product.category}
                    </span>
                  </td>
                  <td className="px-6 py-4 text-right">${product.price.toFixed(2)}</td>
                  <td className="px-6 py-4 text-right font-medium">
                    {product.stock}
                  </td>
                  <td className="px-6 py-4 text-center">
                    {product.stock > 10 ? (
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
                  <td className="px-6 py-4 text-center">
                    <button
                      type="button"
                      className="inline-flex items-center rounded-md border border-gray-300 p-2 text-gray-700 hover:bg-gray-100"
                      title="Ajustar stock"
                      onClick={() => openAdjustModal(product)}
                    >
                      <Plus size={14} />
                    </button>
                  </td>
                </tr>
              ))}
              {filteredProducts.length === 0 && (
                <tr>
                  <td colSpan={7} className="px-6 py-12 text-center text-gray-500">
                    No se encontraron productos coincidentes.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {adjustModal && (
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
