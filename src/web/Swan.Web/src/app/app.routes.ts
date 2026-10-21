import { Routes } from '@angular/router';
import { LoginComponent } from './login/login.component';
import { ShellComponent } from './layout/shell.component';
import { SitesComponent } from './sites/sites.component';
import { SiteFormComponent } from './sites/site-form.component';
import { DocumentsComponent } from './documents/documents.component';
import { DocumentDetailComponent } from './documents/document-detail.component';
import { ArticlesComponent } from './articles/articles.component';
import { ArticleDetailComponent } from './articles/article-detail.component';
import { TeamComponent } from './team/team.component';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: 'documents', component: DocumentsComponent },
      { path: 'articles', component: ArticlesComponent },
      { path: 'articles/:id', component: ArticleDetailComponent },
      { path: 'documents/:id', component: DocumentDetailComponent },
      { path: 'sites', component: SitesComponent },
      { path: 'sites/new', component: SiteFormComponent },
      { path: 'sites/:id/edit', component: SiteFormComponent },
      { path: 'settings/team', component: TeamComponent },
      { path: '', pathMatch: 'full', redirectTo: 'documents' },
      { path: '**', redirectTo: 'documents' },
    ],
  },
];

