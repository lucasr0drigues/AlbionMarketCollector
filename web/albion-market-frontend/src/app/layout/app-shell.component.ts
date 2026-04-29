import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { SidebarComponent } from './sidebar.component';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [SidebarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div style="display:flex;height:100%;overflow:hidden;background:var(--color-bg);color:var(--color-text);">
      <app-sidebar [collapsed]="collapsed()" />
      <div style="flex:1;display:flex;flex-direction:column;overflow:hidden;min-width:0;">
        <ng-content select="[topbar]" />
        <ng-content />
      </div>
    </div>
  `,
})
export class AppShellComponent {
  readonly collapsed = signal(false);

  toggleSidebar(): void {
    this.collapsed.update((v) => !v);
  }
}
