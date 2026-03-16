import { useEffect, useRef, useState } from 'react';
import { db } from '../db/db';
import apiClient from '../api/apiClient';
import { isAxiosError } from 'axios';

export function useSyncOfflineSales() {
  const [isOnline, setIsOnline] = useState(navigator.onLine);
  const [isSyncing, setIsSyncing] = useState(false);
  const isSyncingRef = useRef(false);

  useEffect(() => {
    const handleOnline = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, []);

  useEffect(() => {
    const syncSales = async () => {
      if (!isOnline || isSyncingRef.current) return;

      try {
        isSyncingRef.current = true;
        setIsSyncing(true);
        // Find all sales that haven't been synced yet
        const pendingSales = await db.sales
          .toCollection()
          .filter(sale => !sale.isSynced && !sale.isSyncBlocked)
          .toArray();

        if (pendingSales.length === 0) {
          setIsSyncing(false);
          return;
        }

        console.log(`Buscando sincronizar ${pendingSales.length} ventas locales...`);

        for (const sale of pendingSales) {
          try {
            // Remove the IndexedDb specific tracking fields before sending
            const { isSynced, isSyncBlocked, syncError, ...apiPayload } = sale;
            
            await apiClient.post('/sales', apiPayload, {
              headers: {
                'X-Branch-Id': sale.branchId,
              },
            });

            // Mark as synced locally
            await db.sales.update(sale.id, {
              isSynced: true,
              isSyncBlocked: false,
              syncError: undefined
            });
            console.log(`Venta ${sale.id} sincronizada con éxito.`);
          } catch (error: unknown) {
            console.error(`Error sincronizando venta ${sale.id}:`, error);

            const status = isAxiosError(error) ? error.response?.status : undefined;
            const isPermanentClientError = !!status && status >= 400 && status < 500 && status !== 408 && status !== 429;
            const errorMessage = isAxiosError(error)
              ? error.response?.data?.error?.message || error.message
              : 'Error desconocido al sincronizar venta offline.';

            await db.sales.update(sale.id, {
              isSyncBlocked: isPermanentClientError,
              syncError: isPermanentClientError
                ? `[BLOQUEADA] ${errorMessage}`
                : errorMessage,
            });
          }
        }
      } catch (error) {
         console.error("Error global durante sincronización:", error);
      } finally {
        isSyncingRef.current = false;
        setIsSyncing(false);
      }
    };

    if (!isOnline) {
      return;
    }

    // Initial attempt when hook mounts or connectivity becomes online.
    syncSales();

    // Keep retrying periodically for transient failures while online.
    const intervalId = window.setInterval(() => {
      syncSales();
    }, 30000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [isOnline]);

  return { isOnline, isSyncing };
}
