import { Outlet, Link, useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/useAuthStore';
import { LayoutDashboard, Package, ShoppingCart, LogOut, Menu, Users, Settings } from 'lucide-react';
import { useState } from 'react';
import { useSyncOfflineSales } from '../hooks/useSyncOfflineSales';
import { useCatalogSync } from '../hooks/useCatalogSync';

export const MainLayout = () => {
  const { user, branches, currentBranchId, setCurrentBranchId, logout } = useAuthStore();
  const location = useLocation();
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const isAdmin = user?.role === 'Admin';
  
  // Initialize sync hook. It listens to online/offline globally.
  const { isSyncing } = useSyncOfflineSales();
  const { isCatalogSyncing } = useCatalogSync();

  const navigation = isAdmin
    ? [
        { name: 'Dashboard', href: '/', icon: LayoutDashboard },
        { name: 'Inventario', href: '/inventory', icon: Package },
        { name: 'POS Ventas', href: '/pos', icon: ShoppingCart },
        { name: 'Usuarios', href: '/users', icon: Users },
        { name: 'Configuracion', href: '/settings', icon: Settings },
      ]
    : [
        { name: 'Dashboard', href: '/', icon: LayoutDashboard },
        { name: 'Inventario', href: '/inventory', icon: Package },
        { name: 'POS Ventas', href: '/pos', icon: ShoppingCart },
      ];

  return (
    <div className="flex h-screen overflow-hidden bg-gray-100">
      {/* Mobile sidebar overlay */}
      {isMobileMenuOpen && (
        <div 
          className="fixed inset-0 z-20 bg-black/50 lg:hidden"
          onClick={() => setIsMobileMenuOpen(false)}
        />
      )}

      {/* Sidebar */}
      <aside className={`fixed inset-y-0 left-0 z-30 w-64 transform bg-gray-900 text-white transition-transform duration-300 lg:static lg:translate-x-0 ${isMobileMenuOpen ? 'translate-x-0' : '-translate-x-full'}`}>
        <div className="flex h-16 items-center justify-center border-b border-gray-800">
          <h1 className="text-2xl font-bold text-primary-500">SGP</h1>
        </div>
        
        <nav className="mt-6 flex flex-col gap-2 px-4">
          {navigation.map((item) => {
            const isActive = location.pathname === item.href;
            const Icon = item.icon;
            
            return (
              <Link
                key={item.name}
                to={item.href}
                onClick={() => setIsMobileMenuOpen(false)}
                className={`flex items-center gap-3 rounded-lg px-4 py-3 transition-colors ${
                  isActive 
                    ? 'bg-primary-600 text-white' 
                    : 'text-gray-400 hover:bg-gray-800 hover:text-white'
                }`}
              >
                <Icon size={20} />
                <span className="font-medium">{item.name}</span>
              </Link>
            );
          })}
        </nav>
      </aside>

      {/* Main Content */}
      <div className="flex flex-1 flex-col overflow-hidden">
        {/* Top Header */}
        <header className="flex h-16 items-center justify-between bg-white px-6 shadow-sm">
          <button 
            className="p-1 text-gray-500 lg:hidden"
            onClick={() => setIsMobileMenuOpen(true)}
          >
            <Menu size={24} />
          </button>
          
          <div className="ml-auto flex items-center gap-4">
            {user?.role === 'Admin' && branches.length > 1 && (
              <select
                value={currentBranchId ?? ''}
                onChange={(e) => setCurrentBranchId(e.target.value)}
                className="hidden rounded-lg border border-gray-300 bg-white px-3 py-1.5 text-sm text-gray-700 outline-none focus:border-blue-500 lg:block"
                aria-label="Sucursal activa"
              >
                {branches.map((branch) => (
                  <option key={branch.id} value={branch.id}>
                    {branch.name}
                  </option>
                ))}
              </select>
            )}

            {isSyncing && (
              <span className="text-xs font-medium text-blue-600 animate-pulse bg-blue-50 px-2.5 py-1 rounded-full border border-blue-200 hidden sm:inline-block">
                Sincronizando ventas...
              </span>
            )}
            {isCatalogSyncing && (
              <span className="text-xs font-medium text-indigo-600 animate-pulse bg-indigo-50 px-2.5 py-1 rounded-full border border-indigo-200 hidden sm:inline-block">
                Sincronizando catalogo...
              </span>
            )}
            <span className="text-sm font-medium text-gray-700 border-l border-gray-200 pl-4">
              {user?.email || 'Usuario'}
            </span>
            <button
              onClick={logout}
              className="flex items-center gap-2 rounded-lg p-2 text-gray-500 hover:bg-gray-100 hover:text-red-600 transition-colors"
              title="Cerrar Sessión"
            >
              <LogOut size={20} />
            </button>
          </div>
        </header>

        {/* Page Content */}
        <main className="flex-1 overflow-y-auto bg-gray-50 p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
};
