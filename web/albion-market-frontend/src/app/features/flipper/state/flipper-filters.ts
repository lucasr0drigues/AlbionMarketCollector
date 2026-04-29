import { ItemSearchResult } from '../../../core/models';

export interface FlipperFilters {
  sourceKeys: string[];
  sellingKeys: string[];
  maxAgeMinutes: number | null;
  minProfitSilver: number | null;
  minProfitPercent: number | null;
  minTotalProfitSilver: number | null;
  qualityLevel: number | null;
  enchantmentLevel: number | null;
  limit: number;
  items: ItemSearchResult[];
}

export const DEFAULT_FILTERS: FlipperFilters = {
  sourceKeys: ['fort-sterling'],
  sellingKeys: ['black-market'],
  maxAgeMinutes: null,
  minProfitSilver: null,
  minProfitPercent: null,
  minTotalProfitSilver: null,
  qualityLevel: null,
  enchantmentLevel: null,
  limit: 100,
  items: [],
};

export const QUICK_AGE_PRESETS: Array<{ label: string; value: number | null }> = [
  { label: '15m', value: 15 },
  { label: '1h', value: 60 },
  { label: '6h', value: 360 },
  { label: '24h', value: 1440 },
  { label: 'Any', value: null },
];

export const QUICK_PROFIT_PERCENT_PRESETS: Array<{ label: string; value: number | null }> = [
  { label: '10%', value: 10 },
  { label: '25%', value: 25 },
  { label: '50%', value: 50 },
  { label: '100%', value: 100 },
  { label: 'Any', value: null },
];

export const QUALITY_OPTIONS = [
  { value: null, label: 'Any quality' },
  { value: 1, label: 'Normal' },
  { value: 2, label: 'Good' },
  { value: 3, label: 'Outstanding' },
  { value: 4, label: 'Excellent' },
  { value: 5, label: 'Masterpiece' },
];

export const ENCHANTMENT_OPTIONS = [
  { value: null, label: 'Any enchant' },
  { value: 0, label: '+0' },
  { value: 1, label: '+1' },
  { value: 2, label: '+2' },
  { value: 3, label: '+3' },
  { value: 4, label: '+4' },
];
