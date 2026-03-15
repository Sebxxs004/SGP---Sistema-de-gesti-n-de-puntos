import { useState } from 'react';
import { PackageSearch, Boxes, AlertCircle } from 'lucide-react';

export const Inventory = () => {
  // In a real scenario, this would be fetched via React Query using apiClient
  // Since Phase 4 only requested "CreateProductCommand", we mock the list view for testing the UI
  const [products] = useState([
    { id: '1', sku: 'PRD-001', name: 'Café de Especialidad 500g', stock: 45, price: 12.50, category: 'Cafetería' },
    { id: '2', sku: 'PRD-002', name: 'Taza SGP Pro', stock: 12, price: 8.90, category: 'Merch' },
    { id: '3', sku: 'PRD-003', name: 'Leche de Almendras 1L', stock: 4, price: 3.20, category: 'Cafetería' },
    { id: '4', sku: 'PRD-004', name: 'Galletas de Avena', stock: 0, price: 2.10, category: 'Pastelería' },
  ]);

  const [searchTerm, setSearchTerm] = useState('');

  const filteredProducts = products.filter(p => 
    p.name.toLowerCase().includes(searchTerm.toLowerCase()) || 
    p.sku.toLowerCase().includes(searchTerm.toLowerCase())
  );

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
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {filteredProducts.map((product) => (
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
                </tr>
              ))}
              {filteredProducts.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center text-gray-500">
                    No se encontraron productos coincidentes.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
