import { ChangeDetectionStrategy, Component, EventEmitter, Output, computed, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MultiSelectComponent, MultiSelectOption } from '../../../shared/ui/multi-select.component';
import { ItemMultiSelectComponent } from './item-multiselect.component';
import { FlipperFilters, QUALITY_OPTIONS, ENCHANTMENT_OPTIONS } from '../state/flipper-filters';
import { SOURCE_LOCATIONS, SELLING_LOCATIONS } from '../../../core/domain/locations';
import { ItemSearchResult } from '../../../core/models';

const AGE_OPTIONS: Array<{ label: string; value: number | null }> = [
  { label: '15m', value: 15 },
  { label: '1h', value: 60 },
  { label: '6h', value: 360 },
  { label: '24h', value: 1440 },
  { label: 'Any', value: null },
];

const PROFIT_PCT_OPTIONS: Array<{ label: string; value: number | null }> = [
  { label: '10%', value: 10 },
  { label: '25%', value: 25 },
  { label: '50%', value: 50 },
  { label: '100%', value: 100 },
  { label: 'Any', value: null },
];

@Component({
  selector: 'app-flipper-filter-bar',
  standalone: true,
  imports: [FormsModule, MultiSelectComponent, ItemMultiSelectComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div style="background:var(--color-surface);border-bottom:1px solid var(--color-border);padding:14px 20px;display:flex;flex-direction:column;gap:8px;flex-shrink:0;">
      <!-- Main filter row -->
      <div style="display:flex;align-items:flex-end;gap:14px;flex-wrap:wrap;">

        <!-- Source cities -->
        <div style="min-width:200px;">
          <ui-multi-select
            label="Source Cities"
            placeholder="All cities"
            [options]="sourceOptions()"
            [selectedKeys]="filters().sourceKeys"
            (selectedKeysChange)="patch({ sourceKeys: $event })"
            [showChips]="false"
          />
        </div>

        <!-- Extra space between Source and Selling -->
        <div style="width:12px;flex-shrink:0;"></div>

        <!-- Selling at -->
        <div style="min-width:200px;">
          <ui-multi-select
            label="Selling At"
            placeholder="All destinations"
            [options]="sellingOptions()"
            [selectedKeys]="filters().sellingKeys"
            (selectedKeysChange)="patch({ sellingKeys: $event })"
            [showChips]="false"
          />
        </div>

        <!-- Divider -->
        <div style="width:1px;height:44px;background:var(--color-border);flex-shrink:0;"></div>

        <!-- Max Age chips -->
        <div style="display:flex;flex-direction:column;gap:4px;">
          <span style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.06em;font-weight:600;">Max Age</span>
          <div style="display:flex;gap:3px;">
            @for (opt of ageOptions; track opt.label) {
              <button
                type="button"
                (click)="patch({ maxAgeMinutes: opt.value })"
                [style.border-color]="ageEqual(opt.value) ? 'rgba(214,168,79,0.3)' : 'var(--color-border)'"
                [style.background]="ageEqual(opt.value) ? 'var(--color-gold-dim)' : 'transparent'"
                [style.color]="ageEqual(opt.value) ? 'var(--color-gold)' : 'var(--color-text-muted)'"
                [style.font-weight]="ageEqual(opt.value) ? '600' : '400'"
                style="padding:4px 10px;border-radius:6px;border:1px solid;font-size:11px;cursor:pointer;transition:all 0.12s;white-space:nowrap;"
              >{{ opt.label }}</button>
            }
          </div>
        </div>

        <!-- Divider -->
        <div style="width:1px;height:44px;background:var(--color-border);flex-shrink:0;"></div>

        <!-- Min Profit % chips -->
        <div style="display:flex;flex-direction:column;gap:4px;">
          <span style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.06em;font-weight:600;">Min Profit %</span>
          <div style="display:flex;gap:3px;">
            @for (opt of profitPctOptions; track opt.label) {
              <button
                type="button"
                (click)="patch({ minProfitPercent: opt.value })"
                [style.border-color]="pctEqual(opt.value) ? 'rgba(214,168,79,0.3)' : 'var(--color-border)'"
                [style.background]="pctEqual(opt.value) ? 'var(--color-gold-dim)' : 'transparent'"
                [style.color]="pctEqual(opt.value) ? 'var(--color-gold)' : 'var(--color-text-muted)'"
                [style.font-weight]="pctEqual(opt.value) ? '600' : '400'"
                style="padding:4px 10px;border-radius:6px;border:1px solid;font-size:11px;cursor:pointer;transition:all 0.12s;white-space:nowrap;"
              >{{ opt.label }}</button>
            }
          </div>
        </div>

        <!-- Divider -->
        <div style="width:1px;height:44px;background:var(--color-border);flex-shrink:0;"></div>

        <!-- Item search -->
        <div style="min-width:180px;">
          <app-item-multiselect
            [selected]="filters().items"
            (selectedChange)="patch({ items: $event })"
          />
        </div>

        <!-- Divider -->
        <div style="width:1px;height:44px;background:var(--color-border);flex-shrink:0;"></div>

        <!-- Min Total Profit -->
        <div style="display:flex;flex-direction:column;gap:4px;min-width:140px;">
          <span style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.06em;font-weight:600;">Min Total Profit</span>
          <input
            type="number"
            min="0"
            step="1"
            [value]="filters().minTotalProfitSilver ?? ''"
            (input)="onTotalProfitInput($event)"
            placeholder="Any"
            style="height:36px;width:100%;border:1px solid var(--color-border-strong);border-radius:7px;background:var(--color-surface-2);color:var(--color-text);padding:0 10px;font-size:14px;font-family:var(--font-mono);outline:none;transition:border-color 0.15s;"
            onfocus="this.style.borderColor='var(--color-gold)';"
            onblur="this.style.borderColor='var(--color-border-strong)';"
          />
        </div>

        <!-- Min % Profit / Unit -->
        <div style="display:flex;flex-direction:column;gap:4px;min-width:130px;">
          <span style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.06em;font-weight:600;">Min % Profit / Unit</span>
          <input
            type="number"
            min="0"
            step="0.1"
            [value]="filters().minProfitPercent ?? ''"
            (input)="onProfitPctInput($event)"
            placeholder="Any"
            style="height:36px;width:100%;border:1px solid var(--color-border-strong);border-radius:7px;background:var(--color-surface-2);color:var(--color-text);padding:0 10px;font-size:14px;font-family:var(--font-mono);outline:none;transition:border-color 0.15s;"
            onfocus="this.style.borderColor='var(--color-gold)';"
            onblur="this.style.borderColor='var(--color-border-strong)';"
          />
        </div>
      </div>

      <!-- Active chips row -->
      @if (hasActiveChips()) {
        <div style="display:flex;gap:6px;flex-wrap:wrap;">
          @for (key of filters().sourceKeys; track key) {
            <span style="display:inline-flex;align-items:center;gap:5px;font-size:11px;font-weight:500;color:var(--color-blue);background:var(--color-blue-dim);border:1px solid rgba(56,189,248,0.3);border-radius:6px;padding:3px 8px;">
              {{ labelFor(key, 'source') }}
              <button type="button" (click)="removeSourceKey(key)" style="background:none;cursor:pointer;color:var(--color-text-muted);display:flex;padding:0;">
                <svg width="9" height="9" viewBox="0 0 10 10" fill="none"><path d="M2 2l6 6M8 2L2 8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>
              </button>
            </span>
          }
          @for (key of filters().sellingKeys; track key) {
            <span style="display:inline-flex;align-items:center;gap:5px;font-size:11px;font-weight:500;color:var(--color-purple);background:var(--color-purple-dim);border:1px solid rgba(167,139,250,0.3);border-radius:6px;padding:3px 8px;">
              {{ labelFor(key, 'selling') }}
              <button type="button" (click)="removeSellingKey(key)" style="background:none;cursor:pointer;color:var(--color-text-muted);display:flex;padding:0;">
                <svg width="9" height="9" viewBox="0 0 10 10" fill="none"><path d="M2 2l6 6M8 2L2 8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>
              </button>
            </span>
          }
          @for (item of filters().items; track item.uniqueName) {
            <span style="display:inline-flex;align-items:center;gap:5px;font-size:11px;font-weight:500;color:var(--color-gold);background:var(--color-gold-dim);border:1px solid rgba(214,168,79,0.3);border-radius:6px;padding:3px 8px;">
              {{ item.localizedName }}
              <button type="button" (click)="removeItem(item)" style="background:none;cursor:pointer;color:var(--color-text-muted);display:flex;padding:0;">
                <svg width="9" height="9" viewBox="0 0 10 10" fill="none"><path d="M2 2l6 6M8 2L2 8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>
              </button>
            </span>
          }
        </div>
      }
    </div>
  `,
})
export class FlipperFilterBarComponent {
  readonly filters = input.required<FlipperFilters>();
  @Output() readonly filtersChange = new EventEmitter<FlipperFilters>();

  readonly ageOptions = AGE_OPTIONS;
  readonly profitPctOptions = PROFIT_PCT_OPTIONS;

  readonly sourceOptions = computed<MultiSelectOption[]>(() =>
    SOURCE_LOCATIONS.map((o) => ({ key: o.key, label: o.label })),
  );

  readonly sellingOptions = computed<MultiSelectOption[]>(() =>
    SELLING_LOCATIONS.map((o) => ({ key: o.key, label: o.label })),
  );

  readonly hasActiveChips = computed(() =>
    this.filters().sourceKeys.length > 0 ||
    this.filters().sellingKeys.length > 0 ||
    this.filters().items.length > 0,
  );

  ageEqual(value: number | null): boolean {
    return this.filters().maxAgeMinutes === value;
  }

  pctEqual(value: number | null): boolean {
    return this.filters().minProfitPercent === value;
  }

  labelFor(key: string, type: 'source' | 'selling'): string {
    const list = type === 'source' ? SOURCE_LOCATIONS : SELLING_LOCATIONS;
    return list.find((o) => o.key === key)?.label ?? key;
  }

  removeSourceKey(key: string): void {
    this.patch({ sourceKeys: this.filters().sourceKeys.filter((k) => k !== key) });
  }

  removeSellingKey(key: string): void {
    this.patch({ sellingKeys: this.filters().sellingKeys.filter((k) => k !== key) });
  }

  removeItem(item: ItemSearchResult): void {
    this.patch({ items: this.filters().items.filter((i) => i.uniqueName !== item.uniqueName) });
  }

  onTotalProfitInput(event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    const n = raw === '' ? null : Number(raw);
    this.patch({ minTotalProfitSilver: n !== null && Number.isFinite(n) ? n : null });
  }

  onProfitPctInput(event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    const n = raw === '' ? null : Number(raw);
    this.patch({ minProfitPercent: n !== null && Number.isFinite(n) ? n : null });
  }

  patch(partial: Partial<FlipperFilters>): void {
    this.filtersChange.emit({ ...this.filters(), ...partial });
  }
}
