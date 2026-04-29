import { ChangeDetectionStrategy, Component, EventEmitter, input, Output } from '@angular/core';
import { LucideAngularModule, AlertTriangleIcon, RefreshCwIcon } from 'lucide-angular';
import { ButtonComponent } from './button.component';

@Component({
  selector: 'ui-error-state',
  standalone: true,
  imports: [LucideAngularModule, ButtonComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-col items-center justify-center gap-3 px-6 py-16 text-center">
      <div class="flex size-12 items-center justify-center rounded-full border border-danger/30 bg-danger/10 text-danger">
        <lucide-icon [name]="AlertTriangleIcon" [size]="22" />
      </div>
      <div class="space-y-1">
        <p class="text-sm font-semibold text-text">{{ title() }}</p>
        @if (description()) {
          <p class="max-w-md text-xs text-text-muted">{{ description() }}</p>
        }
      </div>
      <ui-button variant="secondary" size="sm" (click)="retry.emit()">
        <lucide-icon [name]="RefreshCwIcon" [size]="14" />
        Retry
      </ui-button>
    </div>
  `,
})
export class ErrorStateComponent {
  readonly title = input.required<string>();
  readonly description = input<string | null>(null);
  @Output() readonly retry = new EventEmitter<void>();

  readonly AlertTriangleIcon = AlertTriangleIcon;
  readonly RefreshCwIcon = RefreshCwIcon;
}
