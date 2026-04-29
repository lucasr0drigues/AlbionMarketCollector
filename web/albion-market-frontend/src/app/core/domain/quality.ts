export interface QualityMeta {
  value: number;
  label: string;
  color: string;
  border: string;
  glow: boolean;
}

export const QUALITY_META: readonly QualityMeta[] = [
  { value: 1, label: 'Normal',      color: '#52525B', border: '#3F3F46', glow: false },
  { value: 2, label: 'Good',        color: '#8A9BA8', border: '#8A9BA8', glow: false },
  { value: 3, label: 'Outstanding', color: '#CD7F32', border: '#CD7F32', glow: false },
  { value: 4, label: 'Excellent',   color: '#C0C0C0', border: '#C0C0C0', glow: true  },
  { value: 5, label: 'Masterpiece', color: '#D6A84F', border: '#D6A84F', glow: true  },
];

export function qualityMeta(level: number): QualityMeta {
  return QUALITY_META.find((q) => q.value === level) ?? QUALITY_META[0];
}

export const TIER_COLORS: Record<string, string> = {
  T4: '#60A5FA',
  T5: '#4ADE80',
  T6: '#FACC15',
  T7: '#F97316',
  T8: '#EF4444',
};

export function extractTier(uniqueName: string): string | null {
  const match = uniqueName.match(/^(T\d)/i);
  return match ? match[1].toUpperCase() : null;
}

export function extractEnchant(uniqueName: string): string {
  const match = uniqueName.match(/@(\d+)$/);
  return match ? `@${match[1]}` : '';
}
