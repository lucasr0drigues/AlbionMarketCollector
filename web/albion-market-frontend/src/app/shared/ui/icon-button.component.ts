import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type IconButtonVariant = 'ghost' | 'solid';
export type IconButtonSize = 'sm' | 'md';

@Component({
  selector: 'ui-icon-button',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      [type]="type()"
      [disabled]="disabled()"
      [attr.aria-label]="ariaLabel()"
      [class]="classes()"
      class="focus-ring inline-flex shrink-0 items-center justify-center rounded-md transition-colors disabled:cursor-not-allowed disabled:opacity-60"
    >
      <ng-content />
    </button>
  `,
})
export class IconButtonComponent {
  readonly variant = input<IconButtonVariant>('ghost');
  readonly size = input<IconButtonSize>('md');
  readonly type = input<'button' | 'submit' | 'reset'>('button');
  readonly disabled = input<boolean>(false);
  readonly ariaLabel = input.required<string>();

  readonly classes = computed(() => {
    const sizeClasses = this.size() === 'sm' ? 'h-7 w-7' : 'h-9 w-9';
    const variantClasses = this.variant() === 'solid'
      ? 'bg-surface-2 text-text border border-border hover:bg-surface-3'
      : 'bg-transparent text-text-muted hover:text-text hover:bg-surface-2 border border-transparent';
    return `${sizeClasses} ${variantClasses}`;
  });
}
