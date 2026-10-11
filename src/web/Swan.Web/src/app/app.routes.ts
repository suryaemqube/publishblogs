import { Routes } from '@angular/router';
import { LoginComponent } from './login/login.component';
import { ShellComponent } from './layout/shell.component';
import { SitesComponent } from './sites/sites.component';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: 'sites', component: SitesComponent },
      { path: '', pathMatch: 'full', redirectTo: 'sites' },
      { path: '**', redirectTo: 'sites' },
    ],
  },
];
