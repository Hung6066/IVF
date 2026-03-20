export interface InventoryItemDto {
  id: string;
  code: string;
  name: string;
  genericName?: string;
  category: string;
  unit: string;
  manufacturer?: string;
  supplier?: string;
  currentStock: number;
  minStock: number;
  maxStock: number;
  unitPrice: number;
  expiryDate?: string;
  batchNumber?: string;
  storageLocation?: string;
  isActive: boolean;
  isLowStock: boolean;
  isExpired: boolean;
  isNearExpiry: boolean;
  notes?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface StockTransactionDto {
  id: string;
  itemId: string;
  itemName?: string;
  transactionType: string;
  quantity: number;
  stockAfter: number;
  reference?: string;
  reason?: string;
  performedByName?: string;
  supplierName?: string;
  unitCost?: number;
  batchNumber?: string;
  createdAt: string;
}

export interface CreateInventoryItemRequest {
  code: string;
  name: string;
  category: string;
  unit: string;
  minStock: number;
  maxStock: number;
  unitPrice: number;
  genericName?: string;
  manufacturer?: string;
  supplier?: string;
  expiryDate?: string;
  batchNumber?: string;
  storageLocation?: string;
}

export interface UpdateInventoryItemRequest {
  name: string;
  category: string;
  unit: string;
  minStock: number;
  maxStock: number;
  unitPrice: number;
  genericName?: string;
  manufacturer?: string;
  supplier?: string;
  expiryDate?: string;
  batchNumber?: string;
  storageLocation?: string;
  notes?: string;
}

export interface ImportStockRequest {
  itemId: string;
  quantity: number;
  supplierName?: string;
  unitCost?: number;
  batchNumber?: string;
  reference?: string;
  performedByName?: string;
}

export interface RecordUsageRequest {
  itemId: string;
  quantity: number;
  reference?: string;
  reason?: string;
  performedByName?: string;
}

export interface AdjustStockRequest {
  itemId: string;
  newQuantity: number;
  reason: string;
  performedByName?: string;
}

export interface InventorySearchResult {
  items: InventoryItemDto[];
  total: number;
  page: number;
  pageSize: number;
}
