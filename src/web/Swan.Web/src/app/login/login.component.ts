import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'swan-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly error = signal<string | null>(null);
  readonly busy = signal(false);
  readonly expired = this.route.snapshot.queryParamMap.get('reason') === 'expired';

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  submit(): void {
    if (this.form.invalid || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();

    this.auth.login(email, password).subscribe({
      next: async () => {
        await this.auth.loadCurrentUser();
        this.busy.set(false);
        await this.router.navigateByUrl('/documents');
      },
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(this.messageFor(e));
        this.form.controls.password.reset();
      },
    });
  }

  private messageFor(e: HttpErrorResponse): string {
    switch (e.status) {
      case 401: return 'Email or password is incorrect.';
      case 423: return 'Too many failed attempts. Try again in 15 minutes.';
      case 429: return 'Too many sign-in attempts from this address. Wait a minute and try again.';
      case 0:   return 'Cannot reach the emQube publishing service. Check that it is running.';
      default:  return `Sign-in failed (HTTP ${e.status}).`;
    }
  }
}
