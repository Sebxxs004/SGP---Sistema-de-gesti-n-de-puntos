import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import apiClient from '../api/apiClient';
import { useAuthStore } from '../store/useAuthStore';
import { Building2, Lock, Mail } from 'lucide-react';

export const Login = () => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [tenantId, setTenantIdInput] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  
  const navigate = useNavigate();
  const setCredentials = useAuthStore((state) => state.setCredentials);

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsLoading(true);

    try {
      // Inject the Tenant-Id locally for this one request before it's globally stored
      const response = await apiClient.post(
        '/auth/login', 
        { email, password },
        { headers: { 'X-Tenant-Id': tenantId } }
      );

      const token = response.data.data.token;
      
      // We simulate decoding or getting the User info if it was returned
      // Since our endpoint returns just the token, we decode or fake the user context
      const user = { id: "user-uuid", email }; 
      
      // Save globally
      setCredentials(token, tenantId, user);
      
      navigate('/');
    } catch (err: any) {
      console.error("Login failed", err);
      setError(
        err.response?.data?.error?.message || 
        "Error al iniciar sesión. Verifica tus credenciales o el Tenant."
      );
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50 p-4">
      <div className="w-full max-w-md rounded-2xl bg-white p-8 shadow-xl">
        <div className="mb-8 text-center">
          <h1 className="text-3xl font-bold text-gray-900">Bienvenido a SGP</h1>
          <p className="mt-2 text-sm text-gray-500">Ingresa tus credenciales para continuar</p>
        </div>

        {error && (
          <div className="mb-6 rounded-lg bg-red-50 p-4 text-sm text-red-600">
            {error}
          </div>
        )}

        <form onSubmit={handleLogin} className="space-y-6">
          <div>
            <label className="mb-2 block text-sm font-medium text-gray-700">ID de Inquilino (Tenant)</label>
            <div className="relative">
              <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
                <Building2 size={20} className="text-gray-400" />
              </div>
              <input
                type="text"
                required
                className="w-full rounded-lg border border-gray-300 py-2 pl-10 pr-3 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                placeholder="00000000-0000-0000-0000-000000000000"
                value={tenantId}
                onChange={(e) => setTenantIdInput(e.target.value)}
              />
            </div>
            <p className="mt-1 text-xs text-gray-400">UUID proporcionado por el administrador</p>
          </div>

          <div>
            <label className="mb-2 block text-sm font-medium text-gray-700">Correo Electrónico</label>
            <div className="relative">
              <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
                <Mail size={20} className="text-gray-400" />
              </div>
              <input
                type="email"
                required
                className="w-full rounded-lg border border-gray-300 py-2 pl-10 pr-3 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                placeholder="tu@empresa.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
          </div>

          <div>
            <label className="mb-2 block text-sm font-medium text-gray-700">Contraseña</label>
            <div className="relative">
              <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
                <Lock size={20} className="text-gray-400" />
              </div>
              <input
                type="password"
                required
                className="w-full rounded-lg border border-gray-300 py-2 pl-10 pr-3 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
                placeholder="••••••••"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={isLoading}
            className="w-full rounded-lg bg-blue-600 px-4 py-2 text-center text-white font-medium hover:bg-blue-700 focus:outline-none focus:ring-4 focus:ring-blue-300 disabled:opacity-70 transition-all"
          >
            {isLoading ? 'Autenticando...' : 'Iniciar Sesión'}
          </button>
        </form>
      </div>
    </div>
  );
};
