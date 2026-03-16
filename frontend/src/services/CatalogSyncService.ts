import apiClient from '../api/apiClient';
import { db, type CatalogCategory, type CatalogProduct } from '../db/db';

interface CatalogSyncCategoryDto {
  id: string;
  name: string;
}

interface CatalogSyncProductDto {
  id: string;
  name: string;
  sku: string;
  price: number;
  categoryId: string;
  categoryName: string;
}

interface CatalogSyncPayload {
  branchId: string;
  syncedAtUtc: string;
  categories: CatalogSyncCategoryDto[];
  products: CatalogSyncProductDto[];
}

interface CatalogSyncResponse {
  success: boolean;
  data: CatalogSyncPayload;
}

export interface CatalogSyncResult {
  branchId: string;
  syncedAtUtc: string;
  productsCount: number;
  categoriesCount: number;
}

const getCatalogLastSyncKey = (branchId: string) => `catalog:lastSync:${branchId}`;

export async function syncCatalog(branchId: string, lastSyncDate?: string): Promise<CatalogSyncResult> {
  const response = await apiClient.get<CatalogSyncResponse>('/inventory/catalog/sync', {
    params: lastSyncDate ? { lastSyncDate } : undefined,
    headers: {
      'X-Branch-Id': branchId,
    },
  });

  if (!response.data.success || !response.data.data) {
    throw new Error('No se pudo sincronizar el catalogo.');
  }

  const payload = response.data.data;

  const categories: CatalogCategory[] = payload.categories.map(category => ({
    cacheKey: `${payload.branchId}:${category.id}`,
    id: category.id,
    branchId: payload.branchId,
    name: category.name,
  }));

  const products: CatalogProduct[] = payload.products.map(product => ({
    cacheKey: `${payload.branchId}:${product.id}`,
    id: product.id,
    branchId: payload.branchId,
    categoryId: product.categoryId,
    categoryName: product.categoryName,
    name: product.name,
    sku: product.sku,
    price: product.price,
  }));

  await db.transaction('rw', db.categories, db.products, db.syncMeta, async () => {
    await db.categories.where('branchId').equals(payload.branchId).delete();
    await db.products.where('branchId').equals(payload.branchId).delete();

    if (categories.length > 0) {
      await db.categories.bulkPut(categories);
    }

    if (products.length > 0) {
      await db.products.bulkPut(products);
    }

    await db.syncMeta.put({
      key: getCatalogLastSyncKey(payload.branchId),
      value: payload.syncedAtUtc,
      updatedAt: new Date().toISOString(),
    });
  });

  return {
    branchId: payload.branchId,
    syncedAtUtc: payload.syncedAtUtc,
    productsCount: products.length,
    categoriesCount: categories.length,
  };
}

export async function getCatalogLastSyncAt(branchId: string): Promise<string | null> {
  const syncMeta = await db.syncMeta.get(getCatalogLastSyncKey(branchId));
  return syncMeta?.value ?? null;
}

export async function getCatalogProducts(branchId: string): Promise<CatalogProduct[]> {
  return db.products.where('branchId').equals(branchId).toArray();
}
