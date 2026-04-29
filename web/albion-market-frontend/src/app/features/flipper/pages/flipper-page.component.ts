import { ChangeDetectionStrategy, Component, OnDestroy, computed, effect, inject, signal } from '@angular/core';
import { Subject, debounceTime, of, switchMap, takeUntil } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import { AppShellComponent } from '../../../layout/app-shell.component';
import { TopbarComponent } from '../../../layout/topbar.component';
import { FlipperFilterBarComponent } from '../components/flipper-filter-bar.component';
import { OpportunityTableComponent, SortDirection, SortKey } from '../components/opportunity-table.component';
import { OpportunityDetailsComponent } from '../components/opportunity-details.component';
import { DrawerComponent } from '../../../shared/ui/drawer.component';
import { EmptyStateComponent } from '../../../shared/ui/empty-state.component';
import { ErrorStateComponent } from '../../../shared/ui/error-state.component';
import { LoadingStateComponent } from '../../../shared/ui/loading-state.component';
import { StatCardComponent } from '../../../shared/ui/stat-card.component';
import { SilverPipe } from '../../../core/formatting/silver.pipe';
import { MarketApiService } from '../../../core/market-api.service';
import { FlipFilters, FlipOpportunity } from '../../../core/models';
import { FlipperFilters, DEFAULT_FILTERS } from '../state/flipper-filters';
import { rowAgeMinutes } from '../../../core/domain/freshness';
import { flattenLocationIds, SOURCE_LOCATIONS, SELLING_LOCATIONS } from '../../../core/domain/locations';

