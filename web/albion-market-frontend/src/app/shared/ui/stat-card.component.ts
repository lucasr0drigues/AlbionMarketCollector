import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type StatCardAccent = 'gold' | 'green' | 'blue' | 'purple' | 'amber' | 'neutral';

const ACCENT_COLORS: Record<StatCardAccent, string> = {
  gold:    'var(--color-gold)',
  green:   'var(--color-profit)',
  blue:    'var(--color-blue)',
  purple:  'var(--color-purple)',
  amber:   'var(--color-warn)',
  neutral: 'var(--color-border)',
};

@Component({
  selector: 'ui-stat-card',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div style="background:var(--color-surface);border:1px solid var(--color-border);border-radius:10px;padding:14px 18px;display:flex;flex-direction:column;gap:4px;width:100%;min-width:0;position:relative;overflow:hidden;">
      <!-- Top accent stripe -->
      <div [style.background]="accentColor()" style="position:absolute;top:0;left:0;right:0;height:2px;border-radius:10px 10px 0 0;"></div>
      <div style="font-size:11px;color:var(--color-text-muted);text-transform:uppercase;letter-spacing:0.07em;font-weight:600;">{{ label() }}</div>
      <div [style.color]="accentColor()" class="mono" style="font-size:22px;font-weight:700;letter-spacing:-0.03em;line-height:1;">
        <ng-content />
      </div>
      @if (sub()) {
        <div style="font-size:11px;color:var(--color-text-muted);">{{ sub() }}</div>
      }
    </div>
  `,
})
export class StatCardComponent {
  readonly label = input.required<string>();
  readonly accent = input<StatCardAccent>('neutral');
  readonly customColor = input<string | null>(null);
  readonly sub = input<string | null>(null);

  readonly accentColor = computed(() => this.customColor() ?? ACCENT_COLORS[this.accent()] ?? ACCENT_COLORS.neutral);
}
