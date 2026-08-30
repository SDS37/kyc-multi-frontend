import { Component, WritableSignal, inject, signal } from '@angular/core';
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router } from '@angular/router';
import { LoginFailedError, LoginService } from '../login.service';

interface LoginFormControls {
  tenantSlug: FormControl<string>;
  email: FormControl<string>;
  password: FormControl<string>;
}

/**
 * Admin / reviewer sign-in (KYC-061).
 * Fields: tenant slug, email, password → GraphQL `login` → TokenStorage → /cases.
 */
@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  private readonly fb: FormBuilder = inject(FormBuilder);
  private readonly loginService: LoginService = inject(LoginService);
  private readonly router: Router = inject(Router);
  private readonly route: ActivatedRoute = inject(ActivatedRoute);

  protected readonly submitting: WritableSignal<boolean> = signal(false);
  protected readonly formError: WritableSignal<string | null> = signal(null);

  protected readonly form: FormGroup<LoginFormControls> = this.fb.nonNullable.group({
    tenantSlug: ['', [Validators.required, Validators.maxLength(64)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.maxLength(128)]],
  });

  protected submit(): void {
    this.formError.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid || this.submitting()) {
      return;
    }

    this.submitting.set(true);
    const { tenantSlug, email, password }: { tenantSlug: string; email: string; password: string } =
      this.form.getRawValue();

    this.loginService.login({ tenantSlug, email, password }).subscribe({
      next: (): void => {
        const returnUrl: string | null = this.route.snapshot.queryParamMap.get('returnUrl');
        const target: string =
          returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//')
            ? returnUrl
            : '/cases';
        void this.router.navigateByUrl(target);
      },
      error: (err: unknown): void => {
        this.submitting.set(false);
        const message: string =
          err instanceof LoginFailedError
            ? err.message
            : 'Sign-in failed. Check your details and try again.';
        this.formError.set(message);
      },
    });
  }
}
