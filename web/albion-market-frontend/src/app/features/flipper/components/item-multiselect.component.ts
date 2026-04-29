import { ChangeDetectionStrategy, Component, EventEmitter, Output, computed, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideAngularModule, ChevronDownIcon, XIcon, SearchIcon } from 'lucide-angular';
import { Subject, debounceTime, distinctUntilChanged, of, switchMap, takeUntil } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import { MarketApiService } from '../../../core/market-api.service';
import { ItemSearchResult } from '../../../core/models';
import { PopoverDirective } from '../../../shared/ui/popover.directive';
import { ItemIconComponent } from '../../../shared/ui/item-icon.component';

@Component({
  selector: 'app-item-multiselect',
  standalone: true,
  imports: [FormsModule, LucideAngularModule, PopoverDirective, ItemIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="block">
      <span class="mb-1.5 block text-[11px] font-semibold uppercase tracking-wide text-text-muted">Items</span>

      <button
        type="button"
        [uiPopoverTrigger]="popoverContent"
        (popoverOpened)="onOpen()"
        class="focus-ring flex h-9 w-full items-center justify-between gap-2 rounded-md border border-border bg-surface-2 px-3 text-sm text-text hover:border-border-strong"
      >
        <span class="truncate text-left">
          @if (selected().length === 0) {
            <span class="text-text-faint">All items</span>
          } @else if (selected().length === 1) {
            {{ selected()[0].localizedName }}
          } @else {
            {{ selected().length }} items
          }
        </span>
        <lucide-icon [name]="ChevronDownIcon" [size]="14" class="text-text-faint" />
      </button>

      @if (selected().length > 0) {
        <div class="mt-2 flex flex-wrap gap-1.5">
          @for (item of selected(); track item.uniqueName) {
            <button
              type="button"
              (click)="remove(item)"
              class="focus-ring inline-flex items-center gap-1.5 rounded-md border border-border bg-surface-2 py-0.5 pl-1 pr-1.5 text-xs text-text hover:border-danger/40 hover:text-danger"
            >
              <ui-item-icon [uniqueName]="item.uniqueName" size="sm" [alt]="item.localizedName" />
              <span class="max-w-40 truncate">{{ item.localizedName }}</span>
              <lucide-icon [name]="XIcon" [size]="12" />
            </button>
          }
        </div>
      }
    </div>

    <ng-template #popoverContent>
      <div class="w-[360px] overflow-hidden rounded-md border border-border bg-surface shadow-2xl shadow-black/40">
        <div class="border-b border-border p-2">
          <div class="relative">
            <span class="pointer-events-none absolute left-2 top-1/2 -translate-y-1/2 text-text-faint">
              <lucide-icon [name]="SearchIcon" [size]="13" />
            </span>
            <input
              [ngModel]="searchText()"
              (ngModelChange)="onSearchChange($event)"
              type="text"
              placeholder="Search item names…"
              class="focus-ring h-8 w-full rounded border border-border bg-surface-2 pl-7 pr-2 text-sm text-text placeholder:text-text-faint focus:border-gold"
              autofocus
            />
          </div>
        </div>
        <ul class="max-h-72 overflow-auto py-1">
          @if (loading()) {
            <li class="px-3 py-3 text-center text-xs text-text-muted">Searching…</li>
          } @else {
            @for (item of results(); track item.uniqueName) {
              <li>
                <button
                  type="button"
                  (click)="add(item)"
                  [disabled]="isSelected(item)"
                  class="flex w-full items-center gap-2 px-3 py-1.5 text-left text-sm text-text hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <ui-item-icon [uniqueName]="item.uniqueName" size="sm" [alt]="item.localizedName" />
                  <span class="min-w-0 flex-1">
                    <span class="block truncate text-sm text-text">{{ item.localizedName }}</span>
                    <span class="block truncate text-[10px] text-text-faint">{{ item.uniqueName }}</span>
                  </span>
                </button>
              </li>
            } @empty {
              <li class="px-3 py-3 text-center text-xs text-text-muted">
                @if (searchText().length === 0) { Type to search items. } @else { No matches. }
              </li>
            }
          }
        </ul>
      </div>
    </ng-template>
  `,
})
export class ItemMultiSelectComponent {
  readonly selected = input.required<ItemSearchResult[]>();
  @Output() readonly selectedChange = new EventEmitter<ItemSearchResult[]>();

  private readonly api = inject(MarketApiService);
  private readonly searchTerms$ = new Subject<string>();
  private readonly destroy$ = new Subject<void>();

  readonly searchText = signal('');
  readonly results = signal<ItemSearchResult[]>([]);
  readonly loading = signal(false);

  readonly isSelected = (item: ItemSearchResult) =>
    computed(() => this.selected().some((s) => s.uniqueName === item.uniqueName))();

  readonly ChevronDownIcon = ChevronDownIcon;
  readonly XIcon = XIcon;
  readonly SearchIcon = SearchIcon;

  constructor() {
    this.searchTerms$
      .pipe(
        debounceTime(220),
        distinctUntilChanged(),
        switchMap((term) => {
          this.loading.set(true);
          return this.api.searchItems(term, 25).pipe(
            catchError(() => of([] as ItemSearchResult[])),
            finalize(() => this.loading.set(false)),
          );
        }),
        takeUntil(this.destroy$),
      )
      .subscribe((items) => this.results.set(items));
  }

  onOpen(): void {
    if (this.results().length === 0 && !this.loading()) {
      this.searchTerms$.next(this.searchText());
    }
  }

  onSearchChange(value: string): void {
    this.searchText.set(value);
    this.searchTerms$.next(value);
  }

  add(item: ItemSearchResult): void {
    if (this.selected().some((s) => s.uniqueName === item.uniqueName)) {
      return;
    }
    this.selectedChange.emit([...this.selected(), item]);
  }

  remove(item: ItemSearchResult): void {
    this.selectedChange.emit(this.selected().filter((s) => s.uniqueName !== item.uniqueName));
  }
}
