import Dexie, { type EntityTable } from 'dexie';

export interface OfflineSaleDetail {
  id: string; // GUID
  productId: string;
  quantity: number;
  unitPrice: number;
}

export interface OfflinePayment {
  id: string; // GUID
  amount: number;
  method: number; // enum (0: Cash, etc)
}

export interface OfflineSale {
  id: string; // Pre-generated GUID
  tenantId: string;
  sessionId: string;
  branchId: string;
  subTotal: number;
  tax: number;
  total: number;
  createdAt: string; // ISO String
  details: OfflineSaleDetail[];
  payments: OfflinePayment[];
  isSynced: boolean; // Flag to trace if synced
  syncError?: string; // If syncing failed
}

const db = new Dexie('GibagOfflineSalesDB') as Dexie & {
  sales: EntityTable<
    OfflineSale,
    'id' // primary key "id"
  >;
};

// Schema declaration
db.version(1).stores({
  sales: 'id, tenantId, sessionId, branchId, isSynced, createdAt' // Primary key and indexes
});

export { db };
