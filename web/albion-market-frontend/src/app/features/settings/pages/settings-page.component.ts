import { ChangeDetectionStrategy, Component, OnDestroy, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, finalize, takeUntil } from 'rxjs';
import { AppShellComponent } from '../../../layout/app-shell.component';
import { MarketApiService } from '../../../core/market-api.service';

@Component({
  selector: 'app-settings-page',
  standalone: true,
  imports: [AppShellComponent, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-shell #shell>
      <header
        topbar
        style="height:62px;background:var(--color-surface);border-bottom:1px solid var(--color-border);display:flex;align-items:center;padding:0 20px 0 16px;gap:12px;flex-shrink:0;"
      >
        <button
          (click)="shell.toggleSidebar()"
          aria-label="Toggle sidebar"
          style="background:transparent;border:none;cursor:pointer;color:var(--color-text-muted);padding:4px;border-radius:4px;display:flex;align-items:center;"
        >
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none"><path d="M2 4h12M2 8h12M2 12h12" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>
        </button>
        <div>
          <div style="font-size:11px;color:var(--color-text-muted);letter-spacing:0.06em;text-transform:uppercase;font-weight:500;">Albion Market Collector</div>
          <div style="font-size:17px;font-weight:700;color:var(--color-text);letter-spacing:-0.01em;line-height:1.2;">Settings</div>
        </div>
      </header>

      <main style="flex:1;overflow:auto;padding:20px;">
        <section style="max-width:680px;">
          <div style="border-bottom:1px solid var(--color-border);padding-bottom:12px;margin-bottom:16px;">
            <h1 style="font-size:20px;font-weight:700;color:var(--color-text);margin:0 0 4px;">Account</h1>
            <p style="font-size:13px;color:var(--color-text-muted);margin:0;">Market calculations that depend on account state.</p>
          </div>

          <div style="background:var(--color-surface);border:1px solid var(--color-border);border-radius:8px;padding:16px 18px;display:flex;align-items:center;justify-content:space-between;gap:18px;">
            <div style="min-width:0;">
              <div style="font-size:14px;font-weight:700;color:var(--color-text);margin-bottom:3px;">Premium</div>
              <div style="font-size:12px;color:var(--color-text-muted);line-height:1.4;">
                Premium accounts use a 4% market tax. Non-premium accounts use 8%.
              </div>
            </div>

            <button
              type="button"
              (click)="togglePremium()"
              [disabled]="loading() || saving()"
              [attr.aria-pressed]="premium()"
              [style.color]="premium() ? 'var(--color-gold)' : 'var(--color-text-muted)'"
              [style.background]="premium() ? 'var(--color-gold-dim)' : 'var(--color-surface-2)'"
              [style.border-color]="premium() ? 'rgba(214,168,79,0.3)' : 'var(--color-border)'"
              style="display:flex;align-items:center;gap:8px;border:1px solid;border-radius:8px;padding:8px 12px;font-size:13px;font-weight:700;white-space:nowrap;cursor:pointer;"
            >
              <span
                [style.background]="premium() ? 'rgba(214,168,79,0.16)' : 'var(--color-surface-3)'"
                [style.border-color]="premium() ? 'rgba(214,168,79,0.35)' : 'var(--color-border)'"
                style="width:30px;height:16px;border-radius:999px;border:1px solid;position:relative;display:inline-flex;align-items:center;padding:2px;"
              >
                <span
                  [style.transform]="premium() ? 'translateX(14px)' : 'translateX(0)'"
                  [style.background]="premium() ? 'var(--color-gold)' : 'var(--color-text-muted)'"
                  style="width:9px;height:9px;border-radius:50%;display:block;transition:all 0.15s;"
                ></span>
              </span>
              {{ premium() ? 'Yes' : 'No' }}
            </button>
          </div>

          <div style="margin-top:14px;background:var(--color-surface);border:1px solid var(--color-border);border-radius:8px;padding:16px 18px;display:flex;flex-direction:column;gap:14px;">
            <div>
              <div style="font-size:14px;font-weight:700;color:var(--color-text);margin-bottom:3px;">Flipper defaults</div>
              <div style="font-size:12px;color:var(--color-text-muted);line-height:1.4;">
                These values are applied to the flipper filters when the page loads.
              </div>
            </div>

            <label style="display:grid;grid-template-columns:1fr 180px;gap:14px;align-items:center;">
              <span>
                <span style="display:block;font-size:13px;font-weight:600;color:var(--color-text);">Default min total profit</span>
                <span style="display:block;font-size:12px;color:var(--color-text-muted);margin-top:2px;">Silver value shown in the flipper filter.</span>
              </span>
              <input
                type="number"
                min="0"
                step="1"
                [ngModel]="defaultMinTotalProfitSilver()"
                (ngModelChange)="defaultMinTotalProfitSilver.set(normalizeNumber($event))"
                style="height:36px;border-radius:6px;border:1px solid var(--color-border);background:var(--color-surface-2);color:var(--color-text);padding:0 10px;font-size:13px;"
              />
            </label>

            <label style="display:grid;grid-template-columns:1fr 180px;gap:14px;align-items:center;">
              <span>
                <span style="display:block;font-size:13px;font-weight:600;color:var(--color-text);">Default min % profit/unit</span>
                <span style="display:block;font-size:12px;color:var(--color-text-muted);margin-top:2px;">Percentage value shown in the flipper filter.</span>
              </span>
              <input
                type="number"
                min="0"
                step="0.1"
                [ngModel]="defaultMinProfitPercent()"
                (ngModelChange)="defaultMinProfitPercent.set(normalizeNumber($event))"
                style="height:36px;border-radius:6px;border:1px solid var(--color-border);background:var(--color-surface-2);color:var(--color-text);padding:0 10px;font-size:13px;"
              />
            </label>

            <div style="display:flex;justify-content:flex-end;">
              <button
                type="button"
                (click)="saveSettings()"
                [disabled]="loading() || saving()"
                style="height:34px;border-radius:7px;border:1px solid rgba(214,168,79,0.3);background:var(--color-gold-dim);color:var(--color-gold);font-size:13px;font-weight:700;padding:0 14px;cursor:pointer;"
              >
                Save Defaults
              </button>
            </div>
          </div>

          @if (error()) {
            <div style="margin-top:14px;color:var(--color-danger);font-size:13px;">{{ error() }}</div>
          }
          @if (saving()) {
            <div style="margin-top:14px;color:var(--color-text-muted);font-size:13px;">Saving settings...</div>
          }
        </section>
      </main>
    </app-shell>
  `,
})
export class SettingsPageComponent implements OnDestroy {
  private readonly api = inject(MarketApiService);
  private readonly destroy$ = new Subject<void>();

  readonly premium = signal(false);
  readonly defaultMinTotalProfitSilver = signal<number | null>(null);
  readonly defaultMinProfitPercent = signal<number | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    this.api.getSettings()
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (settings) => {
          this.premium.set(settings.premium);
          this.defaultMinTotalProfitSilver.set(settings.defaultMinTotalProfitSilver);
          this.defaultMinProfitPercent.set(settings.defaultMinProfitPercent);
        },
        error: () => this.error.set('Could not load settings. Make sure the API is running.'),
      });
  }

  togglePremium(): void {
    const next = !this.premium();
    this.premium.set(next);
    this.saveSettingsRequest(next, () => this.premium.set(!next));
  }

  saveSettings(): void {
    this.saveSettingsRequest(this.premium());
  }

  normalizeNumber(value: unknown): number | null {
    if (value === null || value === undefined || value === '') {
      return null;
    }

    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
  }

  private saveSettingsRequest(
    premium: boolean,
    rollback?: () => void,
  ): void {
    this.saving.set(true);
    this.error.set(null);

    this.api.updateSettings({
      premium,
      defaultMinTotalProfitSilver: this.defaultMinTotalProfitSilver(),
      defaultMinProfitPercent: this.defaultMinProfitPercent(),
    })
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: (settings) => {
          this.premium.set(settings.premium);
          this.defaultMinTotalProfitSilver.set(settings.defaultMinTotalProfitSilver);
          this.defaultMinProfitPercent.set(settings.defaultMinProfitPercent);
        },
        error: () => {
          rollback?.();
          this.error.set('Could not save settings.');
        },
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}
