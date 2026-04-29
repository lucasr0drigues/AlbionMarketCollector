import { ChangeDetectionStrategy, Component, EventEmitter, Output, input } from '@angular/core';
import { formatAgeMinutes } from '../core/domain/freshness';

function ageColor(minutes: number): string {
  if (minutes < 15) return 'var(--color-profit)';
  if (minutes < 60) return 'var(--color-warn)';
  return 'var(--color-danger)';
}

@Component({
  selector: 'app-topbar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header style="height:62px;background:var(--color-surface);border-bottom:1px solid var(--color-border);display:flex;align-items:center;padding:0 20px 0 16px;gap:12px;flex-shrink:0;">
      <button
        (click)="sidebarToggle.emit()"
        style="background:transparent;border:none;cursor:pointer;color:var(--color-text-muted);padding:4px;border-radius:4px;display:flex;align-items:center;transition:all 0.1s;"
        onmouseenter="this.style.color='var(--color-text)';this.style.background='var(--color-surface-2)';"
        onmouseleave="this.style.color='var(--color-text-muted)';this.style.background='transparent';"
        aria-label="Toggle sidebar"
      >
        <svg width="16" height="16" viewBox="0 0 16 16" fill="none"><path d="M2 4h12M2 8h12M2 12h12" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>
      </button>

      <div style="flex:1;">
        <div style="font-size:11px;color:var(--color-text-muted);letter-spacing:0.06em;text-transform:uppercase;font-weight:500;">Albion Market Collector</div>
        <div style="font-size:17px;font-weight:700;color:var(--color-text);letter-spacing:-0.01em;line-height:1.2;">Item Flipper</div>
      </div>

      @if (freshnessAge() !== null) {
        <div style="display:flex;align-items:center;gap:8px;background:var(--color-surface-2);border:1px solid var(--color-border);border-radius:8px;padding:5px 12px;">
          <span [style.background]="dotColor()" [style.box-shadow]="'0 0 6px ' + dotColor()" style="width:6px;height:6px;border-radius:50%;flex-shrink:0;display:block;"></span>
          <div>
            <div style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.05em;font-weight:600;">Freshest data</div>
            <div [style.color]="dotColor()" class="mono" style="font-size:14px;font-weight:600;line-height:1.2;">{{ formatAge(freshnessAge()!) }} ago</div>
          </div>
        </div>
      }

      <button
        (click)="clearCities.emit()"
        [disabled]="loading() || clearing()"
        style="display:flex;align-items:center;gap:6px;padding:6px 12px;border-radius:8px;background:var(--color-surface-2);border:1px solid var(--color-border);color:var(--color-text-muted);font-size:13px;font-weight:600;cursor:pointer;transition:all 0.15s;"
        onmouseenter="this.style.color='var(--color-text)';this.style.borderColor='rgba(214,168,79,0.35)';"
        onmouseleave="this.style.color='var(--color-text-muted)';this.style.borderColor='var(--color-border)';"
      >
        Clear Cities
      </button>

      <button
        (click)="clearBlackMarket.emit()"
        [disabled]="loading() || clearing()"
        style="display:flex;align-items:center;gap:6px;padding:6px 12px;border-radius:8px;background:var(--color-surface-2);border:1px solid var(--color-border);color:var(--color-text-muted);font-size:13px;font-weight:600;cursor:pointer;transition:all 0.15s;"
        onmouseenter="this.style.color='var(--color-text)';this.style.borderColor='rgba(214,168,79,0.35)';"
        onmouseleave="this.style.color='var(--color-text-muted)';this.style.borderColor='var(--color-border)';"
      >
        Clear Black Market
      </button>

      <button
        (click)="refresh.emit()"
        [disabled]="loading() || clearing()"
        style="display:flex;align-items:center;gap:6px;padding:6px 14px;border-radius:8px;background:var(--color-gold-dim);border:1px solid rgba(214,168,79,0.25);color:var(--color-gold);font-size:14px;font-weight:600;cursor:pointer;transition:all 0.15s;"
        onmouseenter="this.style.background='rgba(214,168,79,0.2)';"
        onmouseleave="this.style.background='var(--color-gold-dim)';"
      >
        <svg
          width="14" height="14" viewBox="0 0 14 14" fill="none"
          [style.animation]="loading() || clearing() ? 'spin 1s linear infinite' : 'none'"
        >
          <path d="M12 7A5 5 0 1 1 7 2a5 5 0 0 1 3.54 1.46L12 2v4H8l1.56-1.56A3 3 0 1 0 10 7" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
        {{ loading() || clearing() ? 'Loading...' : 'Refresh' }}
      </button>
    </header>
  `,
})
export class TopbarComponent {
  readonly loading = input<boolean>(false);
  readonly clearing = input<boolean>(false);
  readonly freshnessAge = input<number | null>(null);

  @Output() readonly refresh = new EventEmitter<void>();
  @Output() readonly clearCities = new EventEmitter<void>();
  @Output() readonly clearBlackMarket = new EventEmitter<void>();
  @Output() readonly sidebarToggle = new EventEmitter<void>();

  dotColor(): string {
    return ageColor(this.freshnessAge() ?? 999);
  }

  formatAge(minutes: number): string {
    return formatAgeMinutes(minutes);
  }
}
