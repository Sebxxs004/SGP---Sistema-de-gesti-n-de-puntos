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
  discount: number; // Total discount amount (fixed or percentage-calculated)
  createdAt: string; // ISO String
  details: OfflineSaleDetail[];
  payments: OfflinePayment[];
  isSynced: boolean; // Flag to trace if synced
  isSyncBlocked?: boolean; // Prevent endless retries for permanent 4xx errors
  syncError?: string; // If syncing failed
}

export interface CatalogCategory {
  cacheKey: string; // branchId:categoryId
  id: string;
  branchId: string;
  name: string;
}

export interface CatalogProduct {
  cacheKey: string; // branchId:productId
  id: string;
  branchId: string;
  categoryId: string;
  categoryName: string;
  name: string;
  sku: string;
  price: number;
}

export interface SyncMetadata {
  key: string;
  value: string;
  updatedAt: string;
}

const db = new Dexie('GibagOfflineSalesDB') as Dexie & {
  sales: EntityTable<
    OfflineSale,
    'id' // primary key "id"
  >;
  categories: EntityTable<
    CatalogCategory,
    'cacheKey'
  >;
  products: EntityTable<
    CatalogProduct,
    'cacheKey'
  >;
  syncMeta: EntityTable<
    SyncMetadata,
    'key'
  >;
};

// Schema declaration
db.version(1).stores({
  sales: 'id, tenantId, sessionId, branchId, isSynced, createdAt' // Primary key and indexes
});

// Increment schema version so existing browsers upgrade indexes reliably.
db.version(2)
  .stores({
    sales: 'id, tenantId, sessionId, branchId, isSynced, createdAt'
  })
  .upgrade(async tx => {
    await tx.table('sales').toCollection().modify((sale: Partial<OfflineSale>) => {
      if (typeof sale.isSynced !== 'boolean') {
        sale.isSynced = false;
      }

      if (typeof sale.isSyncBlocked !== 'boolean') {
        sale.isSyncBlocked = false;
      }
    });
  });

db.version(3)
  .stores({
    sales: 'id, tenantId, sessionId, branchId, isSynced, isSyncBlocked, createdAt'
  })
  .upgrade(async tx => {
    await tx.table('sales').toCollection().modify((sale: Partial<OfflineSale>) => {
      if (typeof sale.isSyncBlocked !== 'boolean') {
        sale.isSyncBlocked = false;
      }
    });
  });

db.version(4)
  .stores({
    sales: 'id, tenantId, sessionId, branchId, isSynced, isSyncBlocked, createdAt',
    categories: 'cacheKey, id, branchId, name',
    products: 'cacheKey, id, branchId, categoryId, sku, name, price',
    syncMeta: 'key, updatedAt'
  });

export { db };
