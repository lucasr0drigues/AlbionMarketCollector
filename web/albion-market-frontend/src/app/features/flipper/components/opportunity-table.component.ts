import { ChangeDetectionStrategy, Component, EventEmitter, Output, computed, effect, input } from '@angular/core';
import { FlipOpportunity } from '../../../core/models';
import { SilverPipe } from '../../../core/formatting/silver.pipe';
import { rowAgeMinutes } from '../../../core/domain/freshness';
import { buildProfitTierMap, ProfitTier } from '../../../core/domain/profit-tier';
import { qualityMeta, extractTier, extractEnchant, TIER_COLORS } from '../../../core/domain/quality';
import { SOURCE_LOCATIONS, SELLING_LOCATIONS } from '../../../core/domain/locations';
import { itemImageUrl } from '../../../core/market-api.service';
import { profitAfterMarketTax, totalProfitAfterMarketTax } from '../../../core/domain/market-tax';

export type SortKey = 'item' | 'quality' | 'buyPrice' | 'route' | 'sellPrice' | 'profit' | 'total' | 'qty' | 'freshness';
export type SortDirection = 'asc' | 'desc';

// Item Quality Buy@Source Route Sell@Dest Profit/Unit Total Qty Age Chevron
const COL_GRID = '1.5fr 0.5fr 0.85fr 1.0fr 0.85fr 1.1fr 0.95fr 0.35fr 0.65fr 28px';

function ageColor(minutes: number): string {
  if (minutes < 15) return 'var(--color-profit)';
  if (minutes < 60) return 'var(--color-warn)';
  return 'var(--color-danger)';
}

function fmtAge(m: number): string {
  const total = Math.round(m);
  if (total < 60) return `${total}m`;
  const h = Math.floor(total / 60);
  const min = total % 60;
  return min === 0 ? `${h}h` : `${h}h ${min}m`;
}

interface Row {
  data: FlipOpportunity;
  ageMinutes: number;
  tier: string | null;
  enchant: string;
  qualityLabel: string;
  qualityColor: string;
  sourceLabel: string;
  destLabel: string;
  isBM: boolean;
  profitTier: ProfitTier;
  totalTier: ProfitTier;
}

const sourceMap = new Map(SOURCE_LOCATIONS.flatMap((o) => o.ids.map((id) => [id, o.label] as const)));
const sellingMap = new Map(SELLING_LOCATIONS.flatMap((o) => o.ids.map((id) => [id, o.label] as const)));

const COLS: Array<{ key: SortKey | null; label: string; align: 'left' | 'right' | 'center' }> = [
  { key: 'item',      label: 'ITEM',          align: 'left' },
  { key: 'quality',   label: 'QUALITY',        align: 'center' },
  { key: 'buyPrice',  label: 'BUY @ SOURCE',   align: 'right' },
  { key: 'route',     label: 'ROUTE',          align: 'left' },
  { key: 'sellPrice', label: 'SELL @ DEST',    align: 'right' },
  { key: 'profit',    label: 'PROFIT / UNIT',  align: 'right' },
  { key: 'total',     label: 'TOTAL PROFIT',   align: 'right' },
  { key: 'qty',       label: 'QTY',            align: 'center' },
  { key: 'freshness', label: 'AGE',            align: 'right' },
  { key: null,        label: '',               align: 'center' },
];

