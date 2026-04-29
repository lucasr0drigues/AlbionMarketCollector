import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { LucideAngularModule, InboxIcon } from 'lucide-angular';

@Component({
  selector: 'ui-empty-state',
  standalone: true,
  imports: [LucideAngularModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col items-center justify-center gap-3 px-6 py-16 text-center">
      <div class="flex size-12 items-center justify-center rounded-full border border-border bg-surface-2 text-text-muted">
        <lucide-icon [name]="InboxIcon" [size]="22" />
      </div>
      <div class="space-y-1">
        <p class="text-sm font-semibold text-text">{{ title() }}</p>
        @if (description()) {
          <p class="max-w-md text-xs text-text-muted">{{ description() }}</p>
        }
      </div>
      <ng-content />
    </div>
  `,
})
export class EmptyStateComponent {
  readonly title = input.required<string>();
  readonly description = input<string | null>(null);
  readonly InboxIcon = InboxIcon;
}