@Component({
  selector: 'app-flipper-page',
  standalone: true,
  host: { style: 'display:contents' },
  imports: [
    AppShellComponent,
    TopbarComponent,
    FlipperFilterBarComponent,
    OpportunityTableComponent,
    OpportunityDetailsComponent,
    DrawerComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    LoadingStateComponent,
    StatCardComponent,
    SilverPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-shell #shell>
      <app-topbar
        topbar
        [loading]="loading()"
        [clearing]="clearing()"
        [freshnessAge]="freshestAge()"
        (refresh)="reload()"
        (clearCities)="clearCities()"
        (clearBlackMarket)="clearBlackMarket()"
        (sidebarToggle)="shell.toggleSidebar()"
      />

      <div style="flex:1;overflow:hidden;display:flex;flex-direction:column;">
        <app-flipper-filter-bar
          [filters]="filters()"
          (filtersChange)="updateFilters($event)"
        />

        @if (filteredOpportunities().length > 0 && !loading() && !error()) {
          <div style="display:flex;gap:10px;padding:12px 20px;flex-shrink:0;">
            <ui-stat-card label="Opportunities" accent="gold" sub="showing now" style="flex:1;min-width:0;">{{ visibleCount() }}</ui-stat-card>
            <ui-stat-card label="Top Profit / Unit" accent="green" sub="best single item" style="flex:1;min-width:0;">{{ topUnitProfit() | silver }}</ui-stat-card>
            <ui-stat-card label="Top Total Profit" accent="green" sub="if all qty bought" style="flex:1;min-width:0;">{{ topTotalProfit() | silver }}</ui-stat-card>
            <ui-stat-card label="Freshest Data" accent="neutral" sub="most recent update" style="flex:1;min-width:0;">
              @if (freshestAge() !== null) { {{ freshestAge() }}m ago } @else { — }
            </ui-stat-card>
          </div>
        }

        <div style="flex:1;overflow:hidden;display:flex;flex-direction:column;padding:0 20px 12px;">
          @if (loading() && opportunities().length === 0) {
            <ui-skeleton-rows [count]="10" />
          } @else if (error()) {
            <ui-error-state
              title="Could not load opportunities"
              [description]="error()"
              (retry)="reload()"
            />
          } @else if (filteredOpportunities().length === 0) {
            <ui-empty-state
              title="No flips match these filters"
              description="Try widening Max age, lowering Min profit %, or removing item and profit filters."
            />
          } @else {
            <app-opportunity-table
              [opportunities]="filteredOpportunities()"
              [selected]="selected()"
              [sortKey]="sortKey()"
              [sortDirection]="sortDirection()"
              (rowClick)="select($event)"
              (sortChange)="onSortChange($event)"
              (visibleCountChange)="visibleCount.set($event)"
            />
          }
        </div>
      </div>
    </app-shell>

    <ui-drawer [open]="!!selected()" (close)="closeDetails()">
      @if (selected(); as opportunity) {
        <app-opportunity-details
          [opp]="opportunity"
          [onCloseCallback]="closeDetailsFn"
        />
      }
    </ui-drawer>
  `,
})
export class FlipperPageComponent implements OnDestroy {
  private readonly api = inject(MarketApiService);
  private readonly fetchTrigger$ = new Subject<FlipFilters>();
  private readonly destroy$ = new Subject<void>();

  readonly filters = signal<FlipperFilters>(loadFiltersFromUrl() ?? DEFAULT_FILTERS);
  readonly opportunities = signal<FlipOpportunity[]>([]);
  readonly selected = signal<FlipOpportunity | null>(null);
  readonly loading = signal(false);
  readonly clearing = signal(false);
  readonly error = signal<string | null>(null);

  readonly sortKey = signal<SortKey>('total');
  readonly sortDirection = signal<SortDirection>('desc');
  readonly visibleCount = signal(0);

  readonly closeDetailsFn = () => this.closeDetails();

  readonly filteredOpportunities = computed(() => {
    const all = this.opportunities();
    const minTotal = this.filters().minTotalProfitSilver;
    if (minTotal == null || !Number.isFinite(minTotal)) return all;
    const threshold = minTotal * 10_000;
    return all.filter((o) => o.estimatedTotalProfitSilver >= threshold);
  });

  readonly freshestAge = computed(() => {
    const items = this.filteredOpportunities();
    if (items.length === 0) return null;
    return Math.round(
      items.reduce((best, item) => Math.min(best, rowAgeMinutes(item.buyAgeMinutes, item.sellAgeMinutes)), Infinity),
    );
  });

  readonly topUnitProfit = computed(() =>
    this.filteredOpportunities().reduce((max, item) => Math.max(max, item.profitPerItemSilver), 0),
  );

  readonly topTotalProfit = computed(() =>
    this.filteredOpportunities().reduce((max, item) => Math.max(max, item.estimatedTotalProfitSilver), 0),
  );

  constructor() {
    this.fetchTrigger$
      .pipe(
        debounceTime(280),
        switchMap((apiFilters) => {
          this.loading.set(true);
          this.error.set(null);
          return this.api.findBlackMarketFlips(apiFilters).pipe(
            catchError((err) => {
              this.error.set(this.toMessage(err));
              return of([] as FlipOpportunity[]);
            }),
            finalize(() => this.loading.set(false)),
          );
        }),
        takeUntil(this.destroy$),
      )
      .subscribe((data) => {
        const selected = this.selected();
        this.opportunities.set(data);
        if (selected) {
          this.selected.set(data.find((item) => sameOpportunity(item, selected)) ?? null);
        }
      });

    effect(() => {
      const filters = this.filters();
      saveFiltersToUrl(filters);
      this.fetchTrigger$.next(toApiFilters(filters));
    });
  }

  updateFilters(next: FlipperFilters): void { this.filters.set(next); }
  reload(): void { this.fetchTrigger$.next(toApiFilters(this.filters())); }
  select(opportunity: FlipOpportunity): void { this.selected.set(opportunity); }
  closeDetails(): void { this.selected.set(null); }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  clearCities(): void {
    const sourceKeys = this.filters().sourceKeys;
    const selectedLocations = sourceKeys.length > 0
      ? SOURCE_LOCATIONS.filter((location) => sourceKeys.includes(location.key))
      : SOURCE_LOCATIONS;
    const selectedIds = selectedLocations.flatMap((location) => location.ids);
    const label = sourceKeys.length > 0
      ? selectedLocations.map((location) => location.label).join(', ')
      : 'all source cities';
    this.clearMarketOrders(
      selectedIds.filter((locationId) => locationId !== '3003'),
      `Delete stored market orders for ${label}? This cannot be undone.`,
    );
  }

  clearBlackMarket(): void {
    this.clearMarketOrders(
      ['3003'],
      'Delete stored market orders for the Black Market? This cannot be undone.',
    );
  }

  onSortChange(event: { key: SortKey; direction: SortDirection }): void {
    this.sortKey.set(event.key);
    this.sortDirection.set(event.direction);
  }

  private toMessage(err: unknown): string {
    if (err && typeof err === 'object' && 'message' in err && typeof (err as { message: unknown }).message === 'string') {
      return (err as { message: string }).message;
    }
    return 'API returned an error. Make sure the API is running on http://localhost:5000.';
  }

  private clearMarketOrders(locationIds: string[], confirmationMessage: string): void {
    const uniqueLocationIds = [...new Set(locationIds.filter(Boolean))];
    if (uniqueLocationIds.length === 0) {
      return;
    }

    if (typeof window !== 'undefined' && !window.confirm(confirmationMessage)) {
      return;
    }

    this.clearing.set(true);
    this.error.set(null);
    this.api.clearMarketOrders(uniqueLocationIds)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.clearing.set(false)),
      )
      .subscribe({
        next: () => this.reload(),
        error: (err) => this.error.set(this.toMessage(err)),
      });
  }
}

function sameOpportunity(left: FlipOpportunity, right: FlipOpportunity): boolean {
  return left.buyOrderId === right.buyOrderId &&
    left.sellOrderId === right.sellOrderId &&
    left.sourceLocationId === right.sourceLocationId &&
    left.sellingLocationId === right.sellingLocationId &&
    left.itemUniqueName === right.itemUniqueName;
}

function toApiFilters(filters: FlipperFilters): FlipFilters {
  return {
    sourceLocationIds: flattenLocationIds(SOURCE_LOCATIONS, filters.sourceKeys),
    excludedSourceLocationIds: filters.sourceKeys.length === 0 ? ['3003'] : [],
    sellingLocationIds: flattenLocationIds(SELLING_LOCATIONS, filters.sellingKeys),
    maxAgeMinutes: filters.maxAgeMinutes,
    minProfitSilver: filters.minProfitSilver,
    minProfitPercent: filters.minProfitPercent,
    itemUniqueNames: filters.items.map((i) => i.uniqueName),
    qualityLevel: filters.qualityLevel,
    enchantmentLevel: filters.enchantmentLevel,
    limit: filters.limit,
  };
}

function saveFiltersToUrl(filters: FlipperFilters): void {
  if (typeof window === 'undefined') return;
  const params = new URLSearchParams();
  if (filters.sourceKeys.length > 0) params.set('source', filters.sourceKeys.join(','));
  if (filters.sellingKeys.length > 0) params.set('selling', filters.sellingKeys.join(','));
  if (filters.maxAgeMinutes != null) params.set('age', String(filters.maxAgeMinutes));
  if (filters.minProfitPercent != null) params.set('pct', String(filters.minProfitPercent));
  if (filters.minProfitSilver != null) params.set('min', String(filters.minProfitSilver));
  if (filters.minTotalProfitSilver != null) params.set('mintotal', String(filters.minTotalProfitSilver));
  if (filters.qualityLevel != null) params.set('q', String(filters.qualityLevel));
  if (filters.enchantmentLevel != null) params.set('e', String(filters.enchantmentLevel));
  if (filters.limit !== DEFAULT_FILTERS.limit) params.set('limit', String(filters.limit));
  if (filters.items.length > 0) params.set('items', filters.items.map((i) => i.uniqueName).join(','));
  const search = params.toString();
  window.history.replaceState({}, '', search ? `?${search}` : window.location.pathname);
}

function loadFiltersFromUrl(): FlipperFilters | null {
  if (typeof window === 'undefined') return null;
  const params = new URLSearchParams(window.location.search);
  if (params.toString() === '') return null;
  return {
    sourceKeys: parseList(params.get('source')) ?? DEFAULT_FILTERS.sourceKeys,
    sellingKeys: parseList(params.get('selling')) ?? DEFAULT_FILTERS.sellingKeys,
    maxAgeMinutes: parseNumber(params.get('age')),
    minProfitPercent: parseNumber(params.get('pct')),
    minProfitSilver: parseNumber(params.get('min')),
    minTotalProfitSilver: parseNumber(params.get('mintotal')),
    qualityLevel: parseNumber(params.get('q')),
    enchantmentLevel: parseNumber(params.get('e')),
    limit: parseNumber(params.get('limit')) ?? DEFAULT_FILTERS.limit,
    items: parseList(params.get('items'))?.map((uniqueName) => ({ id: 0, uniqueName, localizedName: uniqueName })) ?? [],
  };
}

function parseList(v: string | null): string[] | null {
  if (!v) return null;
  const parts = v.split(',').map((x) => x.trim()).filter(Boolean);
  return parts.length > 0 ? parts : null;
}

function parseNumber(v: string | null): number | null {
  if (!v) return null;
  const n = Number(v);
  return Number.isFinite(n) ? n : null;
}
