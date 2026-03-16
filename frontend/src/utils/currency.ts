export const formatCurrency = (value: number, currencySymbol = '$') => {
  const normalized = Number.isFinite(value) ? value : 0;
  const amount = new Intl.NumberFormat('es-CO', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(normalized);

  return `${currencySymbol}${amount}`;
};
