import { useEffect, useState } from 'react';
import { db } from '../db/db';
import apiClient from '../api/apiClient';

export function useSyncOfflineSales() {
  const [isOnline, setIsOnline] = useState(navigator.onLine);
  const [isSyncing, setIsSyncing] = useState(false);

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
      if (!isOnline || isSyncing) return;

      try {
        setIsSyncing(true);
        // Find all sales that haven't been synced yet
        const pendingSales = await db.sales.where('isSynced').equals(0).toArray();

        if (pendingSales.length === 0) {
          setIsSyncing(false);
          return;
        }

        console.log(`Buscando sincronizar ${pendingSales.length} ventas locales...`);

        for (const sale of pendingSales) {
          try {
            // Remove the IndexedDb specific tracking fields before sending
            const { isSynced, syncError, ...apiPayload } = sale;
            
            await apiClient.post('/sales', apiPayload);

            // Mark as synced locally
            await db.sales.update(sale.id, {
              isSynced: true,
              syncError: undefined
            });
            console.log(`Venta ${sale.id} sincronizada con éxito.`);
          } catch (error: any) {
            console.error(`Error sincronizando venta ${sale.id}:`, error);
            await db.sales.update(sale.id, {
               syncError: error.response?.data?.error?.message || error.message 
            });
          }
        }
      } catch (error) {
         console.error("Error global durante sincronización:", error);
      } finally {
        setIsSyncing(false);
      }
    };

    if (isOnline) {
      syncSales();
    }
  }, [isOnline]);

  return { isOnline, isSyncing };
}
