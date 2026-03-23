import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { db } from '../db/db';
import type { OfflineSale, OfflineSaleDetail, OfflinePayment } from '../db/db';
import apiClient from '../api/apiClient';
import { useAuthStore } from '../store/useAuthStore';
import { Search, Plus, Minus, CreditCard, Banknote, Trash2, WifiOff, Wifi, RefreshCw, Printer, Pause, Clock3, Play, UserPlus, X, Percent } from 'lucide-react';
import { CloseCashierModal } from '../components/CloseCashierModal';
import { isAxiosError } from 'axios';
import { getCatalogLastSyncAt, getCatalogProducts, syncCatalog } from '../services/CatalogSyncService';
import type { CatalogProduct } from '../db/db';
import type { TicketData, TicketLineItem, TicketPayment } from '../components/TicketTemplate';
import { useCompanySettings } from '../hooks/useCompanySettings';
import { formatCurrency } from '../utils/currency';

interface SessionSaleHistoryItem {
  id: string;
  createdAt: string;
  subTotal: number;
  discount: number;
  tax: number;
  total: number;
  isRefunded: boolean;
  status: string;
  items: number;
  payments: Array<{ method: string; amount: number }>;
}

type SaleWorkflowStatus = 'Pending' | 'Completed' | 'Refunded';

const saleStatusToApiValue: Record<SaleWorkflowStatus, number> = {
  Pending: 0,
  Completed: 1,
  Refunded: 2,
};

interface RefundData {
  saleId: string;
  showModal: boolean;
}

interface CashMovementModalData {
  type: 'CashIn' | 'CashOut';
  amount: string;
  reason: string;
}

interface PendingSaleTicket {
  id: string;
  tenantId: string;
  sessionId: string;
  branchId: string;
  customerId?: string;
  customerName?: string;
  customerDocumentNumber?: string;
  subTotal: number;
  tax: number;
  total: number;
  discount: number;
  createdAt: string;
  details: OfflineSaleDetail[];
  payments: OfflinePayment[];
  status: SaleWorkflowStatus;
  isSynced: boolean;
  syncAction?: 'create' | 'complete';
}

interface SessionSalesHistoryResponse {
  success: boolean;
  data: {
    sessionId: string | null;
    sales: SessionSaleHistoryItem[];
  };
}

interface TicketDataResponse {
  success: boolean;
  data: TicketData;
}

interface ZReportPaymentBreakdown {
  method: string;
  amount: number;
}

interface ZReportData {
  branchId: string;
  branchName: string;
  date: string;
  generatedAt: string;
  grossSales: number;
  discounts: number;
  refunds: number;
  netSales: number;
  ticketCount: number;
  paymentBreakdown: ZReportPaymentBreakdown[];
  cashMovements: {
    cashIn: number;
    cashOut: number;
    net: number;
  };
}

interface ZReportResponse {
  success: boolean;
  data: ZReportData;
}

interface CashSessionHistoryItem {
  id: string;
  cashierName: string;
  openedAt: string;
  closedAt: string;
  expectedAmount: number;
  countedAmount: number;
  difference: number;
}

interface CashSessionsHistoryResponse {
  success: boolean;
  data: CashSessionHistoryItem[];
}

interface CloseSummaryData {
  initialBalance: number;
  cashSalesTotal: number;
  cashRefundsTotal: number;
  manualCashInTotal: number;
  manualCashOutTotal: number;
  finalBalanceExpected: number;
  finalBalanceEncounted: number;
  difference: number;
}

interface CloseSummaryPreviewResponse {
  success: boolean;
  data: {
    sessionId: string;
    initialBalance: number;
    cashSalesTotal: number;
    cashRefundsTotal: number;
    manualCashInTotal: number;
    manualCashOutTotal: number;
    finalBalanceExpected: number;
  };
}

interface PendingSaleApiResponse {
  success: boolean;
  data: Array<{
    id: string;
    sessionId: string;
    branchId: string;
    customerId?: string;
    customer?: {
      id: string;
      name: string;
      documentNumber?: string;
    } | null;
    createdAt: string;
    subTotal: number;
    discount: number;
    tax: number;
    total: number;
    details: Array<{
      id: string;
      productId: string;
      quantity: number;
      unitPrice: number;
      discountAmount?: number;
    }>;
  }>;
}

interface ActiveSessionResponse {
  success: boolean;
  data: {
    id: string;
  } | null;
}

interface CartItem {
  id: string; // ProductId
  name: string;
  price: number;
  quantity: number;
}

interface CustomerSummary {
  id: string;
  name: string;
  documentNumber?: string;
  email?: string;
  phone?: string;
  address?: string;
  isActive: boolean;
}

interface CustomersSearchResponse {
  success: boolean;
  data: {
    items: CustomerSummary[];
    page: number;
    pageSize: number;
    total: number;
    totalPages: number;
  };
}

interface QuickCustomerForm {
  name: string;
  documentNumber: string;
  email: string;
  phone: string;
  address: string;
}

interface ApiErrorPayload {
  error?: {
    code?: string;
    message?: string;
  };
}

const getApiErrorNotificationMessage = (error: unknown, fallback: string) => {
  if (!isAxiosError(error)) {
    return fallback;
  }

  const errorPayload = error.response?.data as ApiErrorPayload | undefined;
  const code = errorPayload?.error?.code;
  const message = errorPayload?.error?.message;

  if (code && message) {
    return `${message} (${code})`;
  }

  return message ?? fallback;
};

const escapeHtml = (value: string) =>
  value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');

const renderTicketHtml = (ticket: TicketData) => {
  const currencySymbol = ticket.company.currencySymbol ?? '$';
  const taxPercentage = ticket.company.taxPercentage ?? 16;
  const issuedAt = new Date(ticket.issuedAt).toLocaleString('es-CO');
  const pendingBalance = ticket.pendingBalance ?? ticket.total;

  const itemsHtml = ticket.items
    .map(
      (item) => `
      <tr>
        <td>${escapeHtml(item.productName)}</td>
        <td style="text-align:right">${item.quantity}</td>
        <td style="text-align:right">${escapeHtml(formatCurrency(item.subTotal, currencySymbol))}</td>
      </tr>
      <tr>
        <td colspan="3" style="text-align:right;color:#6b7280;font-size:10px;">${escapeHtml(formatCurrency(item.unitPrice, currencySymbol))} c/u</td>
      </tr>
    `,
    )
    .join('');

  const paymentsHtml = ticket.payments
    .map(
      (payment) => `
      <div style="display:flex;justify-content:space-between;gap:8px;">
        <span>${escapeHtml(payment.method)}</span>
        <span>${escapeHtml(formatCurrency(payment.amount, currencySymbol))}</span>
      </div>
    `,
    )
    .join('');

  return `<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Ticket ${escapeHtml(ticket.ticketNumber)}</title>
  <style>
    @page { size: 80mm auto; margin: 4mm; }
    html, body { margin: 0; padding: 0; background: #fff; }
    body { font-family: Arial, sans-serif; color: #000; font-size: 11px; }
    .ticket { width: 72mm; margin: 0 auto; }
    .center { text-align: center; }
    .divider { border-bottom: 1px dashed #111; margin: 8px 0; }
    h1 { margin: 0; font-size: 14px; }
    p { margin: 2px 0; }
    table { width: 100%; border-collapse: collapse; font-size: 10px; }
    td { padding: 2px 0; vertical-align: top; }
    .totals div { display: flex; justify-content: space-between; margin: 2px 0; }
    .total { font-size: 12px; font-weight: 700; }
  </style>
</head>
<body>
  <article class="ticket">
    <header class="center">
      <h1>${escapeHtml(ticket.company.name || 'SGP')}</h1>
      <p>NIT: ${escapeHtml(ticket.company.taxId || 'N/A')}</p>
      <p>Sucursal: ${escapeHtml(ticket.branch.name)}</p>
      ${ticket.branch.address ? `<p>${escapeHtml(ticket.branch.address)}</p>` : ''}
      ${ticket.branch.phone ? `<p>Tel: ${escapeHtml(ticket.branch.phone)}</p>` : ''}
      ${ticket.customer?.name ? `<p>Cliente: ${escapeHtml(ticket.customer.name)}</p>` : ''}
      ${ticket.customer?.documentNumber ? `<p>Doc: ${escapeHtml(ticket.customer.documentNumber)}</p>` : ''}
    </header>
    <div class="divider"></div>
    <section>
      <p>Ticket: ${escapeHtml(ticket.ticketNumber)}</p>
      <p>Fecha: ${escapeHtml(issuedAt)}</p>
      <p>Cajero: ${escapeHtml(ticket.cashier.email)}</p>
    </section>
    <div class="divider"></div>
    <section>
      <table>
        <thead>
          <tr>
            <td><strong>Producto</strong></td>
            <td style="text-align:right"><strong>Cant</strong></td>
            <td style="text-align:right"><strong>Total</strong></td>
          </tr>
        </thead>
        <tbody>${itemsHtml}</tbody>
      </table>
    </section>
    <div class="divider"></div>
    <section class="totals">
      ${ticket.isCreditSale ? '<div style="border:1px solid #111;padding:4px;text-align:center;font-weight:700;margin-bottom:4px;">VENTA A CREDITO</div>' : ''}
      <div><span>Subtotal</span><span>${escapeHtml(formatCurrency(ticket.subTotal, currencySymbol))}</span></div>
      ${ticket.discount && ticket.discount > 0 ? `<div><span>Descuento</span><span>-${escapeHtml(formatCurrency(ticket.discount, currencySymbol))}</span></div>` : ''}
      <div><span>Impuestos (${taxPercentage.toFixed(2)}%)</span><span>${escapeHtml(formatCurrency(ticket.tax, currencySymbol))}</span></div>
      <div class="total"><span>TOTAL</span><span>${escapeHtml(formatCurrency(ticket.total, currencySymbol))}</span></div>
      ${ticket.isCreditSale ? `<div class="total"><span>Saldo Pendiente</span><span>${escapeHtml(formatCurrency(pendingBalance, currencySymbol))}</span></div>` : ''}
    </section>
    <div class="divider"></div>
    <section>
      <p><strong>Pagos</strong></p>
      ${paymentsHtml}
    </section>
    <div class="divider"></div>
    <footer class="center">
      <p>${escapeHtml(ticket.company.thankYouMessage || 'Gracias por su compra')}</p>
      <p>Sistema SGP</p>
    </footer>
  </article>
</body>
</html>`;
};

