import { useState } from 'react';

interface CloseCashierModalProps {
  isLoading: boolean;
  currencySymbol: string;
  breakdown: {
    initialBalance: number;
    cashSalesTotal: number;
    cashRefundsTotal: number;
    manualCashInTotal: number;
    manualCashOutTotal: number;
    finalBalanceExpected: number;
  };
  onClose: () => void;
  onSubmit: (finalBalanceEncounted: number) => Promise<void>;
}

const formatMoney = (value: number, currencySymbol: string) => {
  return `${currencySymbol}${value.toLocaleString('es-CO', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
};

export const CloseCashierModal = ({ isLoading, currencySymbol, breakdown, onClose, onSubmit }: CloseCashierModalProps) => {
  const [finalBalanceEncounted, setFinalBalanceEncounted] = useState('0');

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    const parsed = Number.parseFloat(finalBalanceEncounted);
    if (!Number.isFinite(parsed) || parsed < 0) {
      return;
    }

    await onSubmit(parsed);
  };

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-md rounded-xl bg-white p-6 shadow-lg">
        <h3 className="text-lg font-semibold text-gray-900">Cerrar Turno</h3>
        <p className="mt-1 text-sm text-gray-500">Ingresa el monto final contado en caja.</p>

        <div className="mt-4 rounded-lg border border-blue-100 bg-blue-50 p-3 text-sm text-blue-900">
          <p className="font-semibold">Resumen esperado</p>
          <div className="mt-2 space-y-1">
            <div className="flex items-center justify-between">
              <span>Fondo inicial</span>
              <span>{formatMoney(breakdown.initialBalance, currencySymbol)}</span>
            </div>
            <div className="flex items-center justify-between">
              <span>(+) Ventas en efectivo</span>
              <span>{formatMoney(breakdown.cashSalesTotal, currencySymbol)}</span>
            </div>
            <div className="flex items-center justify-between">
              <span>(-) Devoluciones</span>
              <span>{formatMoney(breakdown.cashRefundsTotal, currencySymbol)}</span>
            </div>
            <div className="flex items-center justify-between">
              <span>(+) Entradas manuales</span>
              <span>{formatMoney(breakdown.manualCashInTotal, currencySymbol)}</span>
            </div>
            <div className="flex items-center justify-between">
              <span>(-) Salidas manuales</span>
              <span>{formatMoney(breakdown.manualCashOutTotal, currencySymbol)}</span>
            </div>
            <div className="mt-2 border-t border-blue-200 pt-2 flex items-center justify-between font-semibold">
              <span>= Total esperado en caja</span>
              <span>{formatMoney(breakdown.finalBalanceExpected, currencySymbol)}</span>
            </div>
          </div>
        </div>

        <form className="mt-5 space-y-4" onSubmit={handleSubmit}>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">Monto contado</label>
            <input
              type="number"
              min="0"
              step="0.01"
              value={finalBalanceEncounted}
              onChange={(e) => setFinalBalanceEncounted(e.target.value)}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
              placeholder="0.00"
              required
            />
          </div>

          <div className="flex gap-3">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 rounded-lg border border-gray-300 px-4 py-2 text-gray-700 hover:bg-gray-50"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={isLoading}
              className="flex-1 rounded-lg bg-red-600 px-4 py-2 font-medium text-white hover:bg-red-700 disabled:opacity-70"
            >
              {isLoading ? 'Cerrando...' : 'Confirmar Cierre'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
