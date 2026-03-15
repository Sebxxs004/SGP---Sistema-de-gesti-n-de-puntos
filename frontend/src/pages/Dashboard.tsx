import React, { useEffect, useState } from 'react';
import { useAuthStore } from '../store/useAuthStore';
import { db } from '../db/db';
import { Activity, Clock } from 'lucide-react';

export const Dashboard = () => {
  const user = useAuthStore(state => state.user);
  const [offlineSalesCount, setOfflineSalesCount] = useState(0);

  useEffect(() => {
    const fetchOfflineSales = async () => {
      try {
        const count = await db.sales.where({ isSynced: false }).count();
        setOfflineSalesCount(count);
      } catch (error) {
        console.error("Error fetching offline sales:", error);
      }
    };

    fetchOfflineSales();
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
            Esperando sincronización
          </p>
        </div>
      </div>
    </div>
  );
};
