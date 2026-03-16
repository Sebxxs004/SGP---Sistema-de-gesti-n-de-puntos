import { useEffect, useState } from 'react';
import { db } from '../db/db';
import type { OfflineSale, OfflineSaleDetail, OfflinePayment } from '../db/db';
import apiClient from '../api/apiClient';
import { useAuthStore } from '../store/useAuthStore';
import { Search, Plus, Minus, CreditCard, Banknote, Trash2, WifiOff, Wifi, RefreshCw, Printer } from 'lucide-react';
import { CloseCashierModal } from '../components/CloseCashierModal';
import { isAxiosError } from 'axios';
import { getCatalogLastSyncAt, getCatalogProducts, syncCatalog } from '../services/CatalogSyncService';
import type { CatalogProduct } from '../db/db';
import { TicketTemplate } from '../components/TicketTemplate';
import type { TicketData, TicketLineItem, TicketPayment } from '../components/TicketTemplate';
import { useCompanySettings } from '../hooks/useCompanySettings';
import { formatCurrency } from '../utils/currency';

interface SessionSaleHistoryItem {
  id: string;
  createdAt: string;
  subTotal: number;
  tax: number;
  total: number;
  items: number;
  payments: Array<{ method: string; amount: number }>;
}

interface SessionSalesHistoryResponse {
  success: boolean;
  data: {
    sessionId: string | null;
    sales: SessionSaleHistoryItem[];
  };
}

interface TicketDataResponse {
  success: boolean;
  data: TicketData;
}

interface CashSessionHistoryItem {
  id: string;
  cashierName: string;
  openedAt: string;
  closedAt: string;
  expectedAmount: number;
  countedAmount: number;
  difference: number;
}

