import { formatCurrency } from '../utils/currency';

export interface TicketLineItem {
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  subTotal: number;
}

export interface TicketPayment {
  method: string;
  amount: number;
}

export interface TicketData {
  saleId: string;
  ticketNumber: string;
  issuedAt: string;
  company: {
    id: string;
    name: string;
    taxId: string;
    thankYouMessage?: string;
    taxPercentage?: number;
    currencySymbol?: string;
  };
  branch: {
    id: string;
    name: string;
    address: string;
    phone?: string;
  };
  cashier: {
    id: string;
    email: string;
  };
  items: TicketLineItem[];
  payments: TicketPayment[];
  subTotal: number;
  tax: number;
  total: number;

  discount?: number; // Optional: discount applied to sale
}

interface TicketTemplateProps {
  ticket: TicketData;
}

export const TicketTemplate = ({ ticket }: TicketTemplateProps) => {
  const issuedAt = new Date(ticket.issuedAt);
  const currencySymbol = ticket.company.currencySymbol ?? '$';
  const taxPercentage = ticket.company.taxPercentage ?? 16;

  return (
    <article className="ticket-print-root mx-auto w-[80mm] bg-white px-2 py-2 text-[11px] text-black">
      <header className="border-b border-dashed border-black pb-2 text-center">
        <h1 className="text-[14px] font-bold tracking-wide">{ticket.company.name || 'SGP'}</h1>
        <p className="text-[10px]">NIT: {ticket.company.taxId || 'N/A'}</p>
        <p className="text-[10px]">Sucursal: {ticket.branch.name}</p>
        {ticket.branch.address && <p className="text-[10px]">{ticket.branch.address}</p>}
        {ticket.branch.phone && <p className="text-[10px]">Tel: {ticket.branch.phone}</p>}
      </header>

      <section className="border-b border-dashed border-black py-2 text-[10px]">
        <p>Ticket: {ticket.ticketNumber}</p>
        <p>Fecha: {issuedAt.toLocaleDateString()} {issuedAt.toLocaleTimeString()}</p>
        <p>Cajero: {ticket.cashier.email}</p>
      </section>

      <section className="border-b border-dashed border-black py-2">
        <div className="mb-1 flex text-[10px] font-semibold">
          <span className="flex-1">Producto</span>
          <span className="w-10 text-right">Cant</span>
          <span className="w-16 text-right">Total</span>
        </div>
        {ticket.items.map((item) => (
          <div key={`${ticket.saleId}-${item.productId}`} className="mb-1 text-[10px]">
            <div className="flex">
              <span className="flex-1 pr-1">{item.productName}</span>
              <span className="w-10 text-right">{item.quantity}</span>
              <span className="w-16 text-right">{formatCurrency(item.subTotal, currencySymbol)}</span>
            </div>
            <div className="text-right text-[9px] text-gray-700">{formatCurrency(item.unitPrice, currencySymbol)} c/u</div>
          </div>
        ))}
      </section>

      <section className="space-y-1 border-b border-dashed border-black py-2 text-[10px]">
        <div className="flex justify-between">
          <span>Subtotal</span>
          <span>{formatCurrency(ticket.subTotal, currencySymbol)}</span>
        </div>
        {ticket.discount && ticket.discount > 0 && (
          <div className="flex justify-between font-semibold text-orange-600">
            <span>Descuento</span>
            <span>-{formatCurrency(ticket.discount, currencySymbol)}</span>
          </div>
        )}
        <div className="flex justify-between">
          <span>Impuestos ({taxPercentage.toFixed(2)}%)</span>
          <span>{formatCurrency(ticket.tax, currencySymbol)}</span>
        </div>
        <div className="flex justify-between text-[12px] font-bold">
          <span>TOTAL</span>
          <span>{formatCurrency(ticket.total, currencySymbol)}</span>
        </div>
      </section>

      <section className="border-b border-dashed border-black py-2 text-[10px]">
        <p className="mb-1 font-semibold">Pagos</p>
        {ticket.payments.map((payment, index) => (
          <div key={`${ticket.saleId}-payment-${index}`} className="flex justify-between">
            <span>{payment.method}</span>
            <span>{formatCurrency(payment.amount, currencySymbol)}</span>
          </div>
        ))}
      </section>

      <footer className="pt-2 text-center text-[10px]">
        <p>{ticket.company.thankYouMessage || 'Gracias por su compra'}</p>
        <p>Sistema SGP</p>
      </footer>
    </article>
  );
};
