import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import apiClient from '../api/apiClient';
import { useAuthStore } from '../store/useAuthStore';
import { OpenCashierModal } from '../components/OpenCashierModal';

interface ActiveSessionResponse {
  success: boolean;
  data: {
    id: string;
  } | null;
}

export const CashOpening = () => {
  const navigate = useNavigate();
  const currentSessionId = useAuthStore((state) => state.currentSessionId);
  const setCurrentSessionId = useAuthStore((state) => state.setCurrentSessionId);

  const [isLoading, setIsLoading] = useState(false);
  const [isChecking, setIsChecking] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    const loadActiveSession = async () => {
      try {
        const response = await apiClient.get<ActiveSessionResponse>('/sales/sessions/active');
        const activeSession = response.data.data;
        if (activeSession?.id) {
          setCurrentSessionId(activeSession.id);
          navigate('/pos', { replace: true });
        }
      } catch {
        // If there is no active session or request fails, user can open a new one.
      } finally {
        setIsChecking(false);
      }
    };

    if (currentSessionId) {
      navigate('/pos', { replace: true });
      return;
    }

    void loadActiveSession();
  }, [currentSessionId, navigate, setCurrentSessionId]);

  const handleOpenSession = async (initialBalance: number) => {
    setErrorMessage(null);
    setIsLoading(true);

    try {
      const response = await apiClient.post('/sales/sessions', {
        branchId: '00000000-0000-0000-0000-000000000000',
        initialAmount: initialBalance,
      });

      const sessionId = (response.data as { data?: { id?: string } }).data?.id;
      if (sessionId) {
        setCurrentSessionId(sessionId);
      }

      navigate('/pos', { replace: true });
    } catch (error: unknown) {
      const message =
        typeof error === 'object' && error !== null && 'response' in error
          ? (error as { response?: { data?: { error?: { message?: string } } } }).response?.data?.error?.message
          : undefined;

      setErrorMessage(message ?? 'No fue posible abrir la caja.');
    } finally {
      setIsLoading(false);
    }
  };

  if (isChecking) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center text-sm text-gray-500">
        Verificando sesión activa...
      </div>
    );
  }

  return (
    <div className="flex min-h-[70vh] items-center justify-center p-4">
      <div className="w-full max-w-md space-y-4">
        {errorMessage && (
          <div className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
            {errorMessage}
          </div>
        )}

        <OpenCashierModal isLoading={isLoading} onSubmit={handleOpenSession} />
      </div>
    </div>
  );
};