interface CashSessionsHistoryResponse {
  success: boolean;
  data: CashSessionHistoryItem[];
}

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
  const [isLoadingHistory, setIsLoadingHistory] = useState(false);
  const [isFetchingTicket, setIsFetchingTicket] = useState(false);
  const [sessionSalesHistory, setSessionSalesHistory] = useState<SessionSaleHistoryItem[]>([]);
  const [lastTicketData, setLastTicketData] = useState<TicketData | null>(null);
  const [ticketToPrint, setTicketToPrint] = useState<TicketData | null>(null);
  const [lastCatalogSyncAt, setLastCatalogSyncAt] = useState<string | null>(null);
  const [activeSubmodule, setActiveSubmodule] = useState<'pos' | 'cashHistory'>('pos');
  const [isLoadingCashHistory, setIsLoadingCashHistory] = useState(false);
  const [cashSessionsHistory, setCashSessionsHistory] = useState<CashSessionHistoryItem[]>([]);
  const [notification, setNotification] = useState<{message: string, isError: boolean} | null>(null);
  const [isCloseModalOpen, setIsCloseModalOpen] = useState(false);
  const [isClosingSession, setIsClosingSession] = useState(false);
  const [closeSummary, setCloseSummary] = useState<{
    salesTotal: number;
    finalBalanceExpected: number;
    finalBalanceEncounted: number;
    difference: number;
  } | null>(null);
  
  const { tenantId, currentBranchId, currentSessionId, setCurrentSessionId, branches, user } = useAuthStore();
  const companySettingsQuery = useCompanySettings();
  const taxPercentage = companySettingsQuery.data?.taxPercentage ?? 16;
  const currencySymbol = companySettingsQuery.data?.currencySymbol ?? '$';
  const isOnline = navigator.onLine; // For UI feedback, hook handles real sync
  const currentBranchName = branches.find((branch) => branch.id === currentBranchId)?.name ?? 'Sucursal';

  useEffect(() => {
    const onAfterPrint = () => {
      setTicketToPrint(null);
    };

    window.addEventListener('afterprint', onAfterPrint);
    return () => {
      window.removeEventListener('afterprint', onAfterPrint);
    };
  }, []);

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
  const tax = subTotal * (taxPercentage / 100);
  const total = subTotal + tax;

  const formatMoney = (value: number) => formatCurrency(value, currencySymbol);

  const buildLocalTicketData = (
    saleId: string,
    createdAt: string,
    details: OfflineSaleDetail[],
    payments: OfflinePayment[],
    saleSubTotal: number,
    saleTax: number,
    saleTotal: number
  ): TicketData => {
    const detailsByProduct = new Map(details.map((detail) => [detail.productId, detail]));

    const items: TicketLineItem[] = cart
      .filter((item) => detailsByProduct.has(item.id))
      .map((item) => {
        const detail = detailsByProduct.get(item.id)!;
        return {
          productId: item.id,
          productName: item.name,
          quantity: detail.quantity,
          unitPrice: detail.unitPrice,
          subTotal: detail.quantity * detail.unitPrice,
        };
      });

    const normalizedPayments: TicketPayment[] = payments.map((payment) => ({
      method: payment.method === 0 ? 'Cash' : 'CreditCard',
      amount: payment.amount,
    }));

    return {
      saleId,
      ticketNumber: saleId.slice(0, 8).toUpperCase(),
      issuedAt: createdAt,
      company: {
        id: tenantId ?? 'N/A',
        name: companySettingsQuery.data?.name ?? 'SGP',
        taxId: companySettingsQuery.data?.taxId ?? 'N/A',
        thankYouMessage: companySettingsQuery.data?.thankYouMessage ?? 'Gracias por su compra',
        taxPercentage,
        currencySymbol,
      },
      branch: {
        id: currentBranchId ?? 'N/A',
        name: currentBranchName,
        address: '',
        phone: '',
      },
      cashier: {
        id: user?.id ?? 'N/A',
        email: user?.email ?? 'cajero@sgp.local',
      },
      items,
      payments: normalizedPayments,
      subTotal: saleSubTotal,
      tax: saleTax,
      total: saleTotal,
    };
  };

  const fetchTicketData = async (saleId: string): Promise<TicketData> => {
    const response = await apiClient.get<TicketDataResponse>(`/sales/${saleId}/ticket-data`);
    return response.data.data;
  };

  const printTicket = (ticket: TicketData) => {
    setTicketToPrint(ticket);
    window.setTimeout(() => {
      window.print();
    }, 80);
  };

  const handleReprintFromHistory = async (saleId: string) => {
    setIsFetchingTicket(true);
    try {
      const ticket = await fetchTicketData(saleId);
      printTicket(ticket);
    } catch (error) {
      console.error('Error fetching ticket data:', error);
      setNotification({ message: 'No se pudo recuperar el comprobante para reimpresion.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsFetchingTicket(false);
    }
  };

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

  const refreshSessionHistory = async () => {
    if (!currentSessionId) {
      setSessionSalesHistory([]);
      return;
    }

    setIsLoadingHistory(true);
    try {
      const response = await apiClient.get<SessionSalesHistoryResponse>('/sales/history/current-session');
      setSessionSalesHistory(response.data.data.sales ?? []);
    } catch (error) {
      console.error('Error loading session sales history:', error);
    } finally {
      setIsLoadingHistory(false);
    }
  };

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

  useEffect(() => {
    refreshSessionHistory();

    const intervalId = window.setInterval(() => {
      refreshSessionHistory();
    }, 20000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [currentSessionId]);

  const refreshCashSessionsHistory = async () => {
    setIsLoadingCashHistory(true);
    try {
      const response = await apiClient.get<CashSessionsHistoryResponse>('/sales/sessions/history');
      setCashSessionsHistory(response.data.data ?? []);
    } catch (error) {
      console.error('Error loading cash sessions history:', error);
      setNotification({ message: 'No se pudo cargar el historial de cajas.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsLoadingCashHistory(false);
    }
  };

  useEffect(() => {
    if (activeSubmodule === 'cashHistory') {
      refreshCashSessionsHistory();
    }
  }, [activeSubmodule, currentBranchId]);

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
        const response = await apiClient.post<{ success: boolean; data?: { id?: string } }>('/sales', salePayload);
        const registeredSaleId = response.data?.data?.id ?? saleId;

        try {
          const ticket = await fetchTicketData(registeredSaleId);
          setLastTicketData(ticket);
        } catch {
          setLastTicketData(buildLocalTicketData(saleId, salePayload.createdAt, details, payments, subTotal, tax, total));
        }

        setNotification({ message: 'Venta procesada exitosamente.', isError: false });
        shouldClearCart = true;
      } else {
        await db.sales.add(salePayload);
        setLastTicketData(buildLocalTicketData(saleId, salePayload.createdAt, details, payments, subTotal, tax, total));
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
          setLastTicketData(buildLocalTicketData(saleId, salePayload.createdAt, details, payments, subTotal, tax, total));
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
      if (shouldClearCart) {
        refreshSessionHistory();
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
    <div className="space-y-4">
      <div className="flex gap-2">
        <button
          type="button"
          className={`rounded-lg px-3 py-2 text-sm font-medium ${activeSubmodule === 'pos' ? 'bg-blue-600 text-white' : 'border border-gray-300 bg-white text-gray-700 hover:bg-gray-100'}`}
          onClick={() => setActiveSubmodule('pos')}
        >
          POS
        </button>
        <button
          type="button"
          className={`rounded-lg px-3 py-2 text-sm font-medium ${activeSubmodule === 'cashHistory' ? 'bg-blue-600 text-white' : 'border border-gray-300 bg-white text-gray-700 hover:bg-gray-100'}`}
          onClick={() => setActiveSubmodule('cashHistory')}
        >
          Historial de Cajas
        </button>
      </div>

      {activeSubmodule === 'cashHistory' && (
        <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
          <div className="flex items-center justify-between border-b border-gray-100 p-4">
            <div>
              <h2 className="text-lg font-semibold text-gray-900">Historial de Cajas</h2>
              <p className="text-sm text-gray-500">Auditoria de turnos cerrados en la sucursal actual.</p>
            </div>
            <button
              type="button"
              onClick={refreshCashSessionsHistory}
              className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-100"
            >
              Actualizar
            </button>
          </div>

          <div className="overflow-x-auto">
            <table className="min-w-[820px] w-full text-left text-sm text-gray-600">
              <thead className="bg-gray-50 text-xs uppercase text-gray-700">
                <tr>
                  <th className="px-4 py-3">Cajero</th>
                  <th className="px-4 py-3">Apertura</th>
                  <th className="px-4 py-3">Cierre</th>
                  <th className="px-4 py-3 text-right">Esperado</th>
                  <th className="px-4 py-3 text-right">Contado</th>
                  <th className="px-4 py-3 text-right">Diferencia</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {isLoadingCashHistory && (
                  <tr>
                    <td colSpan={6} className="px-4 py-10 text-center text-gray-500">Cargando historial...</td>
                  </tr>
                )}

                {!isLoadingCashHistory && cashSessionsHistory.length === 0 && (
                  <tr>
                    <td colSpan={6} className="px-4 py-10 text-center text-gray-500">No hay arqueos cerrados para esta sucursal.</td>
                  </tr>
                )}

                {!isLoadingCashHistory && cashSessionsHistory.map((session) => (
                  <tr key={session.id} className="hover:bg-gray-50">
                    <td className="px-4 py-3 font-medium text-gray-900">{session.cashierName}</td>
                    <td className="px-4 py-3">{new Date(session.openedAt).toLocaleString('es-CO')}</td>
                    <td className="px-4 py-3">{new Date(session.closedAt).toLocaleString('es-CO')}</td>
                    <td className="px-4 py-3 text-right">{formatMoney(session.expectedAmount)}</td>
                    <td className="px-4 py-3 text-right">{formatMoney(session.countedAmount)}</td>
                    <td className={`px-4 py-3 text-right font-semibold ${session.difference >= 0 ? 'text-emerald-700' : 'text-red-700'}`}>
                      {formatMoney(session.difference)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {activeSubmodule === 'pos' && (
    <div className="flex flex-col lg:flex-row gap-6 h-[calc(100vh-12rem)]">
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
              <span className="text-blue-600 font-bold mt-2">{formatMoney(p.price)}</span>
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
          <div className="mb-3 rounded-lg border border-gray-200 bg-white p-3">
            <div className="mb-2 flex items-center justify-between">
              <h3 className="text-sm font-semibold text-gray-700">Historial de Ventas (Sesión)</h3>
              {isLoadingHistory && <span className="text-xs text-blue-600">Actualizando...</span>}
            </div>
            {sessionSalesHistory.length > 0 ? (
              <div className="max-h-44 overflow-y-auto">
                <table className="w-full border-collapse text-left text-xs">
                  <thead>
                    <tr className="border-b border-gray-200 text-gray-500">
                      <th className="py-1">Ticket</th>
                      <th className="py-1">Hora</th>
                      <th className="py-1 text-right">Total</th>
                      <th className="py-1 text-center">Acciones</th>
                    </tr>
                  </thead>
                  <tbody>
                    {sessionSalesHistory.map((sale) => (
                      <tr key={sale.id} className="border-b border-gray-100 text-gray-700">
                        <td className="py-1.5 font-medium">{sale.id.slice(0, 8)}</td>
                        <td className="py-1.5">{new Date(sale.createdAt).toLocaleTimeString()}</td>
                        <td className="py-1.5 text-right font-semibold">{formatMoney(sale.total)}</td>
                        <td className="py-1.5 text-center">
                          <button
                            type="button"
                            title="Reimprimir ticket"
                            disabled={isFetchingTicket}
                            onClick={() => handleReprintFromHistory(sale.id)}
                            className="inline-flex items-center justify-center rounded-md border border-gray-200 p-1.5 text-gray-600 hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-50"
                          >
                            <Printer size={14} />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="text-xs text-gray-500">No hay ventas registradas en esta sesión.</p>
            )}
          </div>

          {cart.length === 0 ? (
            <div className="h-full flex items-center justify-center text-gray-400 text-sm">
              El carrito está vacío
            </div>
          ) : cart.map(item => (
            <div key={item.id} className="flex items-center gap-3 p-3 bg-gray-50 rounded-lg border border-gray-100">
              <div className="flex-1">
                <p className="text-sm font-medium text-gray-800 line-clamp-1">{item.name}</p>
                <p className="text-xs text-blue-600 font-semibold">{formatMoney(item.price)}</p>
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
            <div className="flex justify-between text-gray-500"><span>Subtotal</span><span>{formatMoney(subTotal)}</span></div>
            <div className="flex justify-between text-gray-500"><span>IVA ({taxPercentage.toFixed(2)}%)</span><span>{formatMoney(tax)}</span></div>
            <div className="flex justify-between text-lg font-bold text-gray-900 border-t border-gray-200 pt-2 mt-2">
              <span>Total</span><span>{formatMoney(total)}</span>
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
              <p>Ventas totales: {formatMoney(closeSummary.salesTotal)}</p>
              <p>Esperado: {formatMoney(closeSummary.finalBalanceExpected)}</p>
              <p>Contado: {formatMoney(closeSummary.finalBalanceEncounted)}</p>
              <p>Diferencia: {formatMoney(closeSummary.difference)}</p>
            </div>
          )}

          <button 
            disabled={cart.length === 0 || isProcessing}
            onClick={handleFinalizeSale}
            className="w-full bg-blue-600 hover:bg-blue-700 text-white font-semibold py-3 rounded-xl shadow-md transition-all disabled:opacity-50 disabled:cursor-not-allowed active:scale-[0.98]"
          >
            {isProcessing ? 'Procesando...' : 'Finalizar Venta'}
          </button>

          {lastTicketData && (
            <button
              type="button"
              onClick={() => printTicket(lastTicketData)}
              className="w-full rounded-xl border border-blue-300 bg-white py-2.5 font-semibold text-blue-700 transition-all hover:bg-blue-50"
            >
              Imprimir Ticket
            </button>
          )}
        </div>
      </div>

      {isCloseModalOpen && (
        <CloseCashierModal
          isLoading={isClosingSession}
          onClose={() => setIsCloseModalOpen(false)}
          onSubmit={handleCloseSession}
        />
      )}

      {ticketToPrint && (
        <div className="print-ticket-container">
          <TicketTemplate ticket={ticketToPrint} />
        </div>
      )}
    </div>
      )}
    </div>
  );
};
