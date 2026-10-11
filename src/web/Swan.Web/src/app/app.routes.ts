import { Routes } from '@angular/router';
import { LoginComponent } from './login/login.component';
import { ShellComponent } from './layout/shell.component';
import { SitesComponent } from './sites/sites.component';
import { DocumentsComponent } from './documents/documents.component';
import { DocumentDetailComponent } from './documents/document-detail.component';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: 'documents', component: DocumentsComponent },
      { path: 'documents/:id', component: DocumentDetailComponent },
      { path: 'sites', component: SitesComponent },
      { path: '', pathMatch: 'full', redirectTo: 'sites' },
      { path: '**', redirectTo: 'sites' },
    ],
  },
];

