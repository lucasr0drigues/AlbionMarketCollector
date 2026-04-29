import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside
      [style.width]="collapsed() ? '52px' : '200px'"
      style="background:var(--color-surface);border-right:1px solid var(--color-border);display:flex;flex-direction:column;flex-shrink:0;height:100%;overflow:hidden;transition:width 0.2s ease;"
    >
      <!-- Logo row -->
      <div style="height:62px;display:flex;align-items:center;padding:0 14px;border-bottom:1px solid var(--color-border);gap:10px;flex-shrink:0;">
        <div style="width:26px;height:26px;border-radius:6px;background:linear-gradient(135deg,#D6A84F,#A07030);display:flex;align-items:center;justify-content:center;font-weight:700;font-size:14px;color:#1A1000;flex-shrink:0;">A</div>
        @if (!collapsed()) {
          <div>
            <div style="font-size:11px;font-weight:700;color:var(--color-gold);letter-spacing:0.06em;text-transform:uppercase;">Albion</div>
            <div style="font-size:11px;color:var(--color-text-muted);letter-spacing:0.04em;">Market</div>
          </div>
        }
      </div>

      <!-- Nav -->
      <nav style="flex:1;padding:8px 6px;display:flex;flex-direction:column;gap:2px;">
        <!-- Flipper (active) -->
        <button style="display:flex;align-items:center;gap:10px;padding:8px 12px;border-radius:7px;background:var(--color-gold-dim);color:var(--color-gold);font-size:14px;font-weight:600;width:100%;text-align:left;white-space:nowrap;overflow:hidden;border:none;">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
            <path d="M2 4h12M2 8h8M2 12h10" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/>
            <path d="M12 10l3 2-3 2" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
          @if (!collapsed()) { <span>Flipper</span> }
        </button>

        <!-- History (disabled) -->
        <button style="display:flex;align-items:center;gap:10px;padding:8px 12px;border-radius:7px;background:transparent;color:var(--color-text-muted);font-size:14px;width:100%;text-align:left;white-space:nowrap;overflow:hidden;border:none;cursor:not-allowed;opacity:0.5;">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none"><circle cx="8" cy="8" r="6" stroke="currentColor" stroke-width="1.5"/><path d="M8 5v3l2 2" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>
          @if (!collapsed()) {
            <span>History</span>
            <span style="margin-left:auto;border-radius:4px;background:var(--color-surface-3);padding:1px 5px;font-size:9px;font-weight:600;letter-spacing:0.05em;text-transform:uppercase;color:var(--color-text-faint);">Soon</span>
          }
        </button>

        <!-- Reference (disabled) -->
        <button style="display:flex;align-items:center;gap:10px;padding:8px 12px;border-radius:7px;background:transparent;color:var(--color-text-muted);font-size:14px;width:100%;text-align:left;white-space:nowrap;overflow:hidden;border:none;cursor:not-allowed;opacity:0.5;">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none"><rect x="2" y="2" width="12" height="12" rx="2" stroke="currentColor" stroke-width="1.5"/><path d="M5 6h6M5 9h4" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>
          @if (!collapsed()) {
            <span>Reference</span>
            <span style="margin-left:auto;border-radius:4px;background:var(--color-surface-3);padding:1px 5px;font-size:9px;font-weight:600;letter-spacing:0.05em;text-transform:uppercase;color:var(--color-text-faint);">Soon</span>
          }
        </button>
      </nav>

      <!-- Settings -->
      <div style="padding:8px 6px;border-top:1px solid var(--color-border);">
        <button style="display:flex;align-items:center;gap:10px;padding:8px 12px;border-radius:7px;background:transparent;color:var(--color-text-muted);font-size:14px;width:100%;text-align:left;white-space:nowrap;overflow:hidden;border:none;cursor:not-allowed;opacity:0.5;">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none"><circle cx="8" cy="8" r="2.5" stroke="currentColor" stroke-width="1.5"/><path d="M8 1v2M8 13v2M1 8h2M13 8h2M3.05 3.05l1.41 1.41M11.54 11.54l1.41 1.41M3.05 12.95l1.41-1.41M11.54 4.46l1.41-1.41" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>
          @if (!collapsed()) { Settings }
        </button>
      </div>
    </aside>
  `,
})
export class SidebarComponent {
  readonly collapsed = input<boolean>(false);
}
