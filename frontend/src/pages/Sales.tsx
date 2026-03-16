import { useEffect, useState } from 'react';
import { db } from '../db/db';
import type { OfflineSale, OfflineSaleDetail, OfflinePayment } from '../db/db';
import apiClient from '../api/apiClient';
import { useAuthStore } from '../store/useAuthStore';
import { Search, Plus, Minus, CreditCard, Banknote, Trash2, WifiOff, Wifi, RefreshCw } from 'lucide-react';
import { CloseCashierModal } from '../components/CloseCashierModal';
import { isAxiosError } from 'axios';
import { getCatalogLastSyncAt, getCatalogProducts, syncCatalog } from '../services/CatalogSyncService';
import type { CatalogProduct } from '../db/db';

interface CartItem {
  id: string; // ProductId
  name: string;
  price: number;
  quantity: number;
}

export const Sales = () => {
  const [cart, setCart] = useState<CartItem[]>([]);
  const [catalog, setCatalog] = useState<CatalogProduct[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [paymentMethod, setPaymentMethod] = useState<number>(0); // 0: Cash, 1: CreditCard
  const [isProcessing, setIsProcessing] = useState(false);
  const [isManualCatalogSyncing, setIsManualCatalogSyncing] = useState(false);
  const [lastCatalogSyncAt, setLastCatalogSyncAt] = useState<string | null>(null);
  const [notification, setNotification] = useState<{message: string, isError: boolean} | null>(null);
  const [isCloseModalOpen, setIsCloseModalOpen] = useState(false);
  const [isClosingSession, setIsClosingSession] = useState(false);
  const [closeSummary, setCloseSummary] = useState<{
    salesTotal: number;
    finalBalanceExpected: number;
    finalBalanceEncounted: number;
    difference: number;
  } | null>(null);
  
  const { tenantId, currentBranchId, currentSessionId, setCurrentSessionId } = useAuthStore();
  const isOnline = navigator.onLine; // For UI feedback, hook handles real sync

  const filteredCatalog = catalog.filter(p =>
    p.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    p.sku.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const refreshLocalCatalog = async () => {
    if (!currentBranchId) {
      setCatalog([]);
      setLastCatalogSyncAt(null);
      return;
    }

    const [products, lastSync] = await Promise.all([
      getCatalogProducts(currentBranchId),
      getCatalogLastSyncAt(currentBranchId),
    ]);

    setCatalog(products);
    setLastCatalogSyncAt(lastSync);
  };

  useEffect(() => {
    refreshLocalCatalog();

    const intervalId = window.setInterval(() => {
      refreshLocalCatalog();
    }, 5000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [currentBranchId]);

  const subTotal = cart.reduce((sum, item) => sum + (item.price * item.quantity), 0);
  const tax = subTotal * 0.16; // 16% assumed
  const total = subTotal + tax;

  const addToCart = (product: CatalogProduct) => {
    setCart(prev => {
      const existing = prev.find(item => item.id === product.id);
      if (existing) {
        return prev.map(item => item.id === product.id ? { ...item, quantity: item.quantity + 1 } : item);
      }
      return [...prev, { id: product.id, name: product.name, price: product.price, quantity: 1 }];
    });
  };

  const updateQuantity = (id: string, delta: number) => {
    setCart(prev => prev.map(item => {
      if (item.id === id) {
        const newQ = item.quantity + delta;
        return newQ > 0 ? { ...item, quantity: newQ } : item;
      }
      return item;
    }));
  };

  const removeFromCart = (id: string) => setCart(prev => prev.filter(item => item.id !== id));

  const handleManualCatalogSync = async () => {
    if (!currentBranchId) {
      setNotification({ message: 'Selecciona una sucursal activa para sincronizar catálogo.', isError: true });
      return;
    }

    setIsManualCatalogSyncing(true);

    try {
      const result = await syncCatalog(currentBranchId, lastCatalogSyncAt ?? undefined);
      await refreshLocalCatalog();
      setNotification({
        message: `Catálogo sincronizado (${result.productsCount} productos).`,
        isError: false,
      });
    } catch (error) {
      console.error('Manual catalog sync failed:', error);
      setNotification({ message: 'No fue posible sincronizar el catálogo.', isError: true });
    } finally {
      setIsManualCatalogSyncing(false);
      setTimeout(() => setNotification(null), 4000);
    }
  };

  const handleFinalizeSale = async () => {
    if (cart.length === 0) return;
    if (!currentBranchId) {
      setNotification({ message: 'Selecciona una sucursal activa antes de vender.', isError: true });
      return;
    }
    if (!currentSessionId) {
      setNotification({ message: 'No tienes una sesión de caja activa para vender.', isError: true });
      return;
    }

    setIsProcessing(true);
    setNotification(null);
    let shouldClearCart = false;

    // Frontend generates the ID to ensure idempotency across offline/online
    const saleId = crypto.randomUUID();
    const branchId = currentBranchId;
    const sessionId = currentSessionId;

    const details: OfflineSaleDetail[] = cart.map(item => ({
      id: crypto.randomUUID(),
      productId: item.id,
      quantity: item.quantity,
      unitPrice: item.price
    }));

    const payments: OfflinePayment[] = [{
      id: crypto.randomUUID(),
      amount: total,
      method: paymentMethod
    }];

    const salePayload: OfflineSale = {
      id: saleId,
      tenantId: tenantId || '',
      sessionId,
      branchId,
      subTotal,
      tax,
      total,
      createdAt: new Date().toISOString(),
      details,
      payments,
      isSynced: false
    };

    try {
      // Try API first when online
      if (isOnline) {
        await apiClient.post('/sales', salePayload);
        setNotification({ message: 'Venta procesada exitosamente.', isError: false });
        shouldClearCart = true;
      } else {
        await db.sales.add(salePayload);
        setNotification({ message: 'Sin conexión. Venta guardada localmente.', isError: false });
        shouldClearCart = true;
      }
    } catch (error: unknown) {
      const apiErrorMessage =
        isAxiosError(error) ? error.response?.data?.error?.message : undefined;

      const isNetworkFailure =
        !navigator.onLine ||
        (isAxiosError(error) && (!error.response && (error.code === 'ERR_NETWORK' || error.code === 'ECONNABORTED')));

      // Only fallback to local persistence for real connectivity failures.
      if (isNetworkFailure) {
        try {
          await db.sales.add(salePayload);
          setNotification({ message: 'Sin conexión o API no disponible. Venta guardada localmente.', isError: false });
          shouldClearCart = true;
        } catch (dbError) {
          console.error('Error guardando en Dexie', dbError);
          setNotification({ message: 'No se pudo procesar ni guardar localmente la venta.', isError: true });
        }
      } else {
        setNotification({ message: apiErrorMessage ?? 'No fue posible procesar la venta.', isError: true });
      }
    } finally {
      setIsProcessing(false);
      if (shouldClearCart) {
        setCart([]);
      }
      setTimeout(() => setNotification(null), 4000);
    }
  };

  const handleCloseSession = async (finalBalanceEncounted: number) => {
    setIsClosingSession(true);
    setNotification(null);

    try {
      const response = await apiClient.post('/sales/sessions/close', {
        branchId: '00000000-0000-0000-0000-000000000000',
        finalBalanceEncounted,
      });

      const payload = (response.data as {
        data?: {
          salesTotal: number;
          finalBalanceExpected: number;
          finalBalanceEncounted: number;
          difference: number;
        };
      }).data;

      if (payload) {
        setCloseSummary(payload);
      }

      setCurrentSessionId(null);
      setIsCloseModalOpen(false);
      setNotification({ message: 'Caja cerrada correctamente.', isError: false });
    } catch (error: unknown) {
      const message =
        typeof error === 'object' && error !== null && 'response' in error
          ? (error as { response?: { data?: { error?: { message?: string } } } }).response?.data?.error?.message
          : undefined;

      setNotification({ message: message ?? 'No fue posible cerrar la caja.', isError: true });
    } finally {
      setIsClosingSession(false);
    }
  };

  return (
    <div className="flex flex-col lg:flex-row gap-6 h-[calc(100vh-8rem)]">
      {/* Catalog Section */}
      <div className="flex-1 flex flex-col bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="p-4 border-b border-gray-100 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-gray-800">Catálogo</h2>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setIsCloseModalOpen(true)}
              className="rounded-lg border border-red-200 bg-red-50 px-3 py-1.5 text-xs font-semibold text-red-700 hover:bg-red-100"
            >
              Cerrar Turno
            </button>
            <button
              type="button"
              onClick={handleManualCatalogSync}
              disabled={isManualCatalogSyncing || !currentBranchId}
              className="rounded-lg border border-blue-200 bg-blue-50 px-3 py-1.5 text-xs font-semibold text-blue-700 hover:bg-blue-100 disabled:cursor-not-allowed disabled:opacity-60"
            >
              <span className="inline-flex items-center gap-1.5">
                <RefreshCw size={13} className={isManualCatalogSyncing ? 'animate-spin' : ''} />
                {isManualCatalogSyncing ? 'Sincronizando...' : 'Sync Catalogo'}
              </span>
            </button>
            {isOnline ? (
              <span className="flex items-center gap-2 text-xs font-medium text-green-600 bg-green-50 px-2.5 py-1 rounded-full"><Wifi size={14} /> Online</span>
            ) : (
              <span className="flex items-center gap-2 text-xs font-medium text-amber-600 bg-amber-50 px-2.5 py-1 rounded-full"><WifiOff size={14} /> Offline Mode</span>
            )}
          </div>
        </div>
        <div className="p-4 border-b border-gray-100">
          <p className="mb-2 text-xs text-gray-500">
            Ultima sync: {lastCatalogSyncAt ? new Date(lastCatalogSyncAt).toLocaleString() : 'Sin sincronizar'}
          </p>
          <div className="relative">
            <Search size={18} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
            <input 
              className="w-full bg-gray-50 border border-gray-200 rounded-lg pl-10 pr-4 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:outline-none" 
              placeholder="Buscar producto..." 
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>
        </div>
        <div className="flex-1 overflow-y-auto p-4 grid grid-cols-2 lg:grid-cols-3 gap-4">
          {filteredCatalog.map(p => (
            <button 
              key={p.id}
              onClick={() => addToCart(p)}
              className="flex flex-col text-left border border-gray-100 p-4 rounded-xl hover:border-blue-500 hover:bg-blue-50 transition-all group"
            >
              <span className="text-xs text-gray-400 mb-1">{p.sku}</span>
              <span className="font-medium text-gray-800 flex-1">{p.name}</span>
              <span className="text-blue-600 font-bold mt-2">${p.price.toFixed(2)}</span>
            </button>
          ))}
          {filteredCatalog.length === 0 && (
            <div className="col-span-full rounded-lg border border-dashed border-gray-300 bg-gray-50 p-6 text-center text-sm text-gray-500">
              No hay productos en caché para esta sucursal. Sincroniza catálogo para habilitar ventas offline.
            </div>
          )}
        </div>
      </div>

      {/* Cart & Checkout Section */}
      <div className="w-full lg:w-96 flex flex-col bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="p-4 border-b border-gray-100">
          <h2 className="text-lg font-semibold text-gray-800">Ticket de Venta</h2>
        </div>
        
        <div className="flex-1 overflow-y-auto p-4 space-y-3">
          {cart.length === 0 ? (
            <div className="h-full flex items-center justify-center text-gray-400 text-sm">
              El carrito está vacío
            </div>
          ) : cart.map(item => (
            <div key={item.id} className="flex items-center gap-3 p-3 bg-gray-50 rounded-lg border border-gray-100">
              <div className="flex-1">
                <p className="text-sm font-medium text-gray-800 line-clamp-1">{item.name}</p>
                <p className="text-xs text-blue-600 font-semibold">${item.price.toFixed(2)}</p>
              </div>
              <div className="flex items-center bg-white rounded-md border border-gray-200 shadow-sm">
                <button onClick={() => updateQuantity(item.id, -1)} className="p-1 hover:bg-gray-100 text-gray-500"><Minus size={14}/></button>
                <span className="w-8 text-center text-sm font-medium">{item.quantity}</span>
                <button onClick={() => updateQuantity(item.id, 1)} className="p-1 hover:bg-gray-100 text-gray-500"><Plus size={14}/></button>
              </div>
              <button onClick={() => removeFromCart(item.id)} className="p-1.5 text-red-500 hover:bg-red-50 rounded-md">
                <Trash2 size={16} />
              </button>
            </div>
          ))}
        </div>

        {/* Totals & Actions */}
        <div className="p-4 border-t border-gray-100 bg-gray-50 space-y-4">
          <div className="space-y-1 text-sm">
            <div className="flex justify-between text-gray-500"><span>Subtotal</span><span>${subTotal.toFixed(2)}</span></div>
            <div className="flex justify-between text-gray-500"><span>IVA (16%)</span><span>${tax.toFixed(2)}</span></div>
            <div className="flex justify-between text-lg font-bold text-gray-900 border-t border-gray-200 pt-2 mt-2">
              <span>Total</span><span>${total.toFixed(2)}</span>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2">
             <button 
                onClick={() => setPaymentMethod(0)}
                className={`py-2 px-3 rounded-lg flex items-center justify-center gap-2 text-sm font-medium transition-all ${paymentMethod === 0 ? 'bg-blue-100 text-blue-700 border-2 border-blue-500' : 'bg-white border text-gray-600 hover:bg-gray-50'}`}
              >
               <Banknote size={16}/> Efectivo
             </button>
             <button 
                onClick={() => setPaymentMethod(1)}
                className={`py-2 px-3 rounded-lg flex items-center justify-center gap-2 text-sm font-medium transition-all ${paymentMethod === 1 ? 'bg-blue-100 text-blue-700 border-2 border-blue-500' : 'bg-white border text-gray-600 hover:bg-gray-50'}`}
              >
               <CreditCard size={16}/> Tarjeta
             </button>
          </div>

          {notification && (
            <div className={`p-3 text-sm rounded-lg text-center ${notification.isError ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'}`}>
              {notification.message}
            </div>
          )}

          {closeSummary && (
            <div className="rounded-lg border border-blue-200 bg-blue-50 p-3 text-sm text-blue-900">
              <p className="font-semibold">Resumen de Arqueo</p>
              <p>Ventas totales: ${closeSummary.salesTotal.toFixed(2)}</p>
              <p>Esperado: ${closeSummary.finalBalanceExpected.toFixed(2)}</p>
              <p>Contado: ${closeSummary.finalBalanceEncounted.toFixed(2)}</p>
              <p>Diferencia: ${closeSummary.difference.toFixed(2)}</p>
            </div>
          )}

          <button 
            disabled={cart.length === 0 || isProcessing}
            onClick={handleFinalizeSale}
            className="w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold py-3 rounded-xl shadow-md transition-all disabled:opacity-50 disabled:cursor-not-allowed active:scale-[0.98]"
          >
            {isProcessing ? 'Procesando...' : 'Finalizar Venta'}
          </button>
        </div>
      </div>

      {isCloseModalOpen && (
        <CloseCashierModal
          isLoading={isClosingSession}
          onClose={() => setIsCloseModalOpen(false)}
          onSubmit={handleCloseSession}
        />
      )}
    </div>
  );
};
