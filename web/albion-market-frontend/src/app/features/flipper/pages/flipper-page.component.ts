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
import { AppSettings, FlipFilters, FlipOpportunity, FlipOpportunityPage } from '../../../core/models';
import { FlipperFilters, DEFAULT_FILTERS, FLIPPER_PAGE_SIZE } from '../state/flipper-filters';
import { formatAgeMinutes, rowAgeMinutes } from '../../../core/domain/freshness';
import { flattenLocationIds, SOURCE_LOCATIONS, SELLING_LOCATIONS } from '../../../core/domain/locations';
import { marketTaxLabel, marketTaxRate, profitAfterMarketTax, totalProfitAfterMarketTax } from '../../../core/domain/market-tax';

function ageColor(minutes: number): string {
  if (minutes < 15) return 'var(--color-profit)';
  if (minutes < 60) return 'var(--color-warn)';
  return 'var(--color-danger)';
}

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
            <ui-stat-card label="Tax" accent="amber" [sub]="premium() ? 'premium enabled' : 'premium disabled'" style="flex:1;min-width:0;">{{ taxLabel() }}</ui-stat-card>
            <ui-stat-card label="Freshest Data" accent="neutral" [customColor]="freshestColor()" sub="most recent update" style="flex:1;min-width:0;">
              @if (freshestAge() !== null) { {{ formattedFreshestAge() }} ago } @else { — }
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
              description="Try widening Max age, lowering Min total profit, lowering Min % profit/unit, or removing item filters."
            />
          } @else {
            <app-opportunity-table
              [opportunities]="filteredOpportunities()"
              [selected]="selected()"
              [sortKey]="sortKey()"
              [sortDirection]="sortDirection()"
              [page]="page()"
              [pageSize]="pageSize"
              [totalCount]="totalCount()"
              [totalPages]="totalPages()"
              [hasMore]="hasMore()"
              [taxRate]="taxRate()"
              (rowClick)="select($event)"
              (sortChange)="onSortChange($event)"
              (pageChange)="goToPage($event)"
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
  private settingsDefaultsApplied = false;

  readonly filters = signal<FlipperFilters>(loadFiltersFromUrl() ?? DEFAULT_FILTERS);
  readonly opportunities = signal<FlipOpportunity[]>([]);
  readonly selected = signal<FlipOpportunity | null>(null);
  readonly loading = signal(false);
  readonly clearing = signal(false);
  readonly error = signal<string | null>(null);
  readonly premium = signal(false);

  readonly sortKey = signal<SortKey>('total');
  readonly sortDirection = signal<SortDirection>('desc');
  readonly visibleCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = FLIPPER_PAGE_SIZE;
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly hasMore = signal(false);

  readonly closeDetailsFn = () => this.closeDetails();

  readonly filteredOpportunities = computed(() => {
    return this.opportunities();
  });

  readonly freshestAge = computed(() => {
    const items = this.filteredOpportunities();
    if (items.length === 0) return null;
    return Math.round(
      items.reduce((best, item) => Math.min(best, rowAgeMinutes(item.buyAgeMinutes, item.sellAgeMinutes)), Infinity),
    );
  });

  readonly topUnitProfit = computed(() =>
    this.filteredOpportunities().reduce((max, item) => Math.max(max, profitAfterMarketTax(item.buyPriceSilver, item.sellPriceSilver, this.taxRate())), 0),
  );

  readonly topTotalProfit = computed(() =>
    this.filteredOpportunities().reduce((max, item) => Math.max(max, totalProfitAfterMarketTax(item.buyPriceSilver, item.sellPriceSilver, item.maxTradableAmount, this.taxRate())), 0),
  );

  readonly taxRate = computed(() => marketTaxRate(this.premium()));
  readonly taxLabel = computed(() => marketTaxLabel(this.taxRate()));
  readonly freshestColor = computed(() => this.freshestAge() === null ? 'var(--color-border)' : ageColor(this.freshestAge()!));
  readonly formattedFreshestAge = computed(() => this.freshestAge() === null ? '—' : formatAgeMinutes(this.freshestAge()!));

  constructor() {
    this.api.getSettings()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (settings) => {
          this.premium.set(settings.premium);
          this.applyDefaultFilters(settings);
        },
        error: () => this.premium.set(false),
      });

    this.fetchTrigger$
      .pipe(
        debounceTime(280),
        switchMap((apiFilters) => {
          this.loading.set(true);
          this.error.set(null);
          return this.api.findBlackMarketFlips(apiFilters).pipe(
            catchError((err) => {
              this.error.set(this.toMessage(err));
              return of(emptyFlipPage(apiFilters.page, apiFilters.pageSize));
            }),
            finalize(() => this.loading.set(false)),
          );
        }),
        takeUntil(this.destroy$),
      )
      .subscribe((data) => {
        const selected = this.selected();
        this.opportunities.set(data.items);
        this.totalCount.set(data.totalCount);
        this.totalPages.set(data.totalPages);
        this.hasMore.set(data.hasMore);
        if (selected) {
          this.selected.set(data.items.find((item) => sameOpportunity(item, selected)) ?? null);
        }
      });

    effect(() => {
      const filters = this.filters();
      const page = this.page();
      const sortKey = this.sortKey();
      const sortDirection = this.sortDirection();
      const taxRate = this.taxRate();
      saveFiltersToUrl(filters);
      this.fetchTrigger$.next(toApiFilters(filters, page, this.pageSize, sortKey, sortDirection, taxRate));
    });
  }

  updateFilters(next: FlipperFilters): void {
    this.page.set(1);
    this.filters.set(next);
  }

  reload(): void {
    this.fetchTrigger$.next(toApiFilters(this.filters(), this.page(), this.pageSize, this.sortKey(), this.sortDirection(), this.taxRate()));
  }

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
    this.page.set(1);
    this.sortKey.set(event.key);
    this.sortDirection.set(event.direction);
  }

  goToPage(page: number): void {
    this.page.set(Math.max(1, page));
  }

  private toMessage(err: unknown): string {
    if (err && typeof err === 'object' && 'message' in err && typeof (err as { message: unknown }).message === 'string') {
      return (err as { message: string }).message;
    }
    return 'API returned an error. Make sure the API is running on http://localhost:5000.';
  }

  private applyDefaultFilters(settings: AppSettings): void {
    if (this.settingsDefaultsApplied) {
      return;
    }

    this.settingsDefaultsApplied = true;
    if (settings.defaultMinTotalProfitSilver === null && settings.defaultMinProfitPercent === null) {
      return;
    }

    this.page.set(1);
    this.filters.update((filters) => ({
      ...filters,
      minTotalProfitSilver: filters.minTotalProfitSilver ?? settings.defaultMinTotalProfitSilver,
      minProfitPercent: filters.minProfitPercent ?? settings.defaultMinProfitPercent,
    }));
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

function toApiFilters(
  filters: FlipperFilters,
  page: number,
  pageSize: number,
  sortBy: SortKey,
  sortDirection: SortDirection,
  marketTaxRate: number,
): FlipFilters {
  return {
    sourceLocationIds: flattenLocationIds(SOURCE_LOCATIONS, filters.sourceKeys),
    excludedSourceLocationIds: filters.sourceKeys.length === 0 ? ['3003'] : [],
    sellingLocationIds: flattenLocationIds(SELLING_LOCATIONS, filters.sellingKeys),
    maxAgeMinutes: filters.maxAgeMinutes,
    minProfitSilver: filters.minProfitSilver,
    minTotalProfitSilver: toStoredSilver(filters.minTotalProfitSilver),
    minProfitPercent: filters.minProfitPercent,
    marketTaxRate,
    itemUniqueNames: filters.items.map((i) => i.uniqueName),
    qualityLevel: filters.qualityLevel,
    enchantmentLevel: filters.enchantmentLevel,
    sortBy,
    sortDirection,
    page,
    pageSize,
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
    items: parseList(params.get('items'))?.map((uniqueName) => ({ id: 0, uniqueName, localizedName: uniqueName })) ?? [],
  };
}

function toStoredSilver(value: number | null): number | null {
  if (value === null || !Number.isFinite(value)) {
    return null;
  }

  return Math.round(value * 10_000);
}

function emptyFlipPage(page: number, pageSize: number): FlipOpportunityPage {
  return {
    items: [],
    page,
    pageSize,
    totalCount: 0,
    totalPages: 0,
    hasMore: false,
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
