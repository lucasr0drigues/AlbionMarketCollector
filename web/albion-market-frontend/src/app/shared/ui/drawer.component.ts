import { ChangeDetectionStrategy, Component, EventEmitter, HostListener, input, Output } from '@angular/core';

@Component({
  selector: 'ui-drawer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <!-- Blur overlay -->
      <div
        (click)="close.emit()"
        style="position:fixed;inset:0;z-index:300;background:rgba(9,9,11,0.55);backdrop-filter:blur(3px);animation:fadeIn 0.18s ease;cursor:default;"
      ></div>
      <!-- Panel -->
      <div
        role="dialog"
        aria-modal="true"
        style="position:fixed;top:0;right:0;bottom:0;z-index:301;width:420px;background:var(--color-surface);border-left:1px solid var(--color-border-strong);display:flex;flex-direction:column;animation:slideIn 0.2s cubic-bezier(0.16,1,0.3,1);box-shadow:-12px 0 40px rgba(0,0,0,0.6);overflow-y:auto;"
      >
        <ng-content />
      </div>
    }
  `,
})
export class DrawerComponent {
  readonly open = input<boolean>(false);
  @Output() readonly close = new EventEmitter<void>();

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.open()) {
      this.close.emit();
    }
  }
}