const formatPaymentMethodLabel = (method: string) => {
  switch (method.toLowerCase()) {
    case 'cash':
      return 'Efectivo';
    case 'creditcard':
      return 'Tarjeta Credito';
    case 'debitcard':
      return 'Tarjeta Debito';
    case 'transfer':
      return 'Transferencia';
    case 'credit':
      return 'Credito';
    default:
      return method;
  }
};

const renderZReportHtml = (
  report: ZReportData,
  currencySymbol: string,
  companyName: string,
  taxId: string,
) => {
  const generatedAt = new Date(report.generatedAt).toLocaleString('es-CO');
  const reportDate = `${report.date} 00:00`;
  const paymentRows = report.paymentBreakdown
    .map((payment) => `
      <div style="display:flex;justify-content:space-between;gap:8px;">
        <span>${escapeHtml(formatPaymentMethodLabel(payment.method))}</span>
        <span>${escapeHtml(formatCurrency(payment.amount, currencySymbol))}</span>
      </div>
    `)
    .join('');

  return `<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Reporte de Cierre Z</title>
  <style>
    @page { size: 80mm auto; margin: 4mm; }
    html, body { margin: 0; padding: 0; background: #fff; }
    body { font-family: Arial, sans-serif; color: #000; font-size: 11px; }
    .ticket { width: 72mm; margin: 0 auto; }
    .center { text-align: center; }
    .divider { border-bottom: 1px dashed #111; margin: 8px 0; }
    h1 { margin: 0; font-size: 13px; }
    p { margin: 2px 0; }
    .row { display: flex; justify-content: space-between; gap: 8px; margin: 2px 0; }
    .strong { font-weight: 700; }
  </style>
</head>
<body>
  <article class="ticket">
    <header class="center">
      <h1>${escapeHtml(companyName || 'SGP')}</h1>
      <p>NIT: ${escapeHtml(taxId || 'N/A')}</p>
      <p>${escapeHtml(report.branchName)}</p>
    </header>
    <div class="divider"></div>
    <section class="center">
      <p class="strong">REPORTE DE CIERRE Z</p>
      <p>Fecha operativa: ${escapeHtml(reportDate)}</p>
      <p>Generado: ${escapeHtml(generatedAt)}</p>
    </section>
    <div class="divider"></div>
    <section>
      <div class="row"><span>Ventas Brutas</span><span>${escapeHtml(formatCurrency(report.grossSales, currencySymbol))}</span></div>
      <div class="row"><span>Descuentos</span><span>-${escapeHtml(formatCurrency(report.discounts, currencySymbol))}</span></div>
      <div class="row"><span>Devoluciones</span><span>-${escapeHtml(formatCurrency(report.refunds, currencySymbol))}</span></div>
      <div class="row strong"><span>Ventas Netas</span><span>${escapeHtml(formatCurrency(report.netSales, currencySymbol))}</span></div>
      <div class="row"><span>Tickets Emitidos</span><span>${report.ticketCount}</span></div>
    </section>
    <div class="divider"></div>
    <section>
      <p class="strong">Desglose de Pagos</p>
      ${paymentRows || '<p>Sin pagos registrados</p>'}
    </section>
    <div class="divider"></div>
    <section>
      <p class="strong">Movimientos de Caja</p>
      <div class="row"><span>Entradas</span><span>${escapeHtml(formatCurrency(report.cashMovements.cashIn, currencySymbol))}</span></div>
      <div class="row"><span>Salidas</span><span>${escapeHtml(formatCurrency(report.cashMovements.cashOut, currencySymbol))}</span></div>
      <div class="row strong"><span>Neto Caja</span><span>${escapeHtml(formatCurrency(report.cashMovements.net, currencySymbol))}</span></div>
    </section>
    <div class="divider"></div>
    <footer class="center">
      <p>Sistema SGP</p>
    </footer>
  </article>
</body>
</html>`;
};

