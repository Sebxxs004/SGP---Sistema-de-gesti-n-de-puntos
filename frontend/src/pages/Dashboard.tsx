import { useEffect, useState } from 'react';
import { useAuthStore } from '../store/useAuthStore';
import { db } from '../db/db';
import { Activity, Clock } from 'lucide-react';

export const Dashboard = () => {
  const user = useAuthStore(state => state.user);
  const [offlineSalesCount, setOfflineSalesCount] = useState(0);
  const [blockedOfflineSalesCount, setBlockedOfflineSalesCount] = useState(0);
  const [isMutatingBlockedSales, setIsMutatingBlockedSales] = useState(false);
  const [actionMessage, setActionMessage] = useState<{ text: string; isError: boolean } | null>(null);

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
    refreshOfflineSales();

    const intervalId = window.setInterval(() => {
      refreshOfflineSales();
    }, 5000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, []);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">Vista General</h1>
      </div>

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
          {blockedOfflineSalesCount > 0 && (
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
    </div>
  );
};
