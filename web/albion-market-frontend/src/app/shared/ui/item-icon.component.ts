import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { itemImageUrl } from '../../core/market-api.service';

@Component({
  selector: 'ui-item-icon',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span [class]="sizeClasses()" class="relative inline-flex shrink-0 overflow-hidden rounded-md border border-border bg-surface-2">
      <img
        [src]="src()"
        [alt]="alt()"
        loading="lazy"
        class="size-full object-contain"
      />
      @if (enchantmentLevel() > 0) {
        <span class="pointer-events-none absolute -bottom-0.5 -right-0.5 inline-flex min-w-[18px] items-center justify-center rounded-tl-md rounded-br-md bg-gold px-1 text-[10px] font-bold leading-[14px] text-bg">
          +{{ enchantmentLevel() }}
        </span>
      }
    </span>
  `,
})
export class ItemIconComponent {
  readonly uniqueName = input.required<string>();
  readonly enchantmentLevel = input<number>(0);
  readonly qualityLevel = input<number>(0);
  readonly alt = input<string>('');
  readonly size = input<'sm' | 'md' | 'lg'>('md');

  readonly sizeClasses = computed(() => {
    switch (this.size()) {
      case 'sm': return 'size-7';
      case 'lg': return 'size-12';
      case 'md':
      default:   return 'size-10';
    }
  });

  readonly src = computed(() => itemImageUrl(this.uniqueName(), this.enchantmentLevel(), this.qualityLevel()));
}
