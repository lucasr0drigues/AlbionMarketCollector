import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ClearMarketOrdersResult, FlipFilters, FlipOpportunity, ItemSearchResult } from './models';

@Injectable({ providedIn: 'root' })
export class MarketApiService {
  private readonly apiBaseUrl = '/api';

  constructor(private readonly http: HttpClient) {}

  findBlackMarketFlips(filters: FlipFilters): Observable<FlipOpportunity[]> {
    let params = new HttpParams().set('limit', filters.limit);

    for (const locationId of filters.sourceLocationIds ?? []) {
      params = params.append('sourceLocationIds', locationId);
    }
    for (const locationId of filters.excludedSourceLocationIds ?? []) {
      params = params.append('excludedSourceLocationIds', locationId);
    }
    for (const locationId of filters.sellingLocationIds ?? []) {
      params = params.append('sellingLocationIds', locationId);
    }

    if (filters.maxAgeMinutes != null) {
      params = params.set('maxAgeMinutes', filters.maxAgeMinutes);
    }
    if (filters.minProfitSilver != null) {
      params = params.set('minProfitSilver', filters.minProfitSilver);
    }
    if (filters.minProfitPercent != null) {
      params = params.set('minProfitPercent', filters.minProfitPercent);
    }

    for (const uniqueName of filters.itemUniqueNames ?? []) {
      params = params.append('itemUniqueNames', uniqueName);
    }

    if (filters.qualityLevel != null) {
      params = params.set('qualityLevel', filters.qualityLevel);
    }
    if (filters.enchantmentLevel != null) {
      params = params.set('enchantmentLevel', filters.enchantmentLevel);
    }

    return this.http.get<FlipOpportunity[]>(`${this.apiBaseUrl}/flips/black-market`, { params });
  }

  searchItems(search: string, limit = 20): Observable<ItemSearchResult[]> {
    const params = new HttpParams().set('search', search).set('limit', limit);
    return this.http.get<ItemSearchResult[]>(`${this.apiBaseUrl}/items`, { params });
  }

  clearMarketOrders(locationIds: string[]): Observable<ClearMarketOrdersResult> {
    let params = new HttpParams();
    for (const locationId of locationIds) {
      params = params.append('locationIds', locationId);
    }

    return this.http.delete<ClearMarketOrdersResult>(`${this.apiBaseUrl}/market-orders`, { params });
  }
}

export function itemImageUrl(uniqueName: string, enchantmentLevel = 0, quality = 0): string {
  const base = enchantmentLevel > 0 && !uniqueName.includes('@')
    ? `${uniqueName}@${enchantmentLevel}`
    : uniqueName;
  const params: string[] = [];
  if (quality > 0) {
    params.push(`quality=${quality}`);
  }
  const query = params.length > 0 ? `?${params.join('&')}` : '';
  return `https://render.albiononline.com/v1/item/${base}${query}`;
}
