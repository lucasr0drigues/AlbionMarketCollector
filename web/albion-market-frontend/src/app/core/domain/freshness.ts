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
  const hours = minutes / 60;
  if (hours < 24) {
    return `${hours.toFixed(hours < 10 ? 1 : 0)}h`;
  }
  const days = hours / 24;
  return `${days.toFixed(days < 10 ? 1 : 0)}d`;
}
