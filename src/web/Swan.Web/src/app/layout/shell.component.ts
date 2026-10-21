import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { PermissionKeys } from '../core/permissions';

/**
 * App frame matching prototype-simple: left nav, topbar with the account, client bar, page outlet.
 * Items without a built page are shown but disabled, so the layout matches the prototype.
 * Team is shown only to someone who can manage users, so writers never see a screen they cannot open.
 */
@Component({
  selector: 'swan-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss',
})
export class ShellComponent {
  readonly auth = inject(AuthService);

  get canManageUsers(): boolean {
    return this.auth.hasPermission(PermissionKeys.UserManage);
  }

  get initials(): string {
    const name = this.auth.user()?.displayName ?? '';
    return name.split(/\s+/).filter(Boolean).slice(0, 2).map((p) => p[0].toUpperCase()).join('') || '?';
  }

  signOut(): void {
    void this.auth.logout();
  }
}
