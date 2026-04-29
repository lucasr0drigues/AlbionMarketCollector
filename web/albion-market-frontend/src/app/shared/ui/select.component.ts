import { ChangeDetectionStrategy, Component, EventEmitter, input, Output } from '@angular/core';

export interface SelectOption<T = unknown> {
  value: T;
  label: string;
}

@Component({
  selector: 'ui-select',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="block">
      @if (label()) {
        <span class="mb-1.5 block text-[11px] font-semibold uppercase tracking-wide text-text-muted">
          {{ label() }}
        </span>
      }
      <select
        [disabled]="disabled()"
        (change)="onChange($event)"
        class="focus-ring h-9 w-full appearance-none rounded-md border border-border bg-surface-2 pl-3 pr-8 text-sm text-text hover:border-border-strong focus:border-gold disabled:cursor-not-allowed disabled:opacity-60"
        style="background-image: url(&quot;data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 12 12' fill='none' stroke='%23A1A1AA' stroke-width='1.5'><path d='M3 4.5l3 3 3-3'/></svg>&quot;); background-repeat: no-repeat; background-position: right 0.6rem center; background-size: 12px;"
      >
        @for (option of options(); track option.value) {
          <option
            [value]="option.value"
            [selected]="isSelected(option.value)"
          >{{ option.label }}</option>
        }
      </select>
    </label>
  `,
})
export class SelectComponent<T> {
  readonly label = input<string | null>(null);
  readonly options = input.required<SelectOption<T>[]>();
  readonly value = input<T | null>(null);
  readonly disabled = input<boolean>(false);

  @Output() readonly valueChange = new EventEmitter<T | null>();

  isSelected(optionValue: T): boolean {
    return this.toKey(optionValue) === this.toKey(this.value());
  }

  onChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const option = this.options().find((o) => this.toKey(o.value) === select.value);
    this.valueChange.emit(option ? option.value : null);
  }

  private toKey(value: T | null | undefined): string {
    if (value === null || value === undefined) {
      return '';
    }
    return String(value);
  }
}
