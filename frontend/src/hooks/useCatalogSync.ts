import { useEffect, useRef, useState } from 'react';
import { useAuthStore } from '../store/useAuthStore';
import { getCatalogLastSyncAt, syncCatalog } from '../services/CatalogSyncService';

export function useCatalogSync() {
  const currentBranchId = useAuthStore(state => state.currentBranchId);
  const [isCatalogSyncing, setIsCatalogSyncing] = useState(false);
  const [lastCatalogSyncAt, setLastCatalogSyncAt] = useState<string | null>(null);
  const syncInFlight = useRef(false);

  const runCatalogSync = async () => {
    if (!currentBranchId || syncInFlight.current) {
      return;
    }

    try {
      syncInFlight.current = true;
      setIsCatalogSyncing(true);

      const lastSyncDate = await getCatalogLastSyncAt(currentBranchId);
      const result = await syncCatalog(currentBranchId, lastSyncDate ?? undefined);
      setLastCatalogSyncAt(result.syncedAtUtc);
    } catch (error) {
      console.error('Catalog sync failed:', error);
    } finally {
      syncInFlight.current = false;
      setIsCatalogSyncing(false);
    }
  };

  useEffect(() => {
    if (!currentBranchId) {
      setLastCatalogSyncAt(null);
      return;
    }

    const bootstrap = async () => {
      const lastSyncDate = await getCatalogLastSyncAt(currentBranchId);
      setLastCatalogSyncAt(lastSyncDate);

      if (navigator.onLine) {
        await runCatalogSync();
      }
    };

    const handleOnline = () => {
      runCatalogSync();
    };

    bootstrap();
    window.addEventListener('online', handleOnline);

    const intervalId = window.setInterval(() => {
      if (navigator.onLine) {
        runCatalogSync();
      }
    }, 60000);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.clearInterval(intervalId);
    };
  }, [currentBranchId]);

  return {
    isCatalogSyncing,
    lastCatalogSyncAt,
    runCatalogSync,
  };
}
