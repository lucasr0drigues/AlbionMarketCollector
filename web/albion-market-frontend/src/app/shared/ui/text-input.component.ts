import { ChangeDetectionStrategy, Component, EventEmitter, input, Output } from '@angular/core';
import { LucideAngularModule, SearchIcon } from 'lucide-angular';

@Component({
  selector: 'ui-text-input',
  standalone: true,
  imports: [LucideAngularModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="block">
      @if (label()) {
        <span class="mb-1.5 block text-[11px] font-semibold uppercase tracking-wide text-text-muted">
          {{ label() }}
        </span>
      }
      <span class="relative flex items-center">
        @if (icon()) {
          <span class="pointer-events-none absolute left-3 text-text-faint">
            <lucide-icon [name]="SearchIcon" [size]="14" />
          </span>
        }
        <input
          [type]="type()"
          [value]="value() ?? ''"
          [placeholder]="placeholder() ?? ''"
          [min]="min() ?? null"
          [step]="step() ?? null"
          [disabled]="disabled()"
          (input)="onInput($event)"
          [class.pl-9]="icon()"
          [class.pl-3]="!icon()"
          class="focus-ring h-9 w-full rounded-md border border-border bg-surface-2 pr-3 text-sm text-text placeholder:text-text-faint hover:border-border-strong focus:border-gold disabled:cursor-not-allowed disabled:opacity-60 tabular-nums"
        />
      </span>
    </label>
  `,
})
export class TextInputComponent {
  readonly label = input<string | null>(null);
  readonly placeholder = input<string | null>(null);
  readonly value = input<string | number | null>(null);
  readonly type = input<'text' | 'number'>('text');
  readonly min = input<number | null>(null);
  readonly step = input<number | null>(null);
  readonly disabled = input<boolean>(false);
  readonly icon = input<boolean>(false);

  @Output() readonly valueChange = new EventEmitter<string>();

  readonly SearchIcon = SearchIcon;

  onInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.valueChange.emit(target.value);
  }
}