export const Sales = () => {
  const [cart, setCart] = useState<CartItem[]>([]);
  const [catalog, setCatalog] = useState<CatalogProduct[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [paymentMethod, setPaymentMethod] = useState<number>(0); // 0: Cash, 1: CreditCard, 5: Credit
  const [isProcessing, setIsProcessing] = useState(false);
  const [isManualCatalogSyncing, setIsManualCatalogSyncing] = useState(false);
  const [isLoadingHistory, setIsLoadingHistory] = useState(false);
  const [isFetchingTicket, setIsFetchingTicket] = useState(false);
  const [sessionSalesHistory, setSessionSalesHistory] = useState<SessionSaleHistoryItem[]>([]);
  const [lastTicketData, setLastTicketData] = useState<TicketData | null>(null);
  const [lastCatalogSyncAt, setLastCatalogSyncAt] = useState<string | null>(null);
  const [isLoadingCashHistory, setIsLoadingCashHistory] = useState(false);
  const [cashSessionsHistory, setCashSessionsHistory] = useState<CashSessionHistoryItem[]>([]);
  const [isLoadingZReport, setIsLoadingZReport] = useState(false);
  const [zReportData, setZReportData] = useState<ZReportData | null>(null);
  const [zReportDate, setZReportDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [notification, setNotification] = useState<{message: string, isError: boolean} | null>(null);
  const [isCloseModalOpen, setIsCloseModalOpen] = useState(false);
  const [isClosingSession, setIsClosingSession] = useState(false);
  const [isOpeningCloseModal, setIsOpeningCloseModal] = useState(false);
  const [closeSummaryPreview, setCloseSummaryPreview] = useState<Omit<CloseSummaryData, 'finalBalanceEncounted' | 'difference'> | null>(null);
  const [closeSummary, setCloseSummary] = useState<CloseSummaryData | null>(null);
  const [discount, setDiscount] = useState(0);
  const [discountType, setDiscountType] = useState<'percentage' | 'fixed'>('fixed');
  const [showDiscountModal, setShowDiscountModal] = useState(false);
  const [refundData, setRefundData] = useState<RefundData | null>(null);
  const [isProcessingRefund, setIsProcessingRefund] = useState(false);
  const [refundedSales, setRefundedSales] = useState<Set<string>>(new Set());
  const [cashMovementModal, setCashMovementModal] = useState<CashMovementModalData | null>(null);
  const [isSubmittingCashMovement, setIsSubmittingCashMovement] = useState(false);
  const [pendingSales, setPendingSales] = useState<PendingSaleTicket[]>([]);
  const [isLoadingPendingSales, setIsLoadingPendingSales] = useState(false);
  const [isPendingDrawerOpen, setIsPendingDrawerOpen] = useState(false);
  const [activeHeldSaleId, setActiveHeldSaleId] = useState<string | null>(null);
  const [selectedCustomer, setSelectedCustomer] = useState<CustomerSummary | null>(null);
  const [isCustomerSelectorOpen, setIsCustomerSelectorOpen] = useState(false);
  const [customerSearchTerm, setCustomerSearchTerm] = useState('');
  const [customerOptions, setCustomerOptions] = useState<CustomerSummary[]>([]);
  const [isLoadingCustomers, setIsLoadingCustomers] = useState(false);
  const [isQuickCustomerModalOpen, setIsQuickCustomerModalOpen] = useState(false);
  const [isCreatingQuickCustomer, setIsCreatingQuickCustomer] = useState(false);
  const [quickCustomerForm, setQuickCustomerForm] = useState<QuickCustomerForm>({
    name: '',
    documentNumber: '',
    email: '',
    phone: '',
    address: '',
  });
  
  const { tenantId, currentBranchId, currentSessionId, setCurrentSessionId, branches, user } = useAuthStore();
  const isAdmin = user?.role === 'Admin';
  const [searchParams] = useSearchParams();
  const activeSubmodule: 'pos' | 'cashHistory' | 'zReport' =
    searchParams.get('tab') === 'cash'
      ? 'cashHistory'
      : isAdmin && searchParams.get('tab') === 'z'
        ? 'zReport'
        : 'pos';
  const companySettingsQuery = useCompanySettings();
  const taxPercentage = companySettingsQuery.data?.taxPercentage ?? 16;
  const currencySymbol = companySettingsQuery.data?.currencySymbol ?? '$';
  const isOnline = navigator.onLine; // For UI feedback, hook handles real sync
  const currentBranchName = branches.find((branch) => branch.id === currentBranchId)?.name ?? 'Sucursal';

  const filteredCatalog = catalog.filter(p =>
    p.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    p.sku.toLowerCase().includes(searchTerm.toLowerCase())
  );

  const refreshLocalCatalog = async () => {
    if (!currentBranchId) {
      setCatalog([]);
      setLastCatalogSyncAt(null);
      return;
    }

    const [products, lastSync] = await Promise.all([
      getCatalogProducts(currentBranchId),
      getCatalogLastSyncAt(currentBranchId),
    ]);

    setCatalog(products);
    setLastCatalogSyncAt(lastSync);
  };

  useEffect(() => {
    refreshLocalCatalog();

    const intervalId = window.setInterval(() => {
      refreshLocalCatalog();
    }, 5000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [currentBranchId]);

  useEffect(() => {
    if (!isCustomerSelectorOpen) {
      return;
    }

    const timeoutId = window.setTimeout(() => {
      loadCustomers(customerSearchTerm);
    }, 250);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [isCustomerSelectorOpen, customerSearchTerm]);

  const subTotal = cart.reduce((sum, item) => sum + (item.price * item.quantity), 0);
  const discountAmount = discountType === 'percentage' ? (subTotal * discount) / 100 : discount;
  const subTotalAfterDiscount = Math.max(subTotal - discountAmount, 0);
  const tax = subTotalAfterDiscount * (taxPercentage / 100);
  const total = subTotalAfterDiscount + tax;

  useEffect(() => {
    if (!selectedCustomer && paymentMethod === 5) {
      setPaymentMethod(0);
    }
  }, [selectedCustomer, paymentMethod]);

  const formatMoney = (value: number) => formatCurrency(value, currencySymbol);

  const resetQuickCustomerForm = () => {
    setQuickCustomerForm({
      name: '',
      documentNumber: '',
      email: '',
      phone: '',
      address: '',
    });
  };

  const loadCustomers = async (search: string) => {
    setIsLoadingCustomers(true);
    try {
      const response = await apiClient.get<CustomersSearchResponse>('/customers', {
        params: {
          page: 1,
          pageSize: 12,
          search: search.trim() || undefined,
        },
      });

      setCustomerOptions(response.data.data.items ?? []);
    } catch (error) {
      console.error('Error loading customers:', error);
      setCustomerOptions([]);
    } finally {
      setIsLoadingCustomers(false);
    }
  };

  const handleCreateQuickCustomer = async () => {
    if (!quickCustomerForm.name.trim()) {
      setNotification({ message: 'El nombre del cliente es obligatorio.', isError: true });
      setTimeout(() => setNotification(null), 4000);
      return;
    }

    setIsCreatingQuickCustomer(true);
    try {
      const response = await apiClient.post<{ success: boolean; data: CustomerSummary }>('/customers', {
        name: quickCustomerForm.name,
        documentNumber: quickCustomerForm.documentNumber || undefined,
        email: quickCustomerForm.email || undefined,
        phone: quickCustomerForm.phone || undefined,
        address: quickCustomerForm.address || undefined,
      });

      const createdCustomer = response.data.data;
      setSelectedCustomer(createdCustomer);
      setIsQuickCustomerModalOpen(false);
      resetQuickCustomerForm();
      setNotification({ message: 'Cliente creado y seleccionado en la venta.', isError: false });
      setTimeout(() => setNotification(null), 4000);
      await loadCustomers(createdCustomer.name);
    } catch (error) {
      const apiErrorMessage = isAxiosError(error) ? error.response?.data?.error?.message : undefined;
      setNotification({ message: apiErrorMessage ?? 'No se pudo crear el cliente.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsCreatingQuickCustomer(false);
    }
  };

  const buildSaleDetails = (): OfflineSaleDetail[] => cart.map(item => ({
    id: crypto.randomUUID(),
    productId: item.id,
    quantity: item.quantity,
    unitPrice: item.price,
    discountAmount: 0,
  }));

  const buildSalePayload = (
    saleId: string,
    status: SaleWorkflowStatus,
    details: OfflineSaleDetail[],
    payments: OfflinePayment[],
    createdAt = new Date().toISOString(),
    sessionIdOverride?: string,
  ): OfflineSale => ({
    id: saleId,
    tenantId: tenantId || '',
    sessionId: sessionIdOverride ?? currentSessionId ?? '',
    branchId: currentBranchId || '',
    customerId: selectedCustomer?.id,
    customerName: selectedCustomer?.name,
    customerDocumentNumber: selectedCustomer?.documentNumber,
    subTotal,
    tax,
    total,
    discount: discountAmount,
    createdAt,
    details,
    payments,
    status,
    isSynced: false,
    syncAction: 'create',
  });

  const toPendingTicket = (sale: OfflineSale): PendingSaleTicket => ({
    id: sale.id,
    tenantId: sale.tenantId,
    sessionId: sale.sessionId,
    branchId: sale.branchId,
    customerId: sale.customerId,
    customerName: sale.customerName,
    customerDocumentNumber: sale.customerDocumentNumber,
    subTotal: sale.subTotal,
    tax: sale.tax,
    total: sale.total,
    discount: sale.discount,
    createdAt: sale.createdAt,
    details: sale.details,
    payments: sale.payments,
    status: sale.status,
    isSynced: sale.isSynced,
    syncAction: sale.syncAction,
  });

  const getCartFromPendingSale = (sale: PendingSaleTicket): CartItem[] => {
    const productsById = new Map(catalog.map(product => [product.id, product]));
    return sale.details.map((detail) => {
      const product = productsById.get(detail.productId);
      return {
        id: detail.productId,
        name: product?.name ?? `Producto ${detail.productId.slice(0, 8)}`,
        price: detail.unitPrice,
        quantity: detail.quantity,
      };
    });
  };

  const buildLocalTicketData = (
    saleId: string,
    createdAt: string,
    details: OfflineSaleDetail[],
    payments: OfflinePayment[],
    saleSubTotal: number,
    saleDiscount: number,
    saleTax: number,
    saleTotal: number
  ): TicketData => {
    const detailsByProduct = new Map(details.map((detail) => [detail.productId, detail]));

    const items: TicketLineItem[] = cart
      .filter((item) => detailsByProduct.has(item.id))
      .map((item) => {
        const detail = detailsByProduct.get(item.id)!;
        return {
          productId: item.id,
          productName: item.name,
          quantity: detail.quantity,
          unitPrice: detail.unitPrice,
          subTotal: detail.quantity * detail.unitPrice,
        };
      });

    const normalizedPayments: TicketPayment[] = payments.map((payment) => ({
      method:
        payment.method === 0
          ? 'Cash'
          : payment.method === 1
            ? 'CreditCard'
            : payment.method === 5
              ? 'Credit'
              : 'Other',
      amount: payment.amount,
    }));

    return {
      saleId,
      ticketNumber: saleId.slice(0, 8).toUpperCase(),
      issuedAt: createdAt,
      company: {
        id: tenantId ?? 'N/A',
        name: companySettingsQuery.data?.name ?? 'SGP',
        taxId: companySettingsQuery.data?.taxId ?? 'N/A',
        thankYouMessage: companySettingsQuery.data?.thankYouMessage ?? 'Gracias por su compra',
        taxPercentage,
        currencySymbol,
      },
      branch: {
        id: currentBranchId ?? 'N/A',
        name: currentBranchName,
        address: '',
        phone: '',
      },
      cashier: {
        id: user?.id ?? 'N/A',
        email: user?.email ?? 'cajero@sgp.local',
      },
      customer: selectedCustomer
        ? {
            id: selectedCustomer.id,
            name: selectedCustomer.name,
            documentNumber: selectedCustomer.documentNumber,
          }
        : null,
      items,
      payments: normalizedPayments,
      isCreditSale: paymentMethod === 5,
      pendingBalance: paymentMethod === 5 ? saleTotal : 0,
      subTotal: saleSubTotal,
      discount: saleDiscount,
      tax: saleTax,
      total: saleTotal,
    };
  };

  const fetchTicketData = async (saleId: string): Promise<TicketData> => {
    const response = await apiClient.get<TicketDataResponse>(`/sales/${saleId}/ticket-data`);
    return response.data.data;
  };

  const ensureActiveSessionId = async (): Promise<string | null> => {
    if (!currentBranchId) {
      setCurrentSessionId(null);
      return null;
    }

    try {
      const response = await apiClient.get<ActiveSessionResponse>('/sales/sessions/active');
      const activeSessionId = response.data.data?.id ?? null;
      const storedSessionId = useAuthStore.getState().currentSessionId;

      if (storedSessionId !== activeSessionId) {
        setCurrentSessionId(activeSessionId);
      }

      return activeSessionId;
    } catch (error) {
      console.error('Error checking active session:', error);
      return useAuthStore.getState().currentSessionId;
    }
  };

  const printTicket = (ticket: TicketData) => {
    const frame = document.createElement('iframe');
    frame.setAttribute('aria-hidden', 'true');
    frame.style.position = 'fixed';
    frame.style.right = '0';
    frame.style.bottom = '0';
    frame.style.width = '0';
    frame.style.height = '0';
    frame.style.border = '0';

    const cleanup = () => {
      window.setTimeout(() => {
        frame.remove();
      }, 500);
    };

    frame.onload = () => {
      const printWindow = frame.contentWindow;
      if (!printWindow) {
        setNotification({ message: 'No se pudo inicializar la ventana de impresion.', isError: true });
        setTimeout(() => setNotification(null), 4000);
        cleanup();
        return;
      }

      printWindow.focus();
      printWindow.print();
      cleanup();
    };

    frame.srcdoc = renderTicketHtml(ticket);
    document.body.appendChild(frame);
  };

  const handleReprintFromHistory = async (saleId: string) => {
    setIsFetchingTicket(true);
    try {
      const ticket = await fetchTicketData(saleId);
      printTicket(ticket);
    } catch (error) {
      console.error('Error fetching ticket data:', error);
      setNotification({ message: 'No se pudo recuperar el comprobante para reimpresion.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsFetchingTicket(false);
    }
  };

  const addToCart = (product: CatalogProduct) => {
    setCart(prev => {
      const existing = prev.find(item => item.id === product.id);
      if (existing) {
        return prev.map(item => item.id === product.id ? { ...item, quantity: item.quantity + 1 } : item);
      }
      return [...prev, { id: product.id, name: product.name, price: product.price, quantity: 1 }];
    });
  };

  const updateQuantity = (id: string, delta: number) => {
    setCart(prev => prev.map(item => {
      if (item.id === id) {
        const newQ = item.quantity + delta;
        return newQ > 0 ? { ...item, quantity: newQ } : item;
      }
      return item;
    }));
  };

  const removeFromCart = (id: string) => setCart(prev => prev.filter(item => item.id !== id));

  const refreshSessionHistory = async () => {
    if (!currentSessionId) {
      setSessionSalesHistory([]);
      return;
    }

    setIsLoadingHistory(true);
    try {
      const response = await apiClient.get<SessionSalesHistoryResponse>('/sales/history/current-session');
      const sales = response.data.data.sales ?? [];
      setSessionSalesHistory(sales);
      setRefundedSales(new Set(sales.filter((sale) => sale.isRefunded).map((sale) => sale.id)));
    } catch (error) {
      console.error('Error loading session sales history:', error);
    } finally {
      setIsLoadingHistory(false);
    }
  };

  const refreshPendingSales = async () => {
    if (!currentSessionId || !currentBranchId) {
      setPendingSales([]);
      return;
    }

    setIsLoadingPendingSales(true);
    try {
      if (isOnline) {
        const response = await apiClient.get<PendingSaleApiResponse>('/sales/pending');
        const localSalesById = new Map((await db.sales.toArray()).map((sale) => [sale.id, sale]));

        for (const sale of response.data.data) {
          const localSale = localSalesById.get(sale.id);
          if (localSale && !localSale.isSynced) {
            continue;
          }

          await db.sales.put({
            id: sale.id,
            tenantId: tenantId || '',
            sessionId: sale.sessionId,
            branchId: sale.branchId,
            customerId: sale.customerId,
            customerName: sale.customer?.name,
            customerDocumentNumber: sale.customer?.documentNumber,
            subTotal: sale.subTotal,
            tax: sale.tax,
            total: sale.total,
            discount: sale.discount,
            createdAt: sale.createdAt,
            details: sale.details,
            payments: [],
            status: 'Pending',
            isSynced: true,
            syncAction: undefined,
            isSyncBlocked: false,
            syncError: undefined,
          });
        }
      }

      const localPendingSales = await db.sales
        .where('sessionId')
        .equals(currentSessionId)
        .filter((sale) => sale.branchId === currentBranchId && sale.status === 'Pending')
        .sortBy('createdAt');

      setPendingSales(localPendingSales.reverse().map(toPendingTicket));
    } catch (error) {
      console.error('Error loading pending sales:', error);
      setNotification({ message: 'No se pudieron cargar las ventas en espera.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsLoadingPendingSales(false);
    }
  };

  const handleManualCatalogSync = async () => {
    if (!currentBranchId) {
      setNotification({ message: 'Selecciona una sucursal activa para sincronizar catalogo.', isError: true });
      return;
    }

    setIsManualCatalogSyncing(true);

    try {
      const result = await syncCatalog(currentBranchId, lastCatalogSyncAt ?? undefined);
      await refreshLocalCatalog();
      setNotification({
        message: `Catalogo sincronizado (${result.productsCount} productos).`,
        isError: false,
      });
    } catch (error) {
      console.error('Manual catalog sync failed:', error);
      setNotification({ message: 'No fue posible sincronizar el catalogo.', isError: true });
    } finally {
      setIsManualCatalogSyncing(false);
      setTimeout(() => setNotification(null), 4000);
    }
  };

  useEffect(() => {
    refreshSessionHistory();

    const intervalId = window.setInterval(() => {
      refreshSessionHistory();
    }, 20000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [currentSessionId]);

  useEffect(() => {
    ensureActiveSessionId();

    const intervalId = window.setInterval(() => {
      ensureActiveSessionId();
    }, 15000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [currentBranchId]);

  useEffect(() => {
    refreshPendingSales();

    const intervalId = window.setInterval(() => {
      refreshPendingSales();
    }, 15000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [currentSessionId, currentBranchId, isOnline, tenantId, catalog.length]);

  const refreshCashSessionsHistory = async () => {
    setIsLoadingCashHistory(true);
    try {
      const response = await apiClient.get<CashSessionsHistoryResponse>('/sales/sessions/history');
      setCashSessionsHistory(response.data.data ?? []);
    } catch (error) {
      console.error('Error loading cash sessions history:', error);
      setNotification({ message: 'No se pudo cargar el historial de cajas.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsLoadingCashHistory(false);
    }
  };

  const loadZReport = async () => {
    if (!currentBranchId) {
      setNotification({ message: 'Selecciona una sucursal para generar el Cierre Z.', isError: true });
      setTimeout(() => setNotification(null), 4000);
      return;
    }

    setIsLoadingZReport(true);
    try {
      const response = await apiClient.get<ZReportResponse>('/sales/reports/z-report', {
        params: {
          branchId: currentBranchId,
          date: zReportDate || undefined,
        },
      });

      setZReportData(response.data.data);
    } catch (error) {
      console.error('Error loading z report:', error);
      const apiErrorMessage = isAxiosError(error) ? error.response?.data?.error?.message : undefined;
      setNotification({ message: apiErrorMessage ?? 'No fue posible generar el reporte Z.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsLoadingZReport(false);
    }
  };

  const printZReport = (report: ZReportData) => {
    const frame = document.createElement('iframe');
    frame.setAttribute('aria-hidden', 'true');
    frame.style.position = 'fixed';
    frame.style.right = '0';
    frame.style.bottom = '0';
    frame.style.width = '0';
    frame.style.height = '0';
    frame.style.border = '0';

    const cleanup = () => {
      window.setTimeout(() => {
        frame.remove();
      }, 500);
    };

    frame.onload = () => {
      const printWindow = frame.contentWindow;
      if (!printWindow) {
        setNotification({ message: 'No se pudo inicializar la ventana de impresion.', isError: true });
        setTimeout(() => setNotification(null), 4000);
        cleanup();
        return;
      }

      printWindow.focus();
      printWindow.print();
      cleanup();
    };

    frame.srcdoc = renderZReportHtml(
      report,
      currencySymbol,
      companySettingsQuery.data?.name ?? 'SGP',
      companySettingsQuery.data?.taxId ?? 'N/A',
    );
    document.body.appendChild(frame);
  };

  useEffect(() => {
    if (activeSubmodule === 'cashHistory') {
      refreshCashSessionsHistory();
    }
  }, [activeSubmodule, currentBranchId]);

  useEffect(() => {
    if (activeSubmodule === 'zReport') {
      loadZReport();
    }
  }, [activeSubmodule, currentBranchId]);

  const handleFinalizeSale = async () => {
    if (cart.length === 0) return;
    if (!currentBranchId) {
      setNotification({ message: 'Selecciona una sucursal activa antes de vender.', isError: true });
      return;
    }
    if (paymentMethod === 5 && !selectedCustomer) {
      setNotification({ message: 'Las ventas a crédito requieren seleccionar un cliente.', isError: true });
      return;
    }
    const activeSessionId = await ensureActiveSessionId();

    if (!activeSessionId) {
      setNotification({ message: 'No tienes una sesion de caja activa para vender.', isError: true });
      return;
    }

    setIsProcessing(true);
    setNotification(null);
    let shouldClearCart = false;

    const saleId = activeHeldSaleId ?? crypto.randomUUID();
    const details = buildSaleDetails();
    const existingHeldSale = activeHeldSaleId ? await db.sales.get(activeHeldSaleId) : undefined;

    const payments: OfflinePayment[] = [{
      id: crypto.randomUUID(),
      amount: total,
      method: paymentMethod
    }];

    const salePayload = buildSalePayload(saleId, 'Completed', details, payments, existingHeldSale?.createdAt, activeSessionId);

    try {
      if (isOnline) {
        const response = activeHeldSaleId && existingHeldSale?.isSynced
          ? await apiClient.post<{ success: boolean; data?: { id?: string } }>(`/sales/${saleId}/complete`, {
              customerId: selectedCustomer?.id,
              discount: discountAmount,
              details,
              payments,
            })
          : await apiClient.post<{ success: boolean; data?: { id?: string } }>('/sales', {
              ...salePayload,
              status: saleStatusToApiValue.Completed,
            });

        const registeredSaleId = response.data?.data?.id ?? saleId;

        try {
          const ticket = await fetchTicketData(registeredSaleId);
          setLastTicketData(ticket);
        } catch {
          setLastTicketData(buildLocalTicketData(saleId, salePayload.createdAt, details, payments, subTotal, discountAmount, tax, total));
        }

        if (activeHeldSaleId) {
          await db.sales.delete(activeHeldSaleId);
        }

        setNotification({ message: 'Venta procesada exitosamente.', isError: false });
        shouldClearCart = true;
      } else {
        await db.sales.put({
          ...salePayload,
          isSynced: false,
          syncAction: existingHeldSale?.isSynced ? 'complete' : 'create',
          isSyncBlocked: false,
          syncError: undefined,
        });
        setLastTicketData(buildLocalTicketData(saleId, salePayload.createdAt, details, payments, subTotal, discountAmount, tax, total));
        setNotification({ message: 'Sin conexion. Venta guardada localmente.', isError: false });
        shouldClearCart = true;
      }
    } catch (error: unknown) {
      const apiErrorMessage = getApiErrorNotificationMessage(error, 'No fue posible procesar la venta.');

      const isNetworkFailure =
        !navigator.onLine ||
        (isAxiosError(error) && (!error.response && (error.code === 'ERR_NETWORK' || error.code === 'ECONNABORTED')));

      // Only fallback to local persistence for real connectivity failures.
      if (isNetworkFailure) {
        try {
          await db.sales.put({
            ...salePayload,
            isSynced: false,
            syncAction: existingHeldSale?.isSynced ? 'complete' : 'create',
            isSyncBlocked: false,
            syncError: undefined,
          });
          setLastTicketData(buildLocalTicketData(saleId, salePayload.createdAt, details, payments, subTotal, discountAmount, tax, total));
          setNotification({ message: 'Sin conexion o API no disponible. Venta guardada localmente.', isError: false });
          shouldClearCart = true;
        } catch (dbError) {
          console.error('Error guardando en Dexie', dbError);
          setNotification({ message: 'No se pudo procesar ni guardar localmente la venta.', isError: true });
        }
      } else {
        setNotification({ message: apiErrorMessage, isError: true });
      }
    } finally {
      setIsProcessing(false);
      if (shouldClearCart) {
        setCart([]);
        setDiscount(0);
        setDiscountType('fixed');
        setActiveHeldSaleId(null);
        setSelectedCustomer(null);
      }
      if (shouldClearCart) {
        refreshSessionHistory();
        refreshPendingSales();
      }
      setTimeout(() => setNotification(null), 4000);
    }
  };

  const handlePutSaleOnHold = async () => {
    if (cart.length === 0) {
      return;
    }

    const activeSessionId = await ensureActiveSessionId();

    if (!currentBranchId || !activeSessionId) {
      setNotification({ message: 'Necesitas una sesion activa para poner ventas en espera.', isError: true });
      setTimeout(() => setNotification(null), 4000);
      return;
    }

    setIsProcessing(true);
    setNotification(null);

    const saleId = activeHeldSaleId ?? crypto.randomUUID();
    const details = buildSaleDetails();
    const salePayload = buildSalePayload(
      saleId,
      'Pending',
      details,
      [],
      activeHeldSaleId ? pendingSales.find((sale) => sale.id === activeHeldSaleId)?.createdAt : undefined,
      activeSessionId,
    );

    try {
      if (isOnline) {
        await apiClient.post('/sales', {
          ...salePayload,
          status: saleStatusToApiValue.Pending,
        });

        await db.sales.put({
          ...salePayload,
          isSynced: true,
          syncAction: undefined,
          isSyncBlocked: false,
          syncError: undefined,
        });
      } else {
        await db.sales.put({
          ...salePayload,
          isSynced: false,
          syncAction: 'create',
          isSyncBlocked: false,
          syncError: undefined,
        });
      }

      setCart([]);
      setDiscount(0);
      setDiscountType('fixed');
      setActiveHeldSaleId(null);
      setSelectedCustomer(null);
      setNotification({ message: 'Venta enviada a espera.', isError: false });
      await refreshPendingSales();
      setTimeout(() => setNotification(null), 4000);
    } catch (error: unknown) {
      const isNetworkFailure =
        !navigator.onLine ||
        (isAxiosError(error) && (!error.response && (error.code === 'ERR_NETWORK' || error.code === 'ECONNABORTED')));

      if (isNetworkFailure) {
        await db.sales.put({
          ...salePayload,
          isSynced: false,
          syncAction: 'create',
          isSyncBlocked: false,
          syncError: undefined,
        });
        setCart([]);
        setDiscount(0);
        setDiscountType('fixed');
        setActiveHeldSaleId(null);
        setSelectedCustomer(null);
        setNotification({ message: 'Sin conexion. Ticket guardado en espera local.', isError: false });
        await refreshPendingSales();
        setTimeout(() => setNotification(null), 4000);
      } else {
        const apiErrorMessage = getApiErrorNotificationMessage(error, 'No fue posible poner la venta en espera.');
        setNotification({ message: apiErrorMessage, isError: true });
        setTimeout(() => setNotification(null), 4000);
      }
    } finally {
      setIsProcessing(false);
    }
  };

  const handleResumePendingSale = (sale: PendingSaleTicket) => {
    setCart(getCartFromPendingSale(sale));
    setDiscount(sale.discount);
    setDiscountType('fixed');
    setActiveHeldSaleId(sale.id);
    setSelectedCustomer(
      sale.customerId
        ? {
            id: sale.customerId,
            name: sale.customerName ?? 'Cliente',
            documentNumber: sale.customerDocumentNumber,
            isActive: true,
          }
        : null,
    );
    setIsPendingDrawerOpen(false);
    setNotification({ message: `Ticket ${sale.id.slice(0, 8)} cargado en el carrito.`, isError: false });
    setTimeout(() => setNotification(null), 3000);
  };

  const handleCloseSession = async (finalBalanceEncounted: number) => {
    setIsClosingSession(true);
    setNotification(null);

    try {
      const response = await apiClient.post('/sales/sessions/close', {
        branchId: '00000000-0000-0000-0000-000000000000',
        finalBalanceEncounted,
      });

      const payload = (response.data as { data?: CloseSummaryData }).data;

      if (payload) {
        setCloseSummary(payload);
      }

      setCurrentSessionId(null);
      setIsCloseModalOpen(false);
      setNotification({ message: 'Caja cerrada correctamente.', isError: false });
    } catch (error: unknown) {
      const message =
        typeof error === 'object' && error !== null && 'response' in error
          ? (error as { response?: { data?: { error?: { message?: string } } } }).response?.data?.error?.message
          : undefined;

      setNotification({ message: message ?? 'No fue posible cerrar la caja.', isError: true });
    } finally {
      setIsClosingSession(false);
    }
  };

  const handleOpenCloseModal = async () => {
    if (!currentSessionId) {
      setNotification({ message: 'No tienes una sesion activa para cerrar.', isError: true });
      setTimeout(() => setNotification(null), 4000);
      return;
    }

    setIsOpeningCloseModal(true);
    try {
      const response = await apiClient.get<CloseSummaryPreviewResponse>('/sales/sessions/current/close-summary');
      setCloseSummaryPreview(response.data.data);
      setIsCloseModalOpen(true);
    } catch (error) {
      console.error('Error loading close summary:', error);
      const apiErrorMessage = isAxiosError(error) ? error.response?.data?.error?.message : undefined;
      setNotification({ message: apiErrorMessage ?? 'No fue posible cargar el resumen de cierre.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsOpeningCloseModal(false);
    }
  };

  const handleRegisterCashMovement = async () => {
    if (!cashMovementModal) return;

    const amount = Number.parseFloat(cashMovementModal.amount);
    if (!Number.isFinite(amount) || amount <= 0) {
      setNotification({ message: 'El monto debe ser mayor a cero.', isError: true });
      setTimeout(() => setNotification(null), 4000);
      return;
    }

    if (!cashMovementModal.reason.trim()) {
      setNotification({ message: 'Debes ingresar un motivo para el movimiento.', isError: true });
      setTimeout(() => setNotification(null), 4000);
      return;
    }

    setIsSubmittingCashMovement(true);
    try {
      await apiClient.post('/sales/sessions/current/movements', {
        type: cashMovementModal.type,
        amount,
        reason: cashMovementModal.reason.trim(),
      });

      setNotification({
        message: cashMovementModal.type === 'CashIn'
          ? 'Entrada de efectivo registrada.'
          : 'Salida de efectivo registrada.',
        isError: false,
      });
      setCashMovementModal(null);
      setTimeout(() => setNotification(null), 4000);
    } catch (error) {
      console.error('Error registering cash movement:', error);
      const apiErrorMessage = isAxiosError(error) ? error.response?.data?.error?.message : undefined;
      setNotification({ message: apiErrorMessage ?? 'No fue posible registrar el movimiento.', isError: true });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsSubmittingCashMovement(false);
    }
  };

  const handleRefundSale = async () => {
    if (!refundData?.saleId) return;

    setIsProcessingRefund(true);
    try {
      const response = await apiClient.post(`/sales/${refundData.saleId}/refund`, {
        reason: 'Devolucion'
      });

      if (response.data?.success) {
        setRefundedSales((prev) => new Set([...prev, refundData.saleId]));
        setNotification({ message: 'Devolucion procesada exitosamente.', isError: false });
        setTimeout(() => setNotification(null), 4000);
      }
    } catch (error) {
      console.error('Error processing refund:', error);
      const apiErrorMessage = isAxiosError(error) ? error.response?.data?.error?.message : undefined;
      setNotification({
        message: apiErrorMessage || 'No se pudo procesar la devolucion.',
        isError: true
      });
      setTimeout(() => setNotification(null), 4000);
    } finally {
      setIsProcessingRefund(false);
      setRefundData(null);
    }
  };

  return (
    <div className="flex h-full flex-col gap-4">
      {activeSubmodule === 'cashHistory' && (
        <div className="grid gap-6 xl:grid-cols-[1.35fr_1fr]">
          <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
            <div className="flex items-center justify-between border-b border-gray-100 p-4">
              <div>
                <h2 className="text-lg font-semibold text-gray-900">Historial de Cajas</h2>
                <p className="text-sm text-gray-500">Auditoria de turnos cerrados en la sucursal actual.</p>
              </div>
              <button
                type="button"
                onClick={refreshCashSessionsHistory}
                className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-100"
              >
                Actualizar
              </button>
            </div>

            <div className="overflow-x-auto">
              <table className="min-w-[820px] w-full text-left text-sm text-gray-600">
                <thead className="bg-gray-50 text-xs uppercase text-gray-700">
                  <tr>
                    <th className="px-4 py-3">Cajero</th>
                    <th className="px-4 py-3">Apertura</th>
                    <th className="px-4 py-3">Cierre</th>
                    <th className="px-4 py-3 text-right">Esperado</th>
                    <th className="px-4 py-3 text-right">Contado</th>
                    <th className="px-4 py-3 text-right">Diferencia</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {isLoadingCashHistory && (
                    <tr>
                      <td colSpan={6} className="px-4 py-10 text-center text-gray-500">Cargando historial...</td>
                    </tr>
                  )}

                  {!isLoadingCashHistory && cashSessionsHistory.length === 0 && (
                    <tr>
                      <td colSpan={6} className="px-4 py-10 text-center text-gray-500">No hay arqueos cerrados para esta sucursal.</td>
                    </tr>
                  )}

                  {!isLoadingCashHistory && cashSessionsHistory.map((session) => (
                    <tr key={session.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3 font-medium text-gray-900">{session.cashierName}</td>
                      <td className="px-4 py-3">{new Date(session.openedAt).toLocaleString('es-CO')}</td>
                      <td className="px-4 py-3">{new Date(session.closedAt).toLocaleString('es-CO')}</td>
                      <td className="px-4 py-3 text-right">{formatMoney(session.expectedAmount)}</td>
                      <td className="px-4 py-3 text-right">{formatMoney(session.countedAmount)}</td>
                      <td className={`px-4 py-3 text-right font-semibold ${session.difference >= 0 ? 'text-emerald-700' : 'text-red-700'}`}>
                        {formatMoney(session.difference)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
            <div className="flex items-center justify-between border-b border-gray-100 p-4">
              <div>
                <h2 className="text-lg font-semibold text-gray-900">Historial de Ventas</h2>
                <p className="text-sm text-gray-500">Ventas de la sesion activa para esta sucursal.</p>
              </div>
              <button
                type="button"
                onClick={refreshSessionHistory}
                className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-100"
              >
                Actualizar
              </button>
            </div>

            <div className="max-h-[32rem] space-y-2 overflow-y-auto p-4">
              {isLoadingHistory && (
                <div className="rounded-lg border border-dashed border-gray-300 bg-gray-50 p-4 text-sm text-gray-500">
                  Cargando ventas...
                </div>
              )}

              {!isLoadingHistory && sessionSalesHistory.length === 0 && (
                <div className="rounded-lg border border-dashed border-gray-300 bg-gray-50 p-4 text-sm text-gray-500">
                  No hay ventas registradas en la sesion activa.
                </div>
              )}

              {!isLoadingHistory && sessionSalesHistory.map((sale) => (
                <div key={sale.id} className="rounded-lg border border-gray-200 bg-white p-3">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="text-sm font-semibold text-gray-900">Ticket {sale.id.slice(0, 8)}</p>
                      <p className="text-xs text-gray-500">{new Date(sale.createdAt).toLocaleString('es-CO')}</p>
                    </div>
                    <p className="text-sm font-bold text-gray-900">
                      {(sale.isRefunded || refundedSales.has(sale.id)) ? (
                        <span className="text-red-600 line-through">{formatMoney(sale.total)}</span>
                      ) : (
                        formatMoney(sale.total)
                      )}
                    </p>
                  </div>

                  <div className="mt-2 flex items-center justify-between gap-2">
                    <span className="rounded-md bg-gray-100 px-2 py-1 text-xs font-medium text-gray-700">
                      {sale.items} item(s)
                    </span>

                    <div className="flex items-center gap-2">
                      <button
                        type="button"
                        title="Reimprimir ticket"
                        disabled={isFetchingTicket}
                        onClick={() => handleReprintFromHistory(sale.id)}
                        className="inline-flex items-center justify-center rounded-md border border-gray-200 p-1.5 text-gray-600 hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-50"
                      >
                        <Printer size={14} />
                      </button>

                      {(sale.isRefunded || refundedSales.has(sale.id)) ? (
                        <span className="rounded-md bg-red-100 px-2 py-1 text-xs font-medium text-red-700">
                          Devuelto
                        </span>
                      ) : (
                        <button
                          type="button"
                          title="Devolver venta"
                          disabled={isProcessingRefund}
                          onClick={() => setRefundData({ saleId: sale.id, showModal: true })}
                          className="inline-flex items-center justify-center rounded-md border border-red-200 bg-red-50 p-1.5 text-red-600 hover:bg-red-100 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                          <Trash2 size={14} />
                        </button>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {activeSubmodule === 'zReport' && (
        <div className="space-y-6">
          <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
            <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
              <div>
                <h2 className="text-lg font-semibold text-gray-900">Cierre Diario (Z)</h2>
                <p className="text-sm text-gray-500">Consolidado de ventas, pagos y movimientos de caja por fecha.</p>
              </div>

              <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
                <label className="flex flex-col gap-1 text-sm text-gray-700">
                  Fecha operativa
                  <input
                    type="date"
                    value={zReportDate}
                    onChange={(e) => setZReportDate(e.target.value)}
                    className="rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  />
                </label>

                <button
                  type="button"
                  onClick={loadZReport}
                  disabled={isLoadingZReport || !currentBranchId}
                  className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-60"
                >
                  {isLoadingZReport ? 'Generando...' : 'Generar Cierre Z'}
                </button>
              </div>
            </div>
          </div>

          {!isLoadingZReport && !zReportData && (
            <div className="rounded-xl border border-dashed border-gray-300 bg-gray-50 p-6 text-sm text-gray-500">
              Selecciona una fecha y genera el reporte para visualizar el cierre diario.
            </div>
          )}

          {zReportData && (
            <div className="grid gap-6 lg:grid-cols-[1.2fr_1fr]">
              <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
                <div className="flex items-start justify-between gap-3 border-b border-gray-100 pb-4">
                  <div>
                    <h3 className="text-base font-semibold text-gray-900">REPORTE DE CIERRE Z</h3>
                    <p className="text-sm text-gray-500">{zReportData.branchName} | {zReportData.date}</p>
                  </div>
                  <button
                    type="button"
                    onClick={() => printZReport(zReportData)}
                    className="inline-flex items-center gap-2 rounded-lg border border-blue-300 bg-white px-3 py-2 text-sm font-semibold text-blue-700 hover:bg-blue-50"
                  >
                    <Printer size={14} />
                    Imprimir Cierre Z
                  </button>
                </div>

                <div className="mt-4 grid gap-3 sm:grid-cols-2">
                  <div className="rounded-lg border border-gray-200 bg-gray-50 p-3">
                    <p className="text-xs uppercase tracking-wide text-gray-500">Ventas Brutas</p>
                    <p className="mt-1 text-lg font-semibold text-gray-900">{formatMoney(zReportData.grossSales)}</p>
                  </div>
                  <div className="rounded-lg border border-gray-200 bg-gray-50 p-3">
                    <p className="text-xs uppercase tracking-wide text-gray-500">Descuentos</p>
                    <p className="mt-1 text-lg font-semibold text-orange-700">-{formatMoney(zReportData.discounts)}</p>
                  </div>
                  <div className="rounded-lg border border-gray-200 bg-gray-50 p-3">
                    <p className="text-xs uppercase tracking-wide text-gray-500">Devoluciones</p>
                    <p className="mt-1 text-lg font-semibold text-red-700">-{formatMoney(zReportData.refunds)}</p>
                  </div>
                  <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-3">
                    <p className="text-xs uppercase tracking-wide text-emerald-700">Ventas Netas</p>
                    <p className="mt-1 text-lg font-semibold text-emerald-800">{formatMoney(zReportData.netSales)}</p>
                  </div>
                </div>

                <div className="mt-4 rounded-lg border border-gray-200 p-3">
                  <p className="text-sm font-semibold text-gray-800">Tickets emitidos</p>
                  <p className="mt-1 text-2xl font-bold text-gray-900">{zReportData.ticketCount}</p>
                </div>
              </div>

              <div className="space-y-4">
                <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
                  <h3 className="text-sm font-semibold text-gray-900">Desglose por forma de pago</h3>
                  <div className="mt-3 space-y-2">
                    {zReportData.paymentBreakdown.length === 0 && (
                      <p className="text-sm text-gray-500">Sin pagos registrados.</p>
                    )}
                    {zReportData.paymentBreakdown.map((payment) => (
                      <div key={payment.method} className="flex items-center justify-between rounded-lg bg-gray-50 px-3 py-2 text-sm">
                        <span className="text-gray-700">{formatPaymentMethodLabel(payment.method)}</span>
                        <span className="font-semibold text-gray-900">{formatMoney(payment.amount)}</span>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
                  <h3 className="text-sm font-semibold text-gray-900">Movimientos de Caja</h3>
                  <div className="mt-3 space-y-2 text-sm">
                    <div className="flex items-center justify-between rounded-lg bg-emerald-50 px-3 py-2">
                      <span className="text-emerald-700">Entradas</span>
                      <span className="font-semibold text-emerald-800">{formatMoney(zReportData.cashMovements.cashIn)}</span>
                    </div>
                    <div className="flex items-center justify-between rounded-lg bg-amber-50 px-3 py-2">
                      <span className="text-amber-700">Salidas</span>
                      <span className="font-semibold text-amber-800">{formatMoney(zReportData.cashMovements.cashOut)}</span>
                    </div>
                    <div className="flex items-center justify-between rounded-lg bg-blue-50 px-3 py-2">
                      <span className="text-blue-700">Neto</span>
                      <span className="font-semibold text-blue-800">{formatMoney(zReportData.cashMovements.net)}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {activeSubmodule === 'pos' && (
    <div className="flex min-h-0 flex-1 flex-col gap-4 lg:flex-row">
      {/* Catalog Section */}
      <div className="flex min-h-[24rem] flex-1 flex-col overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm lg:min-h-0 lg:basis-[68%]">
        <div className="p-4 border-b border-gray-100 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-gray-800">Catalogo</h2>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setIsPendingDrawerOpen(true)}
              disabled={!currentSessionId}
              className="relative rounded-lg border border-slate-200 bg-slate-50 px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60"
            >
              <span className="inline-flex items-center gap-1.5">
                <Clock3 size={13} />
                En Espera
              </span>
              {pendingSales.length > 0 && (
                <span className="absolute -right-2 -top-2 inline-flex min-w-5 items-center justify-center rounded-full bg-blue-600 px-1.5 py-0.5 text-[10px] font-bold text-white">
                  {pendingSales.length}
                </span>
              )}
            </button>
            <button
              type="button"
              onClick={() => setCashMovementModal({ type: 'CashIn', amount: '', reason: '' })}
              disabled={!currentSessionId || isSubmittingCashMovement}
              className="rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-1.5 text-xs font-semibold text-emerald-700 hover:bg-emerald-100 disabled:cursor-not-allowed disabled:opacity-60"
            >
              Entrada de Efectivo
            </button>
            <button
              type="button"
              onClick={() => setCashMovementModal({ type: 'CashOut', amount: '', reason: '' })}
              disabled={!currentSessionId || isSubmittingCashMovement}
              className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-1.5 text-xs font-semibold text-amber-700 hover:bg-amber-100 disabled:cursor-not-allowed disabled:opacity-60"
            >
              Salida de Efectivo
            </button>
            <button
              type="button"
              onClick={handleOpenCloseModal}
              disabled={!currentSessionId || isOpeningCloseModal}
              className="rounded-lg border border-red-200 bg-red-50 px-3 py-1.5 text-xs font-semibold text-red-700 hover:bg-red-100 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {isOpeningCloseModal ? 'Cargando...' : 'Cerrar Turno'}
            </button>
            <button
              type="button"
              onClick={handleManualCatalogSync}
              disabled={isManualCatalogSyncing || !currentBranchId}
              className="rounded-lg border border-blue-200 bg-blue-50 px-3 py-1.5 text-xs font-semibold text-blue-700 hover:bg-blue-100 disabled:cursor-not-allowed disabled:opacity-60"
            >
              <span className="inline-flex items-center gap-1.5">
                <RefreshCw size={13} className={isManualCatalogSyncing ? 'animate-spin' : ''} />
                {isManualCatalogSyncing ? 'Sincronizando...' : 'Sync Catalogo'}
              </span>
            </button>
            {isOnline ? (
              <span className="flex items-center gap-2 text-xs font-medium text-green-600 bg-green-50 px-2.5 py-1 rounded-full"><Wifi size={14} /> Online</span>
            ) : (
              <span className="flex items-center gap-2 text-xs font-medium text-amber-600 bg-amber-50 px-2.5 py-1 rounded-full"><WifiOff size={14} /> Offline Mode</span>
            )}
          </div>
        </div>
        <div className="p-4 border-b border-gray-100">
          <p className="mb-2 text-xs text-gray-500">
            Ultima sync: {lastCatalogSyncAt ? new Date(lastCatalogSyncAt).toLocaleString() : 'Sin sincronizar'}
          </p>
          <div className="relative">
            <Search size={18} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
            <input 
              className="w-full bg-gray-50 border border-gray-200 rounded-lg pl-10 pr-4 py-2 text-sm focus:ring-2 focus:ring-blue-500 focus:outline-none" 
              placeholder="Buscar producto..." 
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
            />
          </div>
        </div>
        <div className="flex-1 overflow-y-auto p-4 grid grid-cols-2 lg:grid-cols-3 gap-4">
          {filteredCatalog.map(p => (
            <button 
              key={p.id}
              onClick={() => addToCart(p)}
              className="flex flex-col text-left border border-gray-100 p-4 rounded-xl hover:border-blue-500 hover:bg-blue-50 transition-all group"
            >
              <span className="text-xs text-gray-400 mb-1">{p.sku}</span>
              <span className="font-medium text-gray-800 flex-1">{p.name}</span>
              <span className="text-blue-600 font-bold mt-2">{formatMoney(p.price)}</span>
            </button>
          ))}
          {filteredCatalog.length === 0 && (
            <div className="col-span-full rounded-lg border border-dashed border-gray-300 bg-gray-50 p-6 text-center text-sm text-gray-500">
              No hay productos en cache para esta sucursal. Sincroniza catalogo para habilitar ventas offline.
            </div>
          )}
        </div>
      </div>

      {/* Cart & Checkout Section */}
      <div className="flex min-h-[28rem] w-full flex-col overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm lg:min-h-0 lg:min-w-[30rem] lg:basis-[32%]">
        <div className="p-4 border-b border-gray-100">
          <h2 className="text-lg font-semibold text-gray-800">Ticket de Venta</h2>
          {activeHeldSaleId && (
            <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
              Editando ticket en espera <span className="font-semibold">{activeHeldSaleId.slice(0, 8)}</span>
            </div>
          )}

          <div className="mt-3 rounded-lg border border-gray-200 bg-white p-3">
            <div className="flex items-center justify-between gap-2">
              <div>
                <p className="text-xs font-medium uppercase tracking-wide text-gray-500">Cliente de la venta</p>
                <p className="text-sm font-semibold text-gray-900">
                  {selectedCustomer?.name ?? 'Consumidor Final'}
                </p>
                {selectedCustomer?.documentNumber && (
                  <p className="text-xs text-gray-600">Doc: {selectedCustomer.documentNumber}</p>
                )}
              </div>
              <button
                type="button"
                onClick={() => {
                  setIsCustomerSelectorOpen((prev) => !prev);
                  setCustomerSearchTerm('');
                }}
                className="rounded-lg border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
              >
                Buscar / Cambiar
              </button>
            </div>

            {isCustomerSelectorOpen && (
              <div className="mt-3 space-y-2 border-t border-gray-100 pt-3">
                <div className="relative">
                  <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
                  <input
                    value={customerSearchTerm}
                    onChange={(e) => setCustomerSearchTerm(e.target.value)}
                    placeholder="Buscar por nombre o documento"
                    className="w-full rounded-lg border border-gray-300 py-2 pl-9 pr-3 text-sm outline-none focus:border-blue-500"
                  />
                </div>

                <div className="max-h-40 overflow-y-auto rounded-lg border border-gray-200 bg-gray-50">
                  {isLoadingCustomers && (
                    <p className="p-3 text-sm text-gray-500">Buscando clientes...</p>
                  )}

                  {!isLoadingCustomers && customerOptions.length === 0 && (
                    <p className="p-3 text-sm text-gray-500">Sin resultados. Crea un cliente rápido.</p>
                  )}

                  {!isLoadingCustomers && customerOptions.map((customer) => (
                    <button
                      key={customer.id}
                      type="button"
                      onClick={() => {
                        setSelectedCustomer(customer);
                        setIsCustomerSelectorOpen(false);
                      }}
                      className="w-full border-b border-gray-100 px-3 py-2 text-left text-sm hover:bg-white"
                    >
                      <p className="font-medium text-gray-900">{customer.name}</p>
                      <p className="text-xs text-gray-500">{customer.documentNumber || 'Sin documento'}</p>
                    </button>
                  ))}
                </div>

                <div className="flex items-center justify-between gap-2">
                  <button
                    type="button"
                    onClick={() => {
                      setSelectedCustomer(null);
                      setIsCustomerSelectorOpen(false);
                    }}
                    className="rounded-lg border border-gray-300 px-3 py-1.5 text-xs text-gray-700 hover:bg-gray-50"
                  >
                    Consumidor Final
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setIsQuickCustomerModalOpen(true);
                      setIsCustomerSelectorOpen(false);
                    }}
                    className="inline-flex items-center gap-1 rounded-lg bg-blue-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-blue-700"
                  >
                    <UserPlus size={13} />
                    Nuevo Cliente
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
        
        <div className="flex flex-1 min-h-0 flex-col p-4">
          <div className="flex min-h-0 flex-1 flex-col rounded-lg border border-gray-200 bg-white">
            <div className="flex items-center justify-between border-b border-gray-100 px-3 py-2">
              <h3 className="text-sm font-semibold text-gray-700">Items del Ticket</h3>
              <span className="rounded-full bg-blue-50 px-2 py-0.5 text-xs font-semibold text-blue-700">
                {cart.length}
              </span>
            </div>

            <div className="flex-1 min-h-0 overflow-y-auto p-3">
              {cart.length === 0 ? (
                <div className="flex h-full items-center justify-center rounded-lg border border-dashed border-gray-300 bg-gray-50 text-sm text-gray-500">
                  El carrito esta vacio
                </div>
              ) : (
                <div className="space-y-2">
                  {cart.map(item => (
                    <div key={item.id} className="flex items-center gap-3 rounded-lg border border-gray-100 bg-gray-50 p-3">
                      <div className="flex-1">
                        <p className="line-clamp-1 text-sm font-medium text-gray-800">{item.name}</p>
                        <p className="text-xs font-semibold text-blue-600">{formatMoney(item.price)}</p>
                      </div>
                      <div className="flex items-center rounded-md border border-gray-200 bg-white shadow-sm">
                        <button onClick={() => updateQuantity(item.id, -1)} className="p-1 text-gray-500 hover:bg-gray-100"><Minus size={14}/></button>
                        <span className="w-8 text-center text-sm font-medium">{item.quantity}</span>
                        <button onClick={() => updateQuantity(item.id, 1)} className="p-1 text-gray-500 hover:bg-gray-100"><Plus size={14}/></button>
                      </div>
                      <button onClick={() => removeFromCart(item.id)} className="rounded-md p-1.5 text-red-500 hover:bg-red-50">
                        <Trash2 size={16} />
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Totals & Actions */}
        <div className="p-4 border-t border-gray-100 bg-gray-50 space-y-4">
          <div className="space-y-1 text-sm">
            <div className="flex justify-between text-gray-500"><span>Subtotal</span><span>{formatMoney(subTotal)}</span></div>
            {discountAmount > 0 && (
              <div className="flex justify-between text-orange-600 font-medium">
                <span>Descuento</span>
                <span>-{formatMoney(discountAmount)}</span>
              </div>
            )}
            <div className="flex justify-between text-gray-500"><span>IVA ({taxPercentage.toFixed(2)}%)</span><span>{formatMoney(tax)}</span></div>
            <div className="flex justify-between text-lg font-bold text-gray-900 border-t border-gray-200 pt-2 mt-2">
              <span>Total</span><span>{formatMoney(total)}</span>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <div className="relative flex-1">
              <div className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-gray-500">
                {paymentMethod === 0 ? <Banknote size={16} /> : paymentMethod === 1 ? <CreditCard size={16} /> : <Clock3 size={16} />}
              </div>
              <select
                value={paymentMethod}
                onChange={(e) => setPaymentMethod(Number(e.target.value))}
                className="w-full rounded-xl border border-gray-300 bg-white py-2.5 pl-9 pr-3 text-sm font-medium text-gray-700 outline-none transition-all focus:border-blue-500"
                aria-label="Forma de pago"
              >
                <option value={0}>Efectivo</option>
                <option value={1}>Tarjeta</option>
                {selectedCustomer && <option value={5}>A Crédito</option>}
              </select>
            </div>

            <button
              type="button"
              onClick={() => setShowDiscountModal(true)}
              className="inline-flex h-11 w-11 items-center justify-center rounded-xl border border-orange-300 bg-orange-100 text-orange-700 transition-all hover:bg-orange-200"
              title="Aplicar descuento"
              aria-label="Aplicar descuento"
            >
              <Percent size={16} />
            </button>
          </div>

          {notification && (
            <div className={`p-3 text-sm rounded-lg text-center ${notification.isError ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-700'}`}>
              {notification.message}
            </div>
          )}

          {closeSummary && (
            <div className="rounded-lg border border-blue-200 bg-blue-50 p-3 text-sm text-blue-900">
              <p className="font-semibold">Resumen de Arqueo</p>
              <p>Fondo inicial: {formatMoney(closeSummary.initialBalance)}</p>
              <p>(+) Ventas en efectivo: {formatMoney(closeSummary.cashSalesTotal)}</p>
              <p>(-) Devoluciones: {formatMoney(closeSummary.cashRefundsTotal)}</p>
              <p>(+) Entradas manuales: {formatMoney(closeSummary.manualCashInTotal)}</p>
              <p>(-) Salidas manuales: {formatMoney(closeSummary.manualCashOutTotal)}</p>
              <p className="font-semibold">= Esperado: {formatMoney(closeSummary.finalBalanceExpected)}</p>
              <p>Contado: {formatMoney(closeSummary.finalBalanceEncounted)}</p>
              <p>Diferencia: {formatMoney(closeSummary.difference)}</p>
            </div>
          )}

          <div className="grid grid-cols-[3rem_1fr] gap-2">
            <button
              type="button"
              disabled={cart.length === 0 || isProcessing}
              onClick={handlePutSaleOnHold}
              className="inline-flex h-12 items-center justify-center rounded-xl border border-slate-300 bg-white text-slate-700 transition-all hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
              title="Poner en espera"
              aria-label="Poner en espera"
            >
              <Pause size={17} />
            </button>

            <button
              disabled={cart.length === 0 || isProcessing}
              onClick={handleFinalizeSale}
              className="w-full rounded-xl bg-blue-600 py-3 font-semibold text-white shadow-md transition-all hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50 active:scale-[0.98]"
            >
              {isProcessing ? 'Procesando...' : activeHeldSaleId ? 'Cobrar Venta en Espera' : 'Finalizar Venta'}
            </button>
          </div>

          {lastTicketData && (
            <button
              type="button"
              onClick={() => printTicket(lastTicketData)}
              className="w-full rounded-xl border border-blue-300 bg-white py-2.5 font-semibold text-blue-700 transition-all hover:bg-blue-50"
            >
              Imprimir Ticket
            </button>
          )}
        </div>
      </div>

      {isCloseModalOpen && (
        <CloseCashierModal
          isLoading={isClosingSession}
          currencySymbol={currencySymbol}
          breakdown={closeSummaryPreview ?? {
            initialBalance: 0,
            cashSalesTotal: 0,
            cashRefundsTotal: 0,
            manualCashInTotal: 0,
            manualCashOutTotal: 0,
            finalBalanceExpected: 0,
          }}
          onClose={() => setIsCloseModalOpen(false)}
          onSubmit={handleCloseSession}
        />
      )}

      {isPendingDrawerOpen && (
        <div className="fixed inset-0 z-50 flex bg-black/30">
          <div className="flex-1" onClick={() => setIsPendingDrawerOpen(false)} />
          <div className="h-full w-full max-w-md overflow-y-auto bg-white shadow-2xl">
            <div className="sticky top-0 border-b border-gray-200 bg-white p-4">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-lg font-semibold text-gray-900">Tickets en Espera</h3>
                  <p className="text-sm text-gray-500">Retoma un carrito pendiente o verifica cuántos clientes están estacionados.</p>
                </div>
                <button
                  type="button"
                  onClick={() => setIsPendingDrawerOpen(false)}
                  className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cerrar
                </button>
              </div>
            </div>

            <div className="p-4 space-y-3">
              {isLoadingPendingSales && (
                <div className="rounded-lg border border-dashed border-gray-300 bg-gray-50 p-4 text-sm text-gray-500">
                  Cargando tickets en espera...
                </div>
              )}

              {!isLoadingPendingSales && pendingSales.length === 0 && (
                <div className="rounded-lg border border-dashed border-gray-300 bg-gray-50 p-4 text-sm text-gray-500">
                  No hay tickets en espera para esta sesion.
                </div>
              )}

              {!isLoadingPendingSales && pendingSales.map((sale) => (
                <div key={sale.id} className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="text-sm font-semibold text-gray-900">Ticket {sale.id.slice(0, 8)}</p>
                      <p className="text-xs text-gray-500">{new Date(sale.createdAt).toLocaleString('es-CO')}</p>
                    </div>
                    <span className={`rounded-full px-2 py-1 text-[11px] font-semibold ${sale.isSynced ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700'}`}>
                      {sale.isSynced ? 'Sincronizado' : 'Local'}
                    </span>
                  </div>

                  <div className="mt-3 space-y-1 text-sm text-gray-600">
                    <p>Productos: {sale.details.reduce((sum, detail) => sum + detail.quantity, 0)}</p>
                    <p>Subtotal: {formatMoney(sale.subTotal)}</p>
                    <p>Descuento: {formatMoney(sale.discount)}</p>
                    <p className="font-semibold text-gray-900">Total: {formatMoney(sale.total)}</p>
                  </div>

                  <button
                    type="button"
                    onClick={() => handleResumePendingSale(sale)}
                    className="mt-4 w-full rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
                  >
                    <span className="inline-flex items-center gap-2">
                      <Play size={14} />
                      Retomar
                    </span>
                  </button>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {cashMovementModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="w-full max-w-md rounded-xl bg-white p-6 shadow-lg">
            <h3 className="text-lg font-semibold text-gray-900">
              {cashMovementModal.type === 'CashIn' ? 'Entrada de Efectivo' : 'Salida de Efectivo'}
            </h3>
            <p className="mt-1 text-sm text-gray-500">Registra un movimiento manual que no corresponde a venta ni devolución.</p>

            <div className="mt-4 space-y-4">
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Monto</label>
                <input
                  type="number"
                  min="0.01"
                  step="0.01"
                  value={cashMovementModal.amount}
                  onChange={(e) => setCashMovementModal((prev) => prev ? { ...prev, amount: e.target.value } : prev)}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  placeholder="0.00"
                />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Motivo</label>
                <textarea
                  value={cashMovementModal.reason}
                  onChange={(e) => setCashMovementModal((prev) => prev ? { ...prev, reason: e.target.value } : prev)}
                  className="h-24 w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  maxLength={200}
                  placeholder={cashMovementModal.type === 'CashIn' ? 'Ej. Cambio para la caja' : 'Ej. Pago proveedor de agua'}
                />
              </div>
            </div>

            <div className="mt-5 flex gap-3">
              <button
                type="button"
                onClick={() => setCashMovementModal(null)}
                disabled={isSubmittingCashMovement}
                className="flex-1 rounded-lg border border-gray-300 px-4 py-2 text-gray-700 hover:bg-gray-50 disabled:opacity-60"
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={handleRegisterCashMovement}
                disabled={isSubmittingCashMovement}
                className={`flex-1 rounded-lg px-4 py-2 font-medium text-white disabled:opacity-60 ${cashMovementModal.type === 'CashIn' ? 'bg-emerald-600 hover:bg-emerald-700' : 'bg-amber-600 hover:bg-amber-700'}`}
              >
                {isSubmittingCashMovement ? 'Guardando...' : 'Registrar Movimiento'}
              </button>
            </div>
          </div>
        </div>
      )}

      {isQuickCustomerModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="w-full max-w-lg rounded-xl bg-white p-6 shadow-lg">
            <div className="flex items-start justify-between gap-2">
              <div>
                <h3 className="text-lg font-semibold text-gray-900">Nuevo Cliente</h3>
                <p className="mt-1 text-sm text-gray-500">Crea el cliente y asígnalo al ticket actual.</p>
              </div>
              <button
                type="button"
                onClick={() => {
                  setIsQuickCustomerModalOpen(false);
                  resetQuickCustomerForm();
                }}
                className="rounded-md p-1 text-gray-500 hover:bg-gray-100"
              >
                <X size={16} />
              </button>
            </div>

            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <div className="sm:col-span-2">
                <label className="mb-1 block text-sm font-medium text-gray-700">Nombre</label>
                <input
                  value={quickCustomerForm.name}
                  onChange={(e) => setQuickCustomerForm((prev) => ({ ...prev, name: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  placeholder="Nombre del cliente"
                />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Documento</label>
                <input
                  value={quickCustomerForm.documentNumber}
                  onChange={(e) => setQuickCustomerForm((prev) => ({ ...prev, documentNumber: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  placeholder="NIT / DNI / RUT"
                />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Teléfono</label>
                <input
                  value={quickCustomerForm.phone}
                  onChange={(e) => setQuickCustomerForm((prev) => ({ ...prev, phone: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  placeholder="+57 300 000 0000"
                />
              </div>
              <div className="sm:col-span-2">
                <label className="mb-1 block text-sm font-medium text-gray-700">Email</label>
                <input
                  value={quickCustomerForm.email}
                  onChange={(e) => setQuickCustomerForm((prev) => ({ ...prev, email: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  placeholder="correo@cliente.com"
                />
              </div>
              <div className="sm:col-span-2">
                <label className="mb-1 block text-sm font-medium text-gray-700">Dirección</label>
                <input
                  value={quickCustomerForm.address}
                  onChange={(e) => setQuickCustomerForm((prev) => ({ ...prev, address: e.target.value }))}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 outline-none focus:border-blue-500"
                  placeholder="Dirección de contacto"
                />
              </div>
            </div>

            <div className="mt-5 flex gap-3">
              <button
                type="button"
                onClick={() => {
                  setIsQuickCustomerModalOpen(false);
                  resetQuickCustomerForm();
                }}
                className="flex-1 rounded-lg border border-gray-300 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50"
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={handleCreateQuickCustomer}
                disabled={isCreatingQuickCustomer}
                className="flex-1 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
              >
                {isCreatingQuickCustomer ? 'Guardando...' : 'Crear Cliente'}
              </button>
            </div>
          </div>
        </div>
      )}

      {showDiscountModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg shadow-lg p-6 max-w-sm w-full mx-4">
            <h3 className="text-xl font-bold mb-4 text-gray-900">Aplicar Descuento</h3>

            <div className="space-y-4">
              <div className="flex gap-2">
                <button
                  onClick={() => setDiscountType('fixed')}
                  className={`flex-1 py-2 px-3 rounded-lg font-medium transition-all ${
                    discountType === 'fixed'
                      ? 'bg-blue-100 text-blue-700 border-2 border-blue-500'
                      : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                  }`}
                >
                  Monto Fijo
                </button>
                <button
                  onClick={() => setDiscountType('percentage')}
                  className={`flex-1 py-2 px-3 rounded-lg font-medium transition-all ${
                    discountType === 'percentage'
                      ? 'bg-blue-100 text-blue-700 border-2 border-blue-500'
                      : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                  }`}
                >
                  Porcentaje
                </button>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  {discountType === 'percentage' ? 'Porcentaje (%)' : `Monto (${currencySymbol})`}
                </label>
                <input
                  type="number"
                  min="0"
                  max={discountType === 'percentage' ? 100 : subTotal}
                  value={discount}
                  onChange={(e) => setDiscount(parseFloat(e.target.value) || 0)}
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
                  placeholder={discountType === 'percentage' ? '10' : '5000'}
                />
              </div>

              {discountAmount > 0 && (
                <div className="bg-blue-50 border border-blue-200 rounded-lg p-3 text-sm">
                  <p className="text-gray-600">Descuento a aplicar: <strong>{formatMoney(discountAmount)}</strong></p>
                  <p className="text-gray-600">Total despues de descuento: <strong>{formatMoney(subTotalAfterDiscount)}</strong></p>
                </div>
              )}
            </div>

            <div className="flex gap-2 mt-6">
              <button
                onClick={() => {
                  setShowDiscountModal(false);
                  setDiscount(0);
                  setDiscountType('fixed');
                }}
                className="flex-1 py-2 px-3 rounded-lg bg-gray-100 text-gray-700 font-medium hover:bg-gray-200 transition-all"
              >
                Cancelar
              </button>
              <button
                onClick={() => setShowDiscountModal(false)}
                className="flex-1 py-2 px-3 rounded-lg bg-blue-600 text-white font-medium hover:bg-blue-700 transition-all"
              >
                Aplicar
              </button>
            </div>
          </div>
        </div>
      )}

      {refundData && refundData.showModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
          <div className="bg-white rounded-lg shadow-lg p-6 max-w-sm w-full mx-4">
            <h3 className="text-xl font-bold mb-4 text-gray-900">Confirmar Devolucion</h3>
            <p className="text-gray-600 mb-6">Deseas procesar la devolucion del ticket <strong>{refundData.saleId.slice(0, 8)}</strong>?</p>
            <p className="text-sm text-gray-500 mb-6">Se restaurara el inventario y se registrara la salida de dinero en la caja actual.</p>

            <div className="flex gap-2">
              <button
                onClick={() => setRefundData(null)}
                disabled={isProcessingRefund}
                className="flex-1 py-2 px-3 rounded-lg bg-gray-100 text-gray-700 font-medium hover:bg-gray-200 transition-all disabled:opacity-50"
              >
                Cancelar
              </button>
              <button
                onClick={handleRefundSale}
                disabled={isProcessingRefund}
                className="flex-1 py-2 px-3 rounded-lg bg-red-600 text-white font-medium hover:bg-red-700 transition-all disabled:opacity-50"
              >
                {isProcessingRefund ? 'Procesando...' : 'Devolver'}
              </button>
            </div>
          </div>
        </div>
      )}

    </div>
      )}
    </div>
  );
};
