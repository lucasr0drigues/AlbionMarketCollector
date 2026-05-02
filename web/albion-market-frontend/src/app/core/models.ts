export interface FlipOpportunity {
  itemUniqueName: string;
  itemLocalizedName: string;
  qualityLevel: number;
  enchantmentLevel: number;
  sourceLocationId: string;
  sourceLocationName: string;
  sellingLocationId: string;
  sellingLocationName: string;
  buyOrderId: number;
  buyPriceSilver: number;
  buyAmount: number;
  buyLastSeenAtUtc: string;
  buyAgeMinutes: number;
  sellOrderId: number;
  sellPriceSilver: number;
  sellAmount: number;
  sellLastSeenAtUtc: string;
  sellAgeMinutes: number;
  maxTradableAmount: number;
  profitPerItemSilver: number;
  profitPercent: number;
  estimatedTotalProfitSilver: number;
}

export interface FlipOpportunityPage {
  items: FlipOpportunity[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasMore: boolean;
}

export interface FlipFilters {
  sourceLocationIds: string[];
  excludedSourceLocationIds?: string[];
  sellingLocationIds: string[];
  maxAgeMinutes?: number | null;
  minProfitSilver?: number | null;
  minTotalProfitSilver?: number | null;
  minProfitPercent?: number | null;
  marketTaxRate?: number;
  itemUniqueNames?: string[];
  qualityLevel?: number | null;
  enchantmentLevel?: number | null;
  sortBy?: string | null;
  sortDirection?: 'asc' | 'desc';
  page: number;
  pageSize: number;
}

export interface ItemSearchResult {
  id: number;
  uniqueName: string;
  localizedName: string;
}

export interface ClearMarketOrdersResult {
  deletedCount: number;
}

export interface AppSettings {
  premium: boolean;
  defaultMinTotalProfitSilver: number | null;
  defaultMinProfitPercent: number | null;
}

export interface UpdateAppSettingsRequest {
  premium: boolean;
  defaultMinTotalProfitSilver: number | null;
  defaultMinProfitPercent: number | null;
}
