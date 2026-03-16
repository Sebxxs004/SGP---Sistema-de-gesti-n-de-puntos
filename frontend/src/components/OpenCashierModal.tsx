import { useState } from 'react';

interface OpenCashierModalProps {
  isLoading: boolean;
  onSubmit: (initialBalance: number) => Promise<void>;
}

export const OpenCashierModal = ({ isLoading, onSubmit }: OpenCashierModalProps) => {
  const [initialBalance, setInitialBalance] = useState('0');

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    const parsed = Number.parseFloat(initialBalance);
    if (!Number.isFinite(parsed) || parsed < 0) {
      return;
    }

    await onSubmit(parsed);
  };

  return (
    <div className="w-full max-w-md rounded-xl border border-gray-200 bg-white p-6 shadow-sm">
      <h2 className="text-lg font-semibold text-gray-900">Apertura de Caja</h2>
      <p className="mt-1 text-sm text-gray-500">Ingresa el monto inicial para abrir tu turno.</p>

      <form className="mt-5 space-y-4" onSubmit={handleSubmit}>
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">Monto inicial</label>
          <input
            type="number"
            min="0"
            step="0.01"
            value={initialBalance}
            onChange={(e) => setInitialBalance(e.target.value)}
            className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
            placeholder="0.00"
            required
          />
        </div>

        <button
          type="submit"
          disabled={isLoading}
          className="w-full rounded-lg bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700 disabled:opacity-70"
        >
          {isLoading ? 'Abriendo...' : 'Abrir Caja'}
        </button>
      </form>
    </div>
  );
};