@Component({
  selector: 'app-opportunity-table',
  standalone: true,
  host: { style: 'flex:1;overflow:hidden;display:flex;flex-direction:column;min-width:0;' },
  imports: [SilverPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    .sort-header {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 11px;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.07em;
      background: none;
      cursor: pointer;
      transition: color 0.12s;
      white-space: nowrap;
      border: none;
    }

    .sort-header:disabled {
      cursor: default;
    }

    .opportunity-row {
      display: grid;
      align-items: center;
      gap: 0 8px;
      padding: 10px 16px;
      border-bottom: 1px solid var(--color-border);
      cursor: pointer;
      transition: background 0.1s;
      position: relative;
      background: transparent;
    }

    .opportunity-row:hover {
      background: rgba(255,255,255,0.028);
    }

    .opportunity-row-selected,
    .opportunity-row-selected:hover {
      background: rgba(255,255,255,0.05);
    }

    .pager-button {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 30px;
      height: 28px;
      border-radius: 5px;
      border: 1px solid var(--color-border);
      background: transparent;
      color: var(--color-text-muted);
      cursor: pointer;
      transition: all 0.1s;
      font-size: 12px;
    }

    .pager-button:hover:not(:disabled) {
      background: var(--color-surface-3);
      color: var(--color-text);
    }

    .pager-button:disabled {
      cursor: not-allowed;
      opacity: 0.45;
    }

    .pager-button-active,
    .pager-button-active:hover:not(:disabled) {
      background: var(--color-gold-dim);
      color: var(--color-gold);
      border-color: rgba(214,168,79,0.3);
      font-weight: 600;
    }
  `],
  template: `
      <!-- Header -->
      <div [style.grid-template-columns]="colGrid" style="display:grid;gap:0 8px;padding:7px 16px;border-bottom:1px solid var(--color-border);flex-shrink:0;">
        @for (col of cols; track col.label) {
          <button
            type="button"
            [disabled]="col.key === null"
            (click)="col.key && setSort(col.key)"
            [style.justify-content]="col.align === 'right' ? 'flex-end' : col.align === 'center' ? 'center' : 'flex-start'"
            [style.color]="sortKey() === col.key ? 'var(--color-gold)' : 'var(--color-text-muted)'"
            [style.padding-right]="col.key === 'buyPrice' ? '24px' : '0'"
            class="sort-header"
          >
            {{ col.label }}
            @if (col.key !== null) {
              <svg width="10" height="10" viewBox="0 0 10 10" fill="none">
                <path d="M5 2L8 5H2L5 2Z" [attr.fill]="sortKey() === col.key && sortDirection() === 'desc' ? 'var(--color-gold)' : 'var(--color-border-strong)'" />
                <path d="M5 8L8 5H2L5 8Z" [attr.fill]="sortKey() === col.key && sortDirection() === 'asc' ? 'var(--color-gold)' : 'var(--color-border-strong)'" />
              </svg>
            }
          </button>
        }
      </div>

      <!-- Rows -->
      <div style="flex:1;overflow-y:auto;">
        @for (row of rows(); track rowKey(row); let isTop = $first) {
          <div
            (click)="rowClick.emit(row.data)"
            class="opportunity-row"
            [class.opportunity-row-selected]="selected() === row.data"
            [style.border-left]="borderLeft(row, selected() === row.data)"
            [style.grid-template-columns]="colGrid"
          >
            <!-- Item -->
            <div style="display:flex;align-items:center;gap:9px;min-width:0;">
              <div
                class="item-icon-hover"
                style="width:42px;height:42px;border-radius:6px;background:var(--color-surface-2);display:flex;align-items:center;justify-content:center;flex-shrink:0;overflow:hidden;"
              >
                <img
                  [src]="imgUrl(row.data)"
                  [alt]="row.data.itemLocalizedName"
                  loading="lazy"
                  style="width:100%;height:100%;object-fit:contain;"
                />
              </div>
              <div style="min-width:0;">
                <div style="font-size:14px;font-weight:600;color:var(--color-text);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">{{ row.data.itemLocalizedName }}</div>
                @if (row.tier) {
                  <span
                    [style.color]="tierColor(row.tier)"
                    [style.background]="tierColor(row.tier) + '18'"
                    [style.border]="'1px solid ' + tierColor(row.tier) + '30'"
                    class="mono"
                    style="display:inline-flex;align-items:center;gap:1px;font-size:11px;font-weight:600;border-radius:4px;padding:1px 5px;letter-spacing:0.03em;flex-shrink:0;"
                  >
                    {{ row.tier }}{{ row.enchant && ('@' + row.enchant.replace('@', '')) }}
                  </span>
                }
              </div>
            </div>

            <!-- Quality -->
            <div style="display:flex;justify-content:center;">
              <span [style.color]="row.qualityColor" style="font-size:11px;font-weight:600;white-space:nowrap;letter-spacing:0.01em;">
                {{ row.qualityLabel }}
              </span>
            </div>

            <!-- Buy @ Source (extra right padding separates it from Route) -->
            <div style="text-align:right;padding-right:24px;">
              <span class="mono" style="font-size:12px;color:var(--color-blue);">{{ row.data.sellPriceSilver | silver }}</span>
            </div>

            <!-- Route: Source → Dest -->
            <div style="display:flex;align-items:center;gap:6px;">
              <span [style.color]="'var(--color-blue)'" style="display:inline-block;font-size:11px;font-weight:500;background:var(--color-blue-dim);border:1px solid rgba(56,189,248,0.3);border-radius:4px;padding:2px 7px;white-space:nowrap;">
                {{ row.sourceLabel }}
              </span>
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none" style="flex-shrink:0;opacity:0.5;">
                <path d="M2 7h10M8 3l4 4-4 4" stroke="var(--color-text-faint)" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
              </svg>
              <span
                [style.color]="row.isBM ? 'var(--color-purple)' : 'var(--color-blue)'"
                [style.background]="row.isBM ? 'var(--color-purple-dim)' : 'var(--color-blue-dim)'"
                [style.border-color]="row.isBM ? 'rgba(167,139,250,0.3)' : 'rgba(56,189,248,0.3)'"
                style="display:inline-block;font-size:11px;font-weight:500;border:1px solid;border-radius:4px;padding:2px 7px;white-space:nowrap;"
              >
                {{ row.isBM ? '⬡ ' : '' }}{{ row.destLabel }}
              </span>
            </div>

            <!-- Sell @ Dest (no extra left gap, sits close to Route) -->
            <div style="text-align:right;padding-left:0;">
              <span class="mono" style="font-size:12px;color:var(--color-purple);">{{ row.data.buyPriceSilver | silver }}</span>
            </div>

            <!-- Profit / Unit -->
            <div style="display:flex;flex-direction:column;align-items:flex-end;gap:2px;">
              <span [style.color]="profitColor(row.profitTier)" class="mono" style="font-size:14px;font-weight:700;">+{{ netProfitPerItem(row.data) | silver }}</span>
              <span [style.color]="profitColor(row.profitTier)" [style.background]="profitBg(row.profitTier)" [style.border]="'1px solid ' + profitColor(row.profitTier) + '30'" class="mono" style="font-size:11px;font-weight:600;border-radius:3px;padding:1px 5px;">
                +{{ netProfitPercent(row.data).toFixed(netProfitPercent(row.data) >= 100 ? 0 : 1) }}%
              </span>
            </div>

            <!-- Total Profit -->
            <div style="text-align:right;">
              <span [style.color]="profitColor(row.totalTier)" class="mono" style="font-size:12px;font-weight:600;">+{{ netEstimatedTotalProfit(row.data) | silver }}</span>
            </div>

            <!-- Qty -->
            <div style="text-align:center;">
              <span class="mono" [style.color]="row.data.maxTradableAmount >= 3 ? 'var(--color-text)' : 'var(--color-text-muted)'" style="font-size:14px;font-weight:600;">{{ row.data.maxTradableAmount }}</span>
            </div>

            <!-- Age -->
            <div style="display:flex;justify-content:flex-end;align-items:center;gap:5px;">
              <span [style.background]="ageColor(row.ageMinutes)" [style.box-shadow]="'0 0 5px ' + ageColor(row.ageMinutes) + '80'" style="width:6px;height:6px;border-radius:50%;flex-shrink:0;display:block;"></span>
              <span class="mono" [style.color]="ageColor(row.ageMinutes)" style="font-size:11px;font-weight:500;">{{ fmtAge(row.ageMinutes) }}</span>
            </div>

            <!-- Chevron -->
            <div style="display:flex;justify-content:center;color:var(--color-text-muted);">
              <svg width="11" height="11" viewBox="0 0 12 12" fill="none"><path d="M4.5 2.5L7.5 6l-3 3.5" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>
            </div>
          </div>
        } @empty {
          <ng-content select="[empty]" />
        }
      </div>

      <!-- Totals row -->
      @if (allRows().length > 0) {
        <div [style.grid-template-columns]="colGrid" style="display:grid;align-items:center;gap:0 8px;padding:9px 16px;border-top:1px solid var(--color-border-strong);background:var(--color-surface-2);flex-shrink:0;">
          <div style="font-size:11px;font-weight:700;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.06em;">{{ allRows().length }} rows</div>
          <div></div>
          <!-- Total buy value -->
          <div style="text-align:right;padding-right:24px;">
            <div style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.06em;margin-bottom:2px;">total buy</div>
            <span class="mono" style="font-size:12px;color:var(--color-blue);">{{ totalBuyValue() | silver }}</span>
          </div>
          <div></div>
          <div></div>
          <!-- Avg profit/unit -->
          <div style="display:flex;flex-direction:column;align-items:flex-end;gap:2px;">
            <div style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.06em;margin-bottom:2px;">avg/unit</div>
            <span class="mono" style="font-size:12px;color:var(--color-profit);">+{{ avgProfitPerUnit() | silver }}</span>
          </div>
          <!-- Total profit -->
          <div style="text-align:right;">
            <div style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.06em;margin-bottom:2px;">total</div>
            <span class="mono" style="font-size:12px;color:var(--color-profit);">+{{ totalProfit() | silver }}</span>
          </div>
          <!-- Total qty -->
          <div style="text-align:center;">
            <div style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.06em;margin-bottom:2px;">qty</div>
            <span class="mono" style="font-size:12px;color:var(--color-text);">{{ totalQty() }}</span>
          </div>
          <div></div>
          <div></div>
        </div>
      }

      <!-- Pagination -->
      @if (totalPages() > 1) {
        <div style="display:flex;align-items:center;justify-content:space-between;gap:12px;padding:8px 16px;border-top:1px solid var(--color-border);background:var(--color-surface-2);flex-shrink:0;">
          <span class="mono" style="font-size:11px;color:var(--color-text-muted);">
            {{ pageStart() }}–{{ pageEnd() }} of {{ totalCount() }}
          </span>
          <div style="display:flex;align-items:center;gap:2px;">
            <button type="button" (click)="prevPage()" [disabled]="page() <= 1" class="pager-button">
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none"><path d="M8 3L4 7l4 4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>
            </button>

            @for (pageNumber of visiblePageNumbers(); track $index) {
              @if (pageNumber === -1) {
                <span style="width:28px;text-align:center;font-size:12px;color:var(--color-text-faint);">…</span>
              } @else {
                <button
                  type="button"
                  (click)="goToPage(pageNumber)"
                  class="pager-button"
                  [class.pager-button-active]="page() === pageNumber"
                >{{ pageNumber }}</button>
              }
            }

            <button type="button" (click)="nextPage()" [disabled]="!hasMore()" class="pager-button">
              <svg width="14" height="14" viewBox="0 0 14 14" fill="none"><path d="M6 3l4 4-4 4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>
            </button>
          </div>
        </div>
      }
  `,
})
export class OpportunityTableComponent {
  readonly opportunities = input.required<FlipOpportunity[]>();
  readonly selected = input<FlipOpportunity | null>(null);
  readonly sortKey = input<SortKey>('total');
  readonly sortDirection = input<SortDirection>('desc');
  readonly page = input(1);
  readonly pageSize = input(48);
  readonly totalCount = input(0);
  readonly totalPages = input(0);
  readonly hasMore = input(false);
  readonly taxRate = input(0);

  @Output() readonly rowClick = new EventEmitter<FlipOpportunity>();
  @Output() readonly sortChange = new EventEmitter<{ key: SortKey; direction: SortDirection }>();
  @Output() readonly pageChange = new EventEmitter<number>();
  @Output() readonly visibleCountChange = new EventEmitter<number>();

  readonly colGrid = COL_GRID;
  readonly cols = COLS;

  constructor() {
    effect(() => {
      this.visibleCountChange.emit(this.rows().length);
    });
  }

  readonly allRows = computed<Row[]>(() => {
    const data = this.opportunities();
    const profitTiers = buildProfitTierMap(data, (r) => this.netProfitPerItem(r));
    const totalTiers = buildProfitTierMap(data, (r) => this.netEstimatedTotalProfit(r));
    return data.map<Row>((d) => {
      const q = qualityMeta(d.qualityLevel);
      const isBlackMarket = d.sellingLocationId === '3003' || d.sellingLocationName?.toLowerCase().includes('black market');
      return {
        data: d,
        ageMinutes: rowAgeMinutes(d.buyAgeMinutes, d.sellAgeMinutes),
        tier: extractTier(d.itemUniqueName),
        enchant: extractEnchant(d.itemUniqueName),
        qualityLabel: q.label,
        qualityColor: q.color,
        sourceLabel: sourceMap.get(d.sourceLocationId) ?? d.sourceLocationName,
        destLabel: sellingMap.get(d.sellingLocationId) ?? d.sellingLocationName,
        isBM: isBlackMarket,
        profitTier: profitTiers.get(d) ?? 'mid',
        totalTier: totalTiers.get(d) ?? 'mid',
      };
    });
  });

  readonly rows = computed<Row[]>(() => this.allRows());

  readonly pageStart = computed(() => this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1);
  readonly pageEnd = computed(() => Math.min(this.page() * this.pageSize(), this.totalCount()));

  readonly totalBuyValue = computed(() => this.opportunities().reduce((s, o) => s + o.sellPriceSilver * o.maxTradableAmount, 0));
  readonly totalProfit = computed(() => this.opportunities().reduce((s, o) => s + this.netEstimatedTotalProfit(o), 0));
  readonly avgProfitPerUnit = computed(() => {
    const opps = this.opportunities();
    if (opps.length === 0) return 0;
    return opps.reduce((s, o) => s + this.netProfitPerItem(o), 0) / opps.length;
  });
  readonly totalQty = computed(() => this.opportunities().reduce((s, o) => s + o.maxTradableAmount, 0));

  imgUrl(d: FlipOpportunity): string {
    return itemImageUrl(d.itemUniqueName, d.enchantmentLevel, d.qualityLevel);
  }

  netProfitPerItem(d: FlipOpportunity): number {
    return profitAfterMarketTax(d.buyPriceSilver, d.sellPriceSilver, this.taxRate());
  }

  netEstimatedTotalProfit(d: FlipOpportunity): number {
    return totalProfitAfterMarketTax(d.buyPriceSilver, d.sellPriceSilver, d.maxTradableAmount, this.taxRate());
  }

  netProfitPercent(d: FlipOpportunity): number {
    if (d.sellPriceSilver === 0) {
      return 0;
    }

    return this.netProfitPerItem(d) / d.sellPriceSilver * 100;
  }

  tierColor(tier: string): string {
    return TIER_COLORS[tier] ?? '#71717A';
  }

  ageColor(minutes: number): string {
    return ageColor(minutes);
  }

  fmtAge(minutes: number): string {
    return fmtAge(minutes);
  }

  rowKey(row: Row): string {
    return `${row.data.buyOrderId}-${row.data.sellOrderId}`;
  }

  profitColor(tier: ProfitTier): string {
    switch (tier) {
      case 'top':  return 'var(--color-profit-strong)';
      case 'high': return 'var(--color-profit)';
      case 'mid':  return 'rgba(34,197,94,0.8)';
      default:     return 'rgba(34,197,94,0.6)';
    }
  }

  profitBg(tier: ProfitTier): string {
    switch (tier) {
      case 'top':
      case 'high': return 'var(--color-profit-dim)';
      default:     return 'transparent';
    }
  }

  borderLeft(row: Row, isSelected: boolean): string {
    if (isSelected) return '2px solid var(--color-gold)';
    if (row.totalTier === 'top') return '2px solid var(--color-profit)';
    return '2px solid transparent';
  }

  setSort(key: SortKey): void {
    if (this.sortKey() === key) {
      this.sortChange.emit({ key, direction: this.sortDirection() === 'asc' ? 'desc' : 'asc' });
      return;
    }
    const direction: SortDirection = (key === 'item' || key === 'route' || key === 'freshness') ? 'asc' : 'desc';
    this.sortChange.emit({ key, direction });
  }

  prevPage(): void {
    this.pageChange.emit(Math.max(1, this.page() - 1));
  }

  nextPage(): void {
    if (this.hasMore()) {
      this.pageChange.emit(this.page() + 1);
    }
  }

  goToPage(page: number): void {
    this.pageChange.emit(Math.max(1, page));
  }

  visiblePageNumbers(): number[] {
    const total = this.totalPages();
    const current = this.page();
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
    const pages: number[] = [1];
    const start = Math.max(2, current - 1);
    const end = Math.min(total - 1, current + 1);
    if (start > 2) pages.push(-1);
    for (let i = start; i <= end; i++) pages.push(i);
    if (end < total - 1) pages.push(-1);
    pages.push(total);
    return pages;
  }
}
