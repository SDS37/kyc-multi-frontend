import {
  Component,
  DestroyRef,
  WritableSignal,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ActivatedRoute, Router } from '@angular/router';
import { resolvePostLoginUrl, toLoginFailedError } from '../auth.mappers';
import { LOGIN_MESSAGES } from '../auth.messages';
import { LoginFormControls } from '../auth.models';
import { LoginService } from '../login.service';
import { UI_MESSAGES } from '../../shared/ui.messages';

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
  private readonly destroyRef: DestroyRef = inject(DestroyRef);

  protected readonly copy = LOGIN_MESSAGES;
  protected readonly brand: string = UI_MESSAGES.brand;

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

    this.loginService
      .login({ tenantSlug, email, password })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (): void => {
          // Reset before navigate so a delayed/failed redirect does not leave the CTA disabled.
          this.submitting.set(false);
          const returnUrl: string | null = this.route.snapshot.queryParamMap.get('returnUrl');
          void this.router.navigateByUrl(resolvePostLoginUrl(returnUrl));
        },
        error: (err: unknown): void => {
          this.submitting.set(false);
          this.formError.set(toLoginFailedError(err).message);
        },
      });
  }
}
