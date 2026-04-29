import { ChangeDetectionStrategy, Component, EventEmitter, Output, computed, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, ChevronDownIcon, XIcon, CheckIcon } from 'lucide-angular';
import { PopoverDirective } from './popover.directive';

export interface MultiSelectOption<T = string> {
  key: T;
  label: string;
  hint?: string;
}

@Component({
  selector: 'ui-multi-select',
  standalone: true,
  imports: [FormsModule, LucideAngularModule, PopoverDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="block">
      @if (label()) {
        <span class="mb-1.5 block text-[11px] font-semibold uppercase tracking-wide text-text-muted">
          {{ label() }}
        </span>
      }

      <button
        type="button"
        [uiPopoverTrigger]="popoverContent"
        class="focus-ring flex h-9 w-full items-center justify-between gap-2 rounded-md border border-border bg-surface-2 px-3 text-sm text-text hover:border-border-strong"
      >
        <span class="truncate text-left">
          @if (selectedKeys().length === 0) {
            <span class="text-text-faint">{{ placeholder() }}</span>
          } @else if (selectedKeys().length === 1) {
            {{ selectedLabel() }}
          } @else {
            {{ selectedKeys().length }} selected
          }
        </span>
        <lucide-icon [name]="ChevronDownIcon" [size]="14" class="text-text-faint" />
      </button>

      @if (selectedKeys().length > 0 && showChips()) {
        <div class="mt-2 flex flex-wrap gap-1.5">
          @for (key of selectedKeys(); track key) {
            <button
              type="button"
              (click)="toggle(key)"
              class="focus-ring inline-flex items-center gap-1 rounded-md border border-border bg-surface-2 px-2 py-1 text-xs text-text hover:border-danger/40 hover:text-danger"
            >
              {{ labelFor(key) }}
              <lucide-icon [name]="XIcon" [size]="12" />
            </button>
          }
        </div>
      }
    </div>

    <ng-template #popoverContent>
      <div class="w-full min-w-[260px] overflow-hidden rounded-md border border-border bg-surface shadow-2xl shadow-black/40">
        <div class="border-b border-border p-2">
          <input
            [ngModel]="searchText()"
            (ngModelChange)="searchText.set($event)"
            type="text"
            [placeholder]="searchPlaceholder()"
            class="focus-ring h-8 w-full rounded border border-border bg-surface-2 px-2 text-sm text-text placeholder:text-text-faint focus:border-gold"
          />
        </div>
        <ul class="max-h-72 overflow-auto py-1">
          @for (option of filteredOptions(); track option.key) {
            <li>
              <button
                type="button"
                (click)="toggle(option.key)"
                class="flex w-full items-center gap-2 px-3 py-1.5 text-left text-sm text-text hover:bg-surface-2"
              >
                <span class="flex size-4 items-center justify-center rounded border" [class]="isSelected(option.key) ? 'border-gold bg-gold/15 text-gold' : 'border-border'">
                  @if (isSelected(option.key)) {
                    <lucide-icon [name]="CheckIcon" [size]="11" />
                  }
                </span>
                <span class="flex-1 truncate">{{ option.label }}</span>
                @if (option.hint) {
                  <span class="text-[10px] text-text-faint">{{ option.hint }}</span>
                }
              </button>
            </li>
          } @empty {
            <li class="px-3 py-3 text-center text-xs text-text-muted">No matches</li>
          }
        </ul>
        <div class="flex items-center justify-between border-t border-border bg-surface-2 px-2 py-1.5">
          <button
            type="button"
            (click)="clear()"
            class="focus-ring rounded px-2 py-1 text-xs text-text-muted hover:text-text"
          >Clear</button>
          <span class="text-[10px] text-text-faint">{{ selectedKeys().length }}/{{ options().length }}</span>
        </div>
      </div>
    </ng-template>
  `,
})
export class MultiSelectComponent<T extends string = string> {
  readonly label = input<string | null>(null);
  readonly placeholder = input<string>('Any');
  readonly searchPlaceholder = input<string>('Search…');
  readonly options = input.required<MultiSelectOption<T>[]>();
  readonly selectedKeys = input.required<T[]>();
  readonly showChips = input<boolean>(true);

  @Output() readonly selectedKeysChange = new EventEmitter<T[]>();

  readonly searchText = signal('');

  readonly ChevronDownIcon = ChevronDownIcon;
  readonly XIcon = XIcon;
  readonly CheckIcon = CheckIcon;

  readonly filteredOptions = computed(() => {
    const term = this.searchText().trim().toLowerCase();
    if (!term) {
      return this.options();
    }
    return this.options().filter((option) =>
      option.label.toLowerCase().includes(term) ||
      option.key.toLowerCase().includes(term),
    );
  });

  readonly selectedLabel = computed(() => {
    const key = this.selectedKeys()[0];
    if (!key) {
      return '';
    }
    return this.options().find((o) => o.key === key)?.label ?? key;
  });

  isSelected(key: T): boolean {
    return this.selectedKeys().includes(key);
  }

  labelFor(key: T): string {
    return this.options().find((o) => o.key === key)?.label ?? key;
  }

  toggle(key: T): void {
    const current = this.selectedKeys();
    const next = current.includes(key)
      ? current.filter((k) => k !== key)
      : [...current, key];
    this.selectedKeysChange.emit(next);
  }

  clear(): void {
    this.selectedKeysChange.emit([]);
  }
}
