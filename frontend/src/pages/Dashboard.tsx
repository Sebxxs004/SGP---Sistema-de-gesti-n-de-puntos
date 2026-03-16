import { useEffect, useState } from 'react';
import { useAuthStore } from '../store/useAuthStore';
import { db } from '../db/db';
import { Activity, Clock, Receipt, TrendingUp } from 'lucide-react';
import apiClient from '../api/apiClient';
import { useCompanySettings } from '../hooks/useCompanySettings';
import { formatCurrency } from '../utils/currency';
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

interface TopProduct {
  productId: string;
  productName: string;
  quantity: number;
  amount: number;
}

interface WeeklySale {
  date: string;
  total: number;
  tickets: number;
}

interface SalesSummaryResponse {
  success: boolean;
  data: {
    totalSalesToday: number;
    ticketsToday: number;
    topProducts: TopProduct[];
    paymentDistribution: {
      cash: { amount: number; percentage: number };
      card: { amount: number; percentage: number };
    };
    weeklySales: WeeklySale[];
  };
}

export const Dashboard = () => {
  const user = useAuthStore(state => state.user);
  const isAdmin = user?.role === 'Admin';
  const currentBranchId = useAuthStore(state => state.currentBranchId);
  const [offlineSalesCount, setOfflineSalesCount] = useState(0);
  const [blockedOfflineSalesCount, setBlockedOfflineSalesCount] = useState(0);
  const [isMutatingBlockedSales, setIsMutatingBlockedSales] = useState(false);
  const [isLoadingSummary, setIsLoadingSummary] = useState(false);
  const [summaryError, setSummaryError] = useState<string | null>(null);
  const [summary, setSummary] = useState<SalesSummaryResponse['data'] | null>(null);
  const [actionMessage, setActionMessage] = useState<{ text: string; isError: boolean } | null>(null);
  const companySettingsQuery = useCompanySettings();
  const currencySymbol = companySettingsQuery.data?.currencySymbol ?? '$';

  const formatMoney = (value: number) => formatCurrency(value, currencySymbol);

  const refreshSummary = async () => {
    if (!currentBranchId) {
      setSummary(null);
      return;
    }

    setIsLoadingSummary(true);
    setSummaryError(null);

    try {
      const response = await apiClient.get<SalesSummaryResponse>('/sales/stats/summary');
      setSummary(response.data.data);
    } catch (error) {
      console.error('Error fetching summary stats:', error);
      setSummaryError('No se pudieron cargar las métricas de ventas.');
    } finally {
      setIsLoadingSummary(false);
    }
  };

  const refreshOfflineSales = async () => {
    try {
      const retryableCount = await db.sales
        .toCollection()
        .filter(sale => !sale.isSynced && !sale.isSyncBlocked)
        .count();

      const blockedCount = await db.sales
        .toCollection()
        .filter(sale => !sale.isSynced && !!sale.isSyncBlocked)
        .count();

      setOfflineSalesCount(retryableCount);
      setBlockedOfflineSalesCount(blockedCount);
    } catch (error) {
      console.error('Error fetching offline sales:', error);
      setActionMessage({ text: 'No se pudieron cargar las ventas offline.', isError: true });
    }
  };

  const handleRetryBlocked = async () => {
    setIsMutatingBlockedSales(true);
    setActionMessage(null);

    try {
      const blockedSales = await db.sales
        .toCollection()
        .filter(sale => !sale.isSynced && !!sale.isSyncBlocked)
        .toArray();

      for (const sale of blockedSales) {
        await db.sales.update(sale.id, {
          isSyncBlocked: false,
          syncError: undefined,
        });
      }

      setActionMessage({ text: `${blockedSales.length} venta(s) marcadas para reintento.`, isError: false });
      await refreshOfflineSales();
    } catch (error) {
      console.error('Error retrying blocked sales:', error);
      setActionMessage({ text: 'No se pudieron preparar las ventas para reintento.', isError: true });
    } finally {
      setIsMutatingBlockedSales(false);
    }
  };

  const handleDiscardBlocked = async () => {
    setIsMutatingBlockedSales(true);
    setActionMessage(null);

    try {
      const blockedSales = await db.sales
        .toCollection()
        .filter(sale => !sale.isSynced && !!sale.isSyncBlocked)
        .toArray();

      const blockedIds = blockedSales.map(sale => sale.id);
      if (blockedIds.length > 0) {
        await db.sales.bulkDelete(blockedIds);
      }

      setActionMessage({ text: `${blockedIds.length} venta(s) bloqueadas descartadas.`, isError: false });
      await refreshOfflineSales();
    } catch (error) {
      console.error('Error discarding blocked sales:', error);
      setActionMessage({ text: 'No se pudieron descartar las ventas bloqueadas.', isError: true });
    } finally {
      setIsMutatingBlockedSales(false);
    }
  };

  useEffect(() => {
    refreshSummary();
    refreshOfflineSales();

    const intervalId = window.setInterval(() => {
      refreshSummary();
      refreshOfflineSales();
    }, 30000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [currentBranchId]);

  const weeklyChartData = (summary?.weeklySales ?? []).map(item => ({
    ...item,
    label: new Date(item.date).toLocaleDateString('es-CO', { weekday: 'short' }),
  }));

  const paymentChartData = summary
    ? [
        { name: 'Efectivo', value: Number(summary.paymentDistribution.cash.amount.toFixed(2)) },
        { name: 'Tarjeta', value: Number(summary.paymentDistribution.card.amount.toFixed(2)) },
      ]
    : [];

  const paymentColors = ['#2563eb', '#14b8a6'];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">Vista General</h1>
        {isLoadingSummary && <span className="text-sm text-blue-600">Actualizando métricas...</span>}
      </div>

      {summaryError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
          {summaryError}
        </div>
      )}

      <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
        {/* Metric Card 1 */}
        <div className="rounded-xl bg-white p-6 shadow-sm border border-gray-100">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-medium text-gray-500">Bienvenido</h3>
            <Activity className="text-primary-500" size={20} />
          </div>
          <p className="mt-2 text-2xl font-semibold text-gray-900">
            {user?.email || 'Usuario'}
          </p>
        </div>

        {/* Metric Card 2 */}
        <div className="rounded-xl bg-white p-6 shadow-sm border border-gray-100">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-medium text-gray-500">Ventas del Día</h3>
            <TrendingUp className="text-emerald-500" size={20} />
          </div>
          <p className="mt-2 text-2xl font-semibold text-gray-900">
            {summary ? formatMoney(summary.totalSalesToday) : formatMoney(0)}
          </p>
          <p className="mt-1 text-sm text-gray-500">Monto total facturado hoy</p>
        </div>

        {/* Metric Card 3 */}
        <div className="rounded-xl bg-white p-6 shadow-sm border border-gray-100">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-medium text-gray-500">Tickets del Día</h3>
            <Receipt className="text-indigo-500" size={20} />
          </div>
          <p className="mt-2 text-2xl font-semibold text-gray-900">
            {summary?.ticketsToday ?? 0}
          </p>
          <p className="mt-1 text-sm text-gray-500">Ventas registradas en la jornada</p>
        </div>

        {/* Metric Card 4 */}
        <div className="rounded-xl bg-white p-6 shadow-sm border border-gray-100">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-medium text-gray-500">Ventas Offline Pendientes</h3>
            <Clock className="text-amber-500" size={20} />
          </div>
          <p className="mt-2 text-2xl font-semibold text-gray-900">
            {offlineSalesCount}
          </p>
          <p className="mt-1 text-sm text-gray-500">
            {blockedOfflineSalesCount > 0
              ? `${blockedOfflineSalesCount} bloqueada(s) por error de validación`
              : 'Esperando sincronización'}
          </p>
          {isAdmin && blockedOfflineSalesCount > 0 && (
            <div className="mt-3 flex flex-wrap gap-2">
              <button
                type="button"
                disabled={isMutatingBlockedSales}
                onClick={handleRetryBlocked}
                className="rounded-md border border-blue-200 bg-blue-50 px-2.5 py-1 text-xs font-medium text-blue-700 hover:bg-blue-100 disabled:cursor-not-allowed disabled:opacity-60"
              >
                Reintentar bloqueadas
              </button>
              <button
                type="button"
                disabled={isMutatingBlockedSales}
                onClick={handleDiscardBlocked}
                className="rounded-md border border-red-200 bg-red-50 px-2.5 py-1 text-xs font-medium text-red-700 hover:bg-red-100 disabled:cursor-not-allowed disabled:opacity-60"
              >
                Descartar bloqueadas
              </button>
            </div>
          )}
          {actionMessage && (
            <p className={`mt-2 text-xs ${actionMessage.isError ? 'text-red-600' : 'text-green-600'}`}>
              {actionMessage.text}
            </p>
          )}
        </div>
      </div>

      {isAdmin && (
        <div className="grid gap-6 lg:grid-cols-3">
        <div className="rounded-xl border border-gray-100 bg-white p-6 shadow-sm lg:col-span-2">
          <h3 className="mb-4 text-lg font-semibold text-gray-800">Ventas Últimos 7 Días</h3>
          <div className="h-72">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={weeklyChartData}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="label" />
                <YAxis />
                <Tooltip
                  formatter={(value) => [formatMoney(Number(value ?? 0)), 'Ventas']}
                  labelFormatter={(label) => `Día: ${label}`}
                />
                <Bar dataKey="total" fill="#2563eb" radius={[6, 6, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="rounded-xl border border-gray-100 bg-white p-6 shadow-sm">
          <h3 className="mb-4 text-lg font-semibold text-gray-800">Métodos de Pago (Hoy)</h3>
          <div className="h-72">
            <ResponsiveContainer width="100%" height="100%">
              <PieChart>
                <Pie
                  data={paymentChartData}
                  dataKey="value"
                  nameKey="name"
                  innerRadius={65}
                  outerRadius={95}
                  paddingAngle={4}
                >
                  {paymentChartData.map((entry, index) => (
                    <Cell key={`cell-${entry.name}`} fill={paymentColors[index % paymentColors.length]} />
                  ))}
                </Pie>
                <Tooltip formatter={(value) => [formatMoney(Number(value ?? 0)), 'Monto']} />
              </PieChart>
            </ResponsiveContainer>
          </div>
          <div className="mt-2 space-y-1 text-sm text-gray-600">
            <p>Efectivo: {summary?.paymentDistribution.cash.percentage ?? 0}%</p>
            <p>Tarjeta: {summary?.paymentDistribution.card.percentage ?? 0}%</p>
          </div>
        </div>
        </div>
      )}

      {isAdmin && (
        <div className="rounded-xl border border-gray-100 bg-white p-6 shadow-sm">
        <h3 className="mb-4 text-lg font-semibold text-gray-800">Top 5 Productos Más Vendidos (Hoy)</h3>
        {summary && summary.topProducts.length > 0 ? (
          <div className="space-y-3">
            {summary.topProducts.map((product, index) => (
              <div key={product.productId} className="flex items-center justify-between rounded-lg border border-gray-100 px-4 py-3">
                <div>
                  <p className="text-sm font-semibold text-gray-800">#{index + 1} {product.productName}</p>
                  <p className="text-xs text-gray-500">{product.productId}</p>
                </div>
                <div className="text-right">
                  <p className="text-sm font-semibold text-gray-800">{product.quantity} und</p>
                  <p className="text-xs text-gray-500">{formatMoney(product.amount)}</p>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-sm text-gray-500">Sin ventas registradas hoy para mostrar ranking.</p>
        )}
        </div>
      )}
    </div>
  );
};
