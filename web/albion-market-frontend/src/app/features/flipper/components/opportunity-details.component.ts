import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { FlipOpportunity } from '../../../core/models';
import { SilverPipe } from '../../../core/formatting/silver.pipe';
import { rowAgeMinutes } from '../../../core/domain/freshness';
import { qualityMeta, extractTier, extractEnchant, TIER_COLORS } from '../../../core/domain/quality';

function ageColor(m: number): string {
  if (m < 15) return 'var(--color-profit)';
  if (m < 60) return 'var(--color-warn)';
  return 'var(--color-danger)';
}

function fmtAge(m: number): string {
  const total = Math.round(m);
  if (total < 60) return `${total}m`;
  const h = Math.floor(total / 60);
  const min = total % 60;
  return min === 0 ? `${h}h` : `${h}h ${min}m`;
}

@Component({
  selector: 'app-opportunity-details',
  standalone: true,
  imports: [SilverPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`
    .details-close-button {
      background: var(--color-surface-2);
      border: 1px solid var(--color-border-strong);
      border-radius: 6px;
      cursor: pointer;
      color: var(--color-text-muted);
      padding: 5px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
      margin-top: 2px;
      transition: color 0.1s;
    }

    .details-close-button:hover {
      color: var(--color-text);
    }
  `],
  template: `
    <div style="display:flex;flex-direction:column;height:100%;overflow-y:auto;">
      <!-- Header with item -->
      <div style="padding:16px 18px 12px;border-bottom:1px solid var(--color-border);flex-shrink:0;">
        <div style="display:flex;align-items:flex-start;justify-content:space-between;margin-bottom:10px;">
          <div>
            <div style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.07em;font-weight:600;margin-bottom:4px;">Opportunity Details</div>
            <div style="font-size:17px;font-weight:700;color:var(--color-text);letter-spacing:-0.01em;">{{ opp().itemLocalizedName }}</div>
          </div>
          <button
            (click)="close()"
            class="details-close-button"
            aria-label="Close"
          >
            <svg width="11" height="11" viewBox="0 0 10 10" fill="none"><path d="M2 2l6 6M8 2L2 8" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>
          </button>
        </div>

        <!-- Item identity -->
        <div style="display:flex;align-items:center;gap:10px;">
          <div
            [style.border]="'2px solid ' + qualityBorder()"
            [style.box-shadow]="qualityGlow() ? '0 0 10px ' + qualityBorder() + '50' : 'none'"
            style="width:44px;height:44px;border-radius:8px;background:var(--color-surface-2);display:flex;align-items:center;justify-content:center;flex-shrink:0;overflow:hidden;"
          >
            <img [src]="imgUrl()" [alt]="opp().itemLocalizedName" loading="lazy" style="width:100%;height:100%;object-fit:contain;"/>
          </div>
          <div>
            <div class="mono" style="font-size:11px;color:var(--color-text-muted);margin-bottom:5px;">{{ opp().itemUniqueName }}</div>
            <div style="display:flex;gap:5px;flex-wrap:wrap;">
              @if (tier()) {
                <span
                  [style.color]="tierColor()"
                  [style.background]="tierColor() + '18'"
                  [style.border]="'1px solid ' + tierColor() + '30'"
                  class="mono"
                  style="font-size:11px;font-weight:600;border-radius:4px;padding:1px 5px;letter-spacing:0.03em;"
                >{{ tier() }}</span>
              }
              @if (enchant()) {
                <span style="font-size:11px;font-weight:600;color:var(--color-purple);background:var(--color-purple-dim);border:1px solid rgba(167,139,250,0.3);border-radius:4px;padding:1px 6px;">
                  +{{ enchant().replace('@','') }} enchantment
                </span>
              }
            </div>
          </div>
        </div>
      </div>

      <!-- Profit highlight 2-col -->
      <div style="display:grid;grid-template-columns:1fr 1fr;gap:1px;border-bottom:1px solid var(--color-border);flex-shrink:0;">
        <div style="padding:12px 18px;background:var(--color-profit-dim);">
          <div style="font-size:11px;color:var(--color-profit);text-transform:uppercase;letter-spacing:0.07em;font-weight:600;opacity:0.8;margin-bottom:4px;">Profit / Unit</div>
          <div class="mono" style="font-size:17px;font-weight:700;color:var(--color-profit);letter-spacing:-0.02em;">+{{ opp().profitPerItemSilver | silver }}</div>
          <div class="mono" style="font-size:11px;color:var(--color-profit);opacity:0.7;">{{ opp().profitPercent.toFixed(1) }}%</div>
        </div>
        <div style="padding:12px 18px;background:var(--color-profit-dim);">
          <div style="font-size:11px;color:var(--color-profit);text-transform:uppercase;letter-spacing:0.07em;font-weight:600;opacity:0.8;margin-bottom:4px;">Total Profit</div>
          <div class="mono" style="font-size:17px;font-weight:700;color:var(--color-profit);letter-spacing:-0.02em;">+{{ opp().estimatedTotalProfitSilver | silver }}</div>
          <div class="mono" style="font-size:11px;color:var(--color-profit);opacity:0.7;">{{ opp().maxTradableAmount }} × unit</div>
        </div>
      </div>

      <!-- Trade route -->
      <div style="padding:14px 18px;border-bottom:1px solid var(--color-border);flex-shrink:0;">
        <div style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.07em;font-weight:600;margin-bottom:10px;">Trade Route</div>
        <div style="display:flex;align-items:stretch;gap:8px;">
          <!-- Buy -->
          <div style="flex:1;background:var(--color-surface-2);border:1px solid rgba(56,189,248,0.2);border-radius:8px;padding:10px 12px;">
            <div style="display:flex;align-items:center;gap:5px;margin-bottom:6px;">
              <svg width="10" height="10" viewBox="0 0 10 10" fill="none"><circle cx="5" cy="5" r="4" stroke="var(--color-blue)" stroke-width="1.3"/></svg>
              <span style="font-size:11px;color:var(--color-blue);text-transform:uppercase;letter-spacing:0.07em;font-weight:700;">Buy At</span>
            </div>
            <div style="font-weight:600;color:var(--color-text);font-size:14px;margin-bottom:2px;">{{ opp().sourceLocationName }}</div>
            <div class="mono" style="font-size:11px;color:var(--color-text-muted);margin-bottom:8px;">City Market</div>
            <div class="mono" style="font-size:14px;font-weight:600;color:var(--color-blue);">{{ opp().sellPriceSilver | silver }}</div>
            <div class="mono" style="font-size:11px;color:var(--color-text-muted);margin-top:1px;">{{ opp().sellPriceSilver | silver }} silver</div>
          </div>
          <!-- Arrow -->
          <div style="display:flex;align-items:center;color:var(--color-text-faint);flex-shrink:0;">
            <svg width="14" height="14" viewBox="0 0 14 14" fill="none"><path d="M2 7h10M8 3l4 4-4 4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>
          </div>
          <!-- Sell -->
          <div style="flex:1;background:var(--color-surface-2);border:1px solid rgba(167,139,250,0.2);border-radius:8px;padding:10px 12px;">
            <div style="display:flex;align-items:center;gap:5px;margin-bottom:6px;">
              <svg width="10" height="10" viewBox="0 0 10 10" fill="none"><circle cx="5" cy="5" r="4" stroke="var(--color-purple)" stroke-width="1.3"/></svg>
              <span style="font-size:11px;color:var(--color-purple);text-transform:uppercase;letter-spacing:0.07em;font-weight:700;">Sell At</span>
            </div>
            <div style="font-weight:600;color:var(--color-text);font-size:14px;margin-bottom:2px;">{{ opp().sellingLocationName }}</div>
            <div class="mono" style="font-size:11px;color:var(--color-text-muted);margin-bottom:8px;">Buy Order</div>
            <div class="mono" style="font-size:14px;font-weight:600;color:var(--color-purple);">{{ opp().buyPriceSilver | silver }}</div>
            <div class="mono" style="font-size:11px;color:var(--color-text-muted);margin-top:1px;">{{ opp().buyPriceSilver | silver }} silver</div>
          </div>
        </div>
      </div>

      <!-- Orders -->
      <div style="padding:14px 18px;border-bottom:1px solid var(--color-border);flex-shrink:0;">
        <div style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.07em;font-weight:600;margin-bottom:10px;">Orders</div>
        <div style="display:flex;flex-direction:column;gap:6px;">
          <div style="display:flex;justify-content:space-between;align-items:center;">
            <span style="font-size:11px;color:var(--color-text-muted);">Sell order</span>
            <span class="mono" style="font-size:11px;color:var(--color-blue);">#{{ opp().sellOrderId }} · {{ opp().sellAmount }} avail.</span>
          </div>
          <div style="display:flex;justify-content:space-between;align-items:center;">
            <span style="font-size:11px;color:var(--color-text-muted);">Buy order</span>
            <span class="mono" style="font-size:11px;color:var(--color-purple);">#{{ opp().buyOrderId }} · {{ opp().buyAmount }} wanted</span>
          </div>
          <div style="display:flex;justify-content:space-between;align-items:center;margin-top:2px;">
            <span style="font-size:11px;color:var(--color-text-muted);">Tradable</span>
            <span class="mono" style="font-size:11px;color:var(--color-text);">{{ opp().maxTradableAmount }} units</span>
          </div>
        </div>
      </div>

      <!-- Data freshness -->
      <div style="padding:14px 18px;flex-shrink:0;">
        <div style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.07em;font-weight:600;margin-bottom:10px;">Data Freshness</div>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:10px;">
          <div style="background:var(--color-surface-2);border:1px solid var(--color-border);border-radius:7px;padding:9px 11px;">
            <div style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.06em;font-weight:600;margin-bottom:5px;">Buy Order Seen</div>
            <div style="display:flex;align-items:center;gap:5px;">
              <span [style.background]="ageColor(opp().buyAgeMinutes)" [style.box-shadow]="'0 0 5px ' + ageColor(opp().buyAgeMinutes) + '80'" style="width:6px;height:6px;border-radius:50%;flex-shrink:0;display:block;"></span>
              <span class="mono" [style.color]="ageColor(opp().buyAgeMinutes)" style="font-size:11px;font-weight:500;">{{ fmtAge(opp().buyAgeMinutes) }}</span>
            </div>
          </div>
          <div style="background:var(--color-surface-2);border:1px solid var(--color-border);border-radius:7px;padding:9px 11px;">
            <div style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.06em;font-weight:600;margin-bottom:5px;">Sell Order Seen</div>
            <div style="display:flex;align-items:center;gap:5px;">
              <span [style.background]="ageColor(opp().sellAgeMinutes)" [style.box-shadow]="'0 0 5px ' + ageColor(opp().sellAgeMinutes) + '80'" style="width:6px;height:6px;border-radius:50%;flex-shrink:0;display:block;"></span>
              <span class="mono" [style.color]="ageColor(opp().sellAgeMinutes)" style="font-size:11px;font-weight:500;">{{ fmtAge(opp().sellAgeMinutes) }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Age footer -->
      <div style="margin-top:auto;padding:10px 18px;border-top:1px solid var(--color-border);display:flex;align-items:center;gap:8px;flex-shrink:0;">
        <span [style.background]="ageColor(overallAge())" [style.box-shadow]="'0 0 5px ' + ageColor(overallAge()) + '80'" style="width:6px;height:6px;border-radius:50%;flex-shrink:0;display:block;"></span>
        <span class="mono" [style.color]="ageColor(overallAge())" style="font-size:11px;font-weight:500;">{{ fmtAge(overallAge()) }}</span>
        <span style="font-size:11px;color:var(--color-text-muted);">oldest data point</span>
      </div>
    </div>
  `,
})
export class OpportunityDetailsComponent {
  readonly opp = input.required<FlipOpportunity>();
  readonly onCloseCallback = input<() => void>();

  close() {
    this.onCloseCallback()?.();
  }

  readonly overallAge = computed(() => rowAgeMinutes(this.opp().buyAgeMinutes, this.opp().sellAgeMinutes));

  qualityBorder(): string { return qualityMeta(this.opp().qualityLevel).border; }
  qualityGlow(): boolean  { return qualityMeta(this.opp().qualityLevel).glow; }
  tier(): string | null   { return extractTier(this.opp().itemUniqueName); }
  enchant(): string       { return extractEnchant(this.opp().itemUniqueName); }
  tierColor(): string     { return TIER_COLORS[this.tier() ?? ''] ?? '#71717A'; }

  imgUrl(): string {
    const d = this.opp();
    const base = d.enchantmentLevel > 0 && !d.itemUniqueName.includes('@')
      ? `${d.itemUniqueName}@${d.enchantmentLevel}` : d.itemUniqueName;
    return `https://render.albiononline.com/v1/item/${base}`;
  }

  ageColor(m: number): string {
    if (m < 15) return 'var(--color-profit)';
    if (m < 60) return 'var(--color-warn)';
    return 'var(--color-danger)';
  }

  fmtAge(m: number): string {
    return fmtAge(m);
  }
}
