import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { SilverPipe } from '../../core/formatting/silver.pipe';
import { ProfitTier } from '../../core/domain/profit-tier';

@Component({
  selector: 'ui-profit-badge',
  standalone: true,
  imports: [SilverPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex items-baseline gap-1.5 tabular-nums">
      <span [class]="silverClasses()" class="font-semibold leading-none">
        {{ silver() | silver }}
      </span>
      @if (percent() != null) {
        <span [class]="percentClasses()" class="rounded px-1 py-px text-[10px] font-semibold leading-none">
          {{ percentLabel() }}
        </span>
      }
    </div>
  `,
})
export class ProfitBadgeComponent {
  readonly silver = input.required<number>();
  readonly percent = input<number | null>(null);
  readonly tier = input<ProfitTier>('mid');
  readonly emphasize = input<boolean>(false);

  readonly silverClasses = computed(() => {
    const base = this.emphasize() ? 'text-[15px]' : 'text-sm';
    switch (this.tier()) {
      case 'top':  return `${base} text-profit-strong`;
      case 'high': return `${base} text-profit`;
      case 'mid':  return `${base} text-profit/80`;
      case 'low':
      default:     return `${base} text-profit/60`;
    }
  });

  readonly percentClasses = computed(() => {
    switch (this.tier()) {
      case 'top':  return 'bg-profit/20 text-profit-strong';
      case 'high': return 'bg-profit/15 text-profit';
      case 'mid':  return 'bg-profit/10 text-profit/80';
      case 'low':
      default:     return 'bg-surface-2 text-text-muted';
    }
  });

  readonly percentLabel = computed(() => {
    const p = this.percent();
    if (p == null) {
      return '';
    }
    return `${p >= 100 ? p.toFixed(0) : p.toFixed(1)}%`;
  });
}
