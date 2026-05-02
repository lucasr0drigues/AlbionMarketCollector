import { Routes } from '@angular/router';
import { FlipperPageComponent } from './features/flipper/pages/flipper-page.component';
import { SettingsPageComponent } from './features/settings/pages/settings-page.component';

export const routes: Routes = [
  { path: '', component: FlipperPageComponent },
  { path: 'settings', component: SettingsPageComponent },
  { path: '**', redirectTo: '' },
];
