export type ProfitTier = 'top' | 'high' | 'mid' | 'low';

export function buildProfitTierMap<T>(
  rows: readonly T[],
  selector: (row: T) => number,
): Map<T, ProfitTier> {
  const map = new Map<T, ProfitTier>();
  if (rows.length === 0) {
    return map;
  }

  const sorted = [...rows].sort((a, b) => selector(b) - selector(a));
  const total = sorted.length;
  const topCutoff = Math.max(1, Math.ceil(total * 0.10));
  const highCutoff = Math.max(topCutoff + 1, Math.ceil(total * 0.35));
  const midCutoff = Math.max(highCutoff + 1, Math.ceil(total * 0.70));

  sorted.forEach((row, index) => {
    if (index < topCutoff) {
      map.set(row, 'top');
    } else if (index < highCutoff) {
      map.set(row, 'high');
    } else if (index < midCutoff) {
      map.set(row, 'mid');
    } else {
      map.set(row, 'low');
    }
  });

  return map;
}
