import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'ui-skeleton-rows',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="divide-y divide-border">
      @for (_ of placeholderRows(); track $index) {
        <div class="flex items-center gap-4 px-4 py-3">
          <div class="size-9 animate-pulse rounded bg-surface-2"></div>
          <div class="flex-1 space-y-2">
            <div class="h-3 w-1/3 animate-pulse rounded bg-surface-2"></div>
            <div class="h-2 w-1/5 animate-pulse rounded bg-surface-2/70"></div>
          </div>
          <div class="h-3 w-16 animate-pulse rounded bg-surface-2"></div>
          <div class="h-3 w-16 animate-pulse rounded bg-surface-2"></div>
          <div class="h-3 w-20 animate-pulse rounded bg-surface-2"></div>
        </div>
      }
    </div>
  `,
})
export class LoadingStateComponent {
  readonly count = input<number>(8);

  placeholderRows(): readonly null[] {
    return new Array(this.count()).fill(null);
  }
}
