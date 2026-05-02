export type FreshnessTier = 'fresh' | 'aging' | 'stale';

export function rowAgeMinutes(buyAgeMinutes: number, sellAgeMinutes: number): number {
  return Math.max(buyAgeMinutes, sellAgeMinutes);
}

export function freshnessTier(ageMinutes: number, maxAgeMinutes: number | null | undefined): FreshnessTier {
  const reference = maxAgeMinutes && maxAgeMinutes > 0 ? maxAgeMinutes : 60;
  if (ageMinutes <= reference * 0.5) {
    return 'fresh';
  }
  if (ageMinutes <= reference * 0.85) {
    return 'aging';
  }
  return 'stale';
}

export function formatAgeMinutes(minutes: number): string {
  if (!Number.isFinite(minutes) || minutes < 0) {
    return '—';
  }
  if (minutes < 1) {
    return '<1m';
  }
  if (minutes < 60) {
    return `${Math.round(minutes)}m`;
  }
  if (minutes < 24 * 60) {
    const totalMinutes = Math.round(minutes);
    const hours = Math.floor(totalMinutes / 60);
    const remainingMinutes = totalMinutes % 60;
    return remainingMinutes === 0 ? `${hours}h` : `${hours}h ${remainingMinutes}m`;
  }
  const totalHours = Math.round(minutes / 60);
  const days = Math.floor(totalHours / 24);
  const remainingHours = totalHours % 24;
  return remainingHours === 0 ? `${days}d` : `${days}d ${remainingHours}h`;
}
