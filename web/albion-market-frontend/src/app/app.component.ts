import { ChangeDetectionStrategy, Component } from '@angular/core';
import { FlipperPageComponent } from './features/flipper/pages/flipper-page.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [FlipperPageComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { style: 'display:contents' },
  template: `<app-flipper-page />`,
})
export class AppComponent {}
