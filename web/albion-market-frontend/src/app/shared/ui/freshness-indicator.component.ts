import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { formatAgeMinutes, FreshnessTier } from '../../core/domain/freshness';

@Component({
  selector: 'ui-freshness-indicator',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span
      class="inline-flex items-center gap-1.5 text-xs tabular-nums"
      [attr.title]="title()"
    >
      <span [class]="dotClasses()" class="size-2 rounded-full"></span>
      <span [class]="textClasses()">{{ label() }}</span>
    </span>
  `,
})
export class FreshnessIndicatorComponent {
  readonly tier = input.required<FreshnessTier>();
  readonly ageMinutes = input.required<number>();
  readonly buyAgeMinutes = input<number | null>(null);
  readonly sellAgeMinutes = input<number | null>(null);

  readonly label = computed(() => formatAgeMinutes(this.ageMinutes()));

  readonly title = computed(() => {
    const buy = this.buyAgeMinutes();
    const sell = this.sellAgeMinutes();
    if (buy == null && sell == null) {
      return `Data age: ${this.label()}`;
    }
    return `Buy age: ${formatAgeMinutes(buy ?? 0)} · Sell age: ${formatAgeMinutes(sell ?? 0)}`;
  });

  readonly dotClasses = computed(() => {
    switch (this.tier()) {
      case 'fresh': return 'bg-profit shadow-[0_0_0_3px_rgba(34,197,94,0.15)]';
      case 'aging': return 'bg-warn shadow-[0_0_0_3px_rgba(245,158,11,0.15)]';
      case 'stale':
      default:      return 'bg-danger shadow-[0_0_0_3px_rgba(239,68,68,0.15)]';
    }
  });

  readonly textClasses = computed(() => {
    switch (this.tier()) {
      case 'fresh': return 'text-text';
      case 'aging': return 'text-warn';
      case 'stale':
      default:      return 'text-danger';
    }
  });
}
