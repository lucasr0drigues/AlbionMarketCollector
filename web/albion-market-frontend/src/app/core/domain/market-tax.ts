export const PREMIUM_MARKET_TAX_RATE = 0.04;
export const STANDARD_MARKET_TAX_RATE = 0.08;

export function marketTaxRate(premium: boolean): number {
  return premium ? PREMIUM_MARKET_TAX_RATE : STANDARD_MARKET_TAX_RATE;
}

export function profitAfterMarketTax(
  sellAtDestination: number,
  buyAtSource: number,
  taxRate: number,
): number {
  if (!Number.isFinite(sellAtDestination) || !Number.isFinite(buyAtSource)) {
    return 0;
  }

  return Math.round(sellAtDestination - buyAtSource - sellAtDestination * taxRate);
}

export function totalProfitAfterMarketTax(
  sellAtDestination: number,
  buyAtSource: number,
  quantity: number,
  taxRate: number,
): number {
  if (!Number.isFinite(quantity) || quantity <= 0) {
    return 0;
  }

  return profitAfterMarketTax(sellAtDestination, buyAtSource, taxRate) * quantity;
}

export function marketTaxLabel(taxRate: number): string {
  return `${Math.round(taxRate * 100)}%`;
}
