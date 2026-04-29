export interface FlipOpportunity {
  itemUniqueName: string;
  itemLocalizedName: string;
  qualityLevel: number;
  enchantmentLevel: number;
  sourceLocationId: string;
  sourceLocationName: string;
  blackMarketLocationId: string;
  blackMarketLocationName: string;
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

export interface FlipFilters {
  sourceLocationIds: string[];
  excludedSourceLocationIds?: string[];
  sellingLocationIds: string[];
  maxAgeMinutes?: number | null;
  minProfitSilver?: number | null;
  minProfitPercent?: number | null;
  itemUniqueNames?: string[];
  qualityLevel?: number | null;
  enchantmentLevel?: number | null;
  limit: number;
}

export interface ItemSearchResult {
  id: number;
  uniqueName: string;
  localizedName: string;
}

export interface ClearMarketOrdersResult {
  deletedCount: number;
}
