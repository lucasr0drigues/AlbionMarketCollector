import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ButtonSize = 'sm' | 'md';

@Component({
  selector: 'ui-button',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      [type]="type()"
      [disabled]="disabled()"
      [attr.aria-label]="ariaLabel()"
      [class]="classes()"
      class="focus-ring inline-flex items-center justify-center gap-2 rounded-md font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-60"
    >
      <ng-content />
    </button>
  `,
})
export class ButtonComponent {
  readonly variant = input<ButtonVariant>('secondary');
  readonly size = input<ButtonSize>('md');
  readonly type = input<'button' | 'submit' | 'reset'>('button');
  readonly disabled = input<boolean>(false);
  readonly ariaLabel = input<string | null>(null);

  readonly classes = computed(() => {
    const variant = this.variant();
    const size = this.size();

    const sizeClasses = size === 'sm'
      ? 'h-8 px-3 text-xs'
      : 'h-9 px-3.5 text-sm';

    const variantClasses = (() => {
      switch (variant) {
        case 'primary':
          return 'bg-gold text-bg hover:bg-gold-strong border border-gold';
        case 'danger':
          return 'bg-danger/10 text-danger hover:bg-danger/20 border border-danger/30';
        case 'ghost':
          return 'bg-transparent text-text-muted hover:text-text hover:bg-surface-2 border border-transparent';
        case 'secondary':
        default:
          return 'bg-surface-2 text-text hover:bg-surface-3 border border-border';
      }
    })();

    return `${sizeClasses} ${variantClasses}`;
  });
}
